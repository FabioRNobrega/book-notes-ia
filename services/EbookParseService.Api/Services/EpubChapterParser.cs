using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using EbookParseService.Api.Models;
using EbookParseService.Api.Options;
using Microsoft.Extensions.Options;

namespace EbookParseService.Api.Services;

public sealed partial class EpubChapterParser(IOptions<EpubParserOptions> options) : IEpubChapterParser
{
    private const string ContainerPath = "META-INF/container.xml";
    private const string EpubNamespace = "http://www.idpf.org/2007/ops";
    private const string DublinCoreNamespace = "http://purl.org/dc/elements/1.1/";
    private static readonly HashSet<string> NarrativeBlocks =
        ["p", "blockquote", "li", "dt", "dd", "h3", "h4", "h5", "h6"];
    private static readonly HashSet<string> ExcludedElements =
        ["script", "style", "nav", "svg", "math", "audio", "video", "canvas"];
    private readonly EpubParserOptions _options = options.Value;

    public Task<ParsedEpubBook> ParseAsync(string fileName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var sourcePath = ResolveSourcePath(fileName);
            var sourceInfo = new FileInfo(sourcePath);
            if (!sourceInfo.Exists)
            {
                throw InvalidRequest("The requested EPUB does not exist in the input folder.");
            }

            var resolvedLink = sourceInfo.ResolveLinkTarget(returnFinalTarget: true);
            if (resolvedLink is not null
                && !string.Equals(
                    Path.GetDirectoryName(Path.GetFullPath(resolvedLink.FullName)),
                    Path.GetFullPath(_options.InputDirectory),
                    StringComparison.Ordinal))
            {
                throw InvalidRequest("The requested EPUB must resolve inside the input folder.");
            }

            if (sourceInfo.Length > _options.MaxSourceFileBytes)
            {
                throw InvalidRequest("The requested EPUB exceeds the configured source size limit.");
            }

            using var archive = ZipFile.OpenRead(sourcePath);
            ValidateArchive(archive);
            var entries = BuildEntryMap(archive);

            if (entries.ContainsKey("META-INF/encryption.xml"))
            {
                throw new EpubParseException(
                    EpubParseErrorKind.UnsupportedContent,
                    "Encrypted or DRM-protected EPUB content is not supported.");
            }

            var container = LoadXml(RequireEntry(entries, ContainerPath), ContainerPath);
            XNamespace containerNs = "urn:oasis:names:tc:opendocument:xmlns:container";
            var rootfile = container.Descendants(containerNs + "rootfile")
                .Select(element => (string?)element.Attribute("full-path"))
                .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));
            if (rootfile is null)
            {
                throw InvalidEpub("The EPUB container does not declare a package document.");
            }

            var packagePath = NormalizeArchivePath(string.Empty, rootfile);
            var package = LoadXml(RequireEntry(entries, packagePath), packagePath);
            var metadata = ReadPackage(package, packagePath);
            var navigation = LoadXml(RequireEntry(entries, metadata.NavigationPath), metadata.NavigationPath);
            var chapters = DiscoverChapters(archive, entries, metadata, navigation, cancellationToken);

            return Task.FromResult(new ParsedEpubBook(
                Path.GetFileName(sourcePath), metadata.Title, metadata.Language, chapters));
        }
        catch (EpubParseException)
        {
            throw;
        }
        catch (InvalidDataException exception)
        {
            throw InvalidEpub("The file is not a valid EPUB archive.", exception);
        }
        catch (XmlException exception)
        {
            throw InvalidEpub("A required EPUB XML document is malformed or exceeds configured limits.", exception);
        }
        catch (IOException exception)
        {
            throw InvalidEpub("The EPUB could not be read safely.", exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw InvalidEpub("The EPUB could not be read safely.", exception);
        }
        catch (OverflowException exception)
        {
            throw InvalidEpub("The EPUB exceeds configured archive limits.", exception);
        }
        catch (ArgumentException exception)
        {
            throw InvalidEpub("The EPUB contains an invalid path or XML value.", exception);
        }
    }

    private string ResolveSourcePath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)
            || fileName != Path.GetFileName(fileName)
            || fileName.Contains('/')
            || fileName.Contains('\\')
            || !SafeFileName().IsMatch(fileName)
            || !string.Equals(Path.GetExtension(fileName), ".epub", StringComparison.OrdinalIgnoreCase))
        {
            throw InvalidRequest("Provide a base filename ending in .epub.");
        }

        var inputRoot = Path.GetFullPath(_options.InputDirectory);
        var sourcePath = Path.GetFullPath(Path.Combine(inputRoot, fileName));
        if (!string.Equals(Path.GetDirectoryName(sourcePath), inputRoot, StringComparison.Ordinal))
        {
            throw InvalidRequest("The requested EPUB must be directly inside the input folder.");
        }

        return sourcePath;
    }

    private void ValidateArchive(ZipArchive archive)
    {
        if (archive.Entries.Count == 0
            || archive.Entries[0].FullName != "mimetype"
            || ReadSmallText(archive.Entries[0], 64) != "application/epub+zip")
        {
            throw InvalidEpub("The archive does not have a conforming EPUB media-type marker.");
        }

        if (archive.Entries.Count > _options.MaxArchiveEntryCount)
        {
            throw InvalidEpub("The EPUB exceeds the configured archive entry limit.");
        }

        long total = 0;
        foreach (var entry in archive.Entries)
        {
            if (entry.Length > _options.MaxEntryBytes)
            {
                throw InvalidEpub("An EPUB resource exceeds the configured entry size limit.");
            }

            total = checked(total + entry.Length);
            if (total > _options.MaxTotalUncompressedBytes)
            {
                throw InvalidEpub("The EPUB exceeds the configured uncompressed size limit.");
            }
        }
    }

    private static Dictionary<string, ZipArchiveEntry> BuildEntryMap(ZipArchive archive)
    {
        var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
        foreach (var entry in archive.Entries.Where(item => !string.IsNullOrEmpty(item.Name)))
        {
            var normalized = NormalizeArchivePath(string.Empty, entry.FullName);
            if (!entries.TryAdd(normalized, entry))
            {
                throw InvalidEpub("The EPUB contains duplicate archive resource paths.");
            }
        }

        return entries;
    }

    private PackageMetadata ReadPackage(XDocument package, string packagePath)
    {
        var root = package.Root ?? throw InvalidEpub("The EPUB package document is empty.");
        var opf = root.Name.Namespace;
        XNamespace dc = DublinCoreNamespace;
        var title = NormalizeWhitespace(root.Descendants(dc + "title").FirstOrDefault()?.Value);
        var language = NormalizeWhitespace(root.Descendants(dc + "language").FirstOrDefault()?.Value);
        if (title.Length == 0 || language.Length == 0)
        {
            throw InvalidEpub("The EPUB package is missing required title or language metadata.");
        }

        var manifest = new Dictionary<string, ManifestItem>(StringComparer.Ordinal);
        foreach (var item in root.Descendants(opf + "manifest").Elements(opf + "item"))
        {
            var id = (string?)item.Attribute("id");
            var href = (string?)item.Attribute("href");
            var mediaType = (string?)item.Attribute("media-type");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(href) || string.IsNullOrWhiteSpace(mediaType))
            {
                throw InvalidEpub("The EPUB manifest contains an incomplete item.");
            }

            var resolved = NormalizeArchivePath(packagePath, href);
            if (!manifest.TryAdd(id, new ManifestItem(id, resolved, mediaType, Tokenize((string?)item.Attribute("properties")))))
            {
                throw InvalidEpub("The EPUB manifest contains duplicate identifiers.");
            }
        }

        var spine = root.Descendants(opf + "spine").Elements(opf + "itemref")
            .Select(item => (string?)item.Attribute("idref"))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .ToList();
        if (spine.Count == 0 || spine.Any(id => !manifest.ContainsKey(id)))
        {
            throw InvalidEpub("The EPUB package has an invalid or empty spine.");
        }

        var navigationItems = manifest.Values.Where(item => item.Properties.Contains("nav")).ToList();
        if (navigationItems.Count != 1
            || !string.Equals(navigationItems[0].MediaType, "application/xhtml+xml", StringComparison.OrdinalIgnoreCase))
        {
            throw new EpubParseException(
                EpubParseErrorKind.UnsupportedStructure,
                "The EPUB must declare one EPUB 3 navigation document.");
        }

        var xhtmlPaths = manifest.Values
            .Where(item => string.Equals(item.MediaType, "application/xhtml+xml", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Path)
            .ToHashSet(StringComparer.Ordinal);

        return new PackageMetadata(title, language, navigationItems[0].Path, xhtmlPaths);
    }

    private IReadOnlyList<ParsedChapter> DiscoverChapters(
        ZipArchive archive,
        IReadOnlyDictionary<string, ZipArchiveEntry> entries,
        PackageMetadata metadata,
        XDocument navigation,
        CancellationToken cancellationToken)
    {
        XNamespace epub = EpubNamespace;
        var toc = navigation.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "nav"
                && Tokenize((string?)element.Attribute(epub + "type")).Contains("toc"));
        if (toc is null)
        {
            throw UnsupportedStructure("The EPUB navigation document has no table of contents.");
        }

        var documentCache = new Dictionary<string, XDocument>(StringComparer.Ordinal);
        var candidates = new List<ChapterCandidate>();
        var numericLabels = new HashSet<int>();
        var targets = new HashSet<string>(StringComparer.Ordinal);

        foreach (var link in toc.Descendants().Where(element => element.Name.LocalName == "a"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var href = (string?)link.Attribute("href");
            if (string.IsNullOrWhiteSpace(href))
            {
                throw UnsupportedStructure("A table-of-contents link has no target.");
            }

            var (resourcePath, fragment) = ResolveNavigationTarget(metadata.NavigationPath, href);
            if (!metadata.XhtmlPaths.Contains(resourcePath))
            {
                throw UnsupportedStructure("A table-of-contents target is outside the declared XHTML manifest.");
            }

            var document = GetContentDocument(resourcePath, entries, documentCache);
            var target = document.Root?.DescendantsAndSelf()
                .FirstOrDefault(element => (string?)element.Attribute("id") == fragment
                    || (string?)element.Attribute(XNamespace.Xml + "id") == fragment);
            if (target is null)
            {
                throw UnsupportedStructure("A table-of-contents fragment target is missing.");
            }

            var label = NormalizeWhitespace(link.Value);
            var semanticBoundary = target.AncestorsAndSelf()
                .FirstOrDefault(element => IsStructural(element) && HasEpubType(element, "chapter"));
            var numericMatch = NumericLabel().Match(label);
            var numeric = numericMatch.Success ? int.Parse(numericMatch.Groups[1].Value, CultureInfo.InvariantCulture) : (int?)null;
            var numericBoundary = numeric.HasValue ? FindNumericBoundary(target, numeric.Value) : null;
            if (semanticBoundary is null && numericBoundary is null)
            {
                continue;
            }

            var key = resourcePath + "#" + fragment;
            if (!targets.Add(key))
            {
                throw UnsupportedStructure("The EPUB table of contents contains a duplicate chapter target.");
            }

            if (numeric.HasValue && !numericLabels.Add(numeric.Value))
            {
                throw UnsupportedStructure("The EPUB table of contents contains a duplicate chapter number.");
            }

            candidates.Add(new ChapterCandidate(resourcePath, target, semanticBoundary ?? numericBoundary!, numeric));
        }

        if (candidates.Count == 0)
        {
            throw UnsupportedStructure("No chapters matched the supported EPUB navigation structures.");
        }

        var chapters = new List<ParsedChapter>(candidates.Count);
        for (var index = 0; index < candidates.Count; index++)
        {
            var paragraphs = ExtractParagraphs(candidates[index], candidates);
            if (paragraphs.Count == 0)
            {
                throw UnsupportedStructure("A discovered chapter contains no narration-ready text.");
            }

            chapters.Add(new ParsedChapter(index + 1, paragraphs));
        }

        return chapters;
    }

    private XDocument GetContentDocument(
        string path,
        IReadOnlyDictionary<string, ZipArchiveEntry> entries,
        IDictionary<string, XDocument> cache)
    {
        if (!cache.TryGetValue(path, out var document))
        {
            document = LoadXml(RequireEntry(entries, path), path);
            cache[path] = document;
        }

        return document;
    }

    private static XElement? FindNumericBoundary(XElement target, int number)
    {
        if (IsHeading(target) && NormalizeWhitespace(target.Value) == number.ToString(CultureInfo.InvariantCulture))
        {
            return target;
        }

        if (!IsStructural(target))
        {
            return null;
        }

        var heading = target.Elements().FirstOrDefault(IsHeading);
        return heading is not null
            && NormalizeWhitespace(heading.Value) == number.ToString(CultureInfo.InvariantCulture)
                ? target
                : null;
    }

    private static IReadOnlyList<string> ExtractParagraphs(
        ChapterCandidate candidate,
        IReadOnlyList<ChapterCandidate> allCandidates)
    {
        IEnumerable<XElement> scope;
        if (IsStructural(candidate.Boundary))
        {
            scope = candidate.Boundary.Descendants();
        }
        else if (IsHeading(candidate.Boundary) && candidate.Boundary.Parent is not null)
        {
            var nextTargets = allCandidates
                .Where(other => ReferenceEquals(other.Target.Parent, candidate.Target.Parent))
                .Select(other => other.Target)
                .ToHashSet();
            scope = candidate.Boundary.ElementsAfterSelf().TakeWhile(element => !nextTargets.Contains(element));
        }
        else
        {
            throw UnsupportedStructure("A chapter target does not have an unambiguous structural boundary.");
        }

        var blocks = scope
            .SelectMany(element => NarrativeBlocks.Contains(element.Name.LocalName)
                ? [element]
                : element.Descendants().Where(descendant => NarrativeBlocks.Contains(descendant.Name.LocalName)))
            .Where(element => !element.Ancestors().Any(ancestor => ExcludedElements.Contains(ancestor.Name.LocalName)))
            .Distinct()
            .Where(element => !element.Ancestors().Any(ancestor => NarrativeBlocks.Contains(ancestor.Name.LocalName)));

        return blocks
            .Select(ReadNarrativeText)
            .Where(text => text.Length > 0)
            .ToList();
    }

    private static string ReadNarrativeText(XElement block)
    {
        var text = string.Concat(block.DescendantNodesAndSelf()
            .OfType<XText>()
            .Where(node => !node.Ancestors().Any(IsExcludedInline))
            .Select(node => node.Value));
        return NormalizeWhitespace(text);
    }

    private static bool IsExcludedInline(XElement element)
    {
        if (ExcludedElements.Contains(element.Name.LocalName))
        {
            return true;
        }

        XNamespace epub = EpubNamespace;
        var types = Tokenize((string?)element.Attribute(epub + "type"));
        return types.Contains("noteref") || types.Contains("backlink") || types.Contains("pagebreak");
    }

    private XDocument LoadXml(ZipArchiveEntry entry, string logicalName)
    {
        if (entry.Length > _options.MaxEntryBytes)
        {
            throw InvalidEpub("A required EPUB document exceeds the configured entry size limit.");
        }

        try
        {
            using var stream = entry.Open();
            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                XmlResolver = null,
                MaxCharactersInDocument = _options.MaxXmlCharacters,
                MaxCharactersFromEntities = 0,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
                CloseInput = false
            });
            return XDocument.Load(reader, LoadOptions.None);
        }
        catch (XmlException exception)
        {
            throw InvalidEpub("A required EPUB XML document is malformed or exceeds configured limits.", exception);
        }
        catch (InvalidOperationException exception)
        {
            throw InvalidEpub("A required EPUB XML document could not be parsed.", exception);
        }
    }

    private static (string Path, string Fragment) ResolveNavigationTarget(string referringPath, string href)
    {
        var hash = href.IndexOf('#');
        if (hash < 1 || hash == href.Length - 1 || href.IndexOf('?', StringComparison.Ordinal) >= 0)
        {
            throw UnsupportedStructure("Every supported chapter link must identify an XHTML fragment.");
        }

        var path = NormalizeArchivePath(referringPath, href[..hash]);
        var fragment = DecodeUriComponent(href[(hash + 1)..]);
        if (fragment.Length == 0 || fragment.Contains('/') || fragment.Contains('\\'))
        {
            throw UnsupportedStructure("A table-of-contents fragment is invalid.");
        }

        return (path, fragment);
    }

    private static string NormalizeArchivePath(string referringPath, string href)
    {
        if (string.IsNullOrWhiteSpace(href)
            || href.StartsWith('/')
            || href.Contains('\\')
            || href.Contains('?')
            || Uri.TryCreate(href, UriKind.Absolute, out _))
        {
            throw InvalidEpub("The EPUB contains an unsafe resource path.");
        }

        var decoded = DecodeUriComponent(href.Split('#')[0]);
        if (decoded.Contains('\\') || decoded.StartsWith('/'))
        {
            throw InvalidEpub("The EPUB contains an unsafe resource path.");
        }

        var baseParts = referringPath.Contains('/')
            ? referringPath[..(referringPath.LastIndexOf('/') + 1)].Split('/', StringSplitOptions.RemoveEmptyEntries).ToList()
            : [];
        foreach (var part in decoded.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".")
            {
                continue;
            }

            if (part == "..")
            {
                if (baseParts.Count == 0)
                {
                    throw InvalidEpub("The EPUB contains a resource path outside the archive root.");
                }

                baseParts.RemoveAt(baseParts.Count - 1);
                continue;
            }

            if (part.Contains(':') || part.IndexOf('\0') >= 0)
            {
                throw InvalidEpub("The EPUB contains an unsafe resource path.");
            }

            baseParts.Add(part);
        }

        if (baseParts.Count == 0)
        {
            throw InvalidEpub("The EPUB contains an empty resource path.");
        }

        return string.Join('/', baseParts);
    }

    private static ZipArchiveEntry RequireEntry(IReadOnlyDictionary<string, ZipArchiveEntry> entries, string path) =>
        entries.TryGetValue(path, out var entry)
            ? entry
            : throw InvalidEpub("A required EPUB resource is missing.");

    private static string ReadSmallText(ZipArchiveEntry entry, int maxCharacters)
    {
        if (entry.Length > maxCharacters)
        {
            return string.Empty;
        }

        using var stream = entry.Open();
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
        var text = reader.ReadToEnd();
        return text.Length <= maxCharacters ? text : string.Empty;
    }

    private static string DecodeUriComponent(string value)
    {
        try
        {
            return Uri.UnescapeDataString(value);
        }
        catch (UriFormatException exception)
        {
            throw InvalidEpub("The EPUB contains an invalid encoded resource path.", exception);
        }
    }

    private static HashSet<string> Tokenize(string? value) =>
        (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static bool HasEpubType(XElement element, string value)
    {
        XNamespace epub = EpubNamespace;
        return Tokenize((string?)element.Attribute(epub + "type")).Contains(value);
    }

    private static bool IsStructural(XElement element) =>
        element.Name.LocalName is "section" or "article";

    private static bool IsHeading(XElement element) =>
        element.Name.LocalName is "h1" or "h2" or "h3" or "h4" or "h5" or "h6";

    private static string NormalizeWhitespace(string? value) =>
        Whitespace().Replace((value ?? string.Empty).Replace('\u00a0', ' '), " ").Trim();

    private static EpubParseException InvalidRequest(string message) =>
        new(EpubParseErrorKind.InvalidRequest, message);

    private static EpubParseException InvalidEpub(string message, Exception? inner = null) =>
        new(EpubParseErrorKind.InvalidEpub, message, inner);

    private static EpubParseException UnsupportedStructure(string message) =>
        new(EpubParseErrorKind.UnsupportedStructure, message);

    private sealed record ManifestItem(string Id, string Path, string MediaType, HashSet<string> Properties);
    private sealed record PackageMetadata(string Title, string Language, string NavigationPath, HashSet<string> XhtmlPaths);
    private sealed record ChapterCandidate(string ResourcePath, XElement Target, XElement Boundary, int? DeclaredNumber);

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._ -]*$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeFileName();

    [GeneratedRegex(@"^0*([1-9][0-9]*)$", RegexOptions.CultureInvariant)]
    private static partial Regex NumericLabel();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();
}
