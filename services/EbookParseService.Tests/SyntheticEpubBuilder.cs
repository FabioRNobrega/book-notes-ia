using System.IO.Compression;
using System.Text;

namespace EbookParseService.Tests;

internal sealed class SyntheticEpubBuilder
{
    private string _mimetype = "application/epub+zip";
    private bool _mimetypeFirst = true;
    private bool _encrypted;
    private string _navigation = DefaultNavigation;
    private string _content = DefaultContent;

    public SyntheticEpubBuilder WithWrongMimetype()
    {
        _mimetype = "application/zip";
        return this;
    }

    public SyntheticEpubBuilder WithMimetypeAfterContainer()
    {
        _mimetypeFirst = false;
        return this;
    }

    public SyntheticEpubBuilder WithEncryption()
    {
        _encrypted = true;
        return this;
    }

    public SyntheticEpubBuilder WithNavigation(string navigation)
    {
        _navigation = navigation;
        return this;
    }

    public SyntheticEpubBuilder WithContent(string content)
    {
        _content = content;
        return this;
    }

    public void Build(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var file = File.Create(path);
        using var archive = new ZipArchive(file, ZipArchiveMode.Create);
        if (_mimetypeFirst)
        {
            Add(archive, "mimetype", _mimetype, CompressionLevel.NoCompression);
        }

        Add(archive, "META-INF/container.xml", Container);
        if (!_mimetypeFirst)
        {
            Add(archive, "mimetype", _mimetype, CompressionLevel.NoCompression);
        }

        if (_encrypted)
        {
            Add(archive, "META-INF/encryption.xml", "<encryption xmlns=\"urn:oasis:names:tc:opendocument:xmlns:container\" />");
        }

        Add(archive, "OPS/package.opf", Package);
        Add(archive, "OPS/nav/toc.xhtml", _navigation);
        Add(archive, "OPS/text/book.xhtml", _content);
    }

    private static void Add(
        ZipArchive archive,
        string name,
        string content,
        CompressionLevel compression = CompressionLevel.Optimal)
    {
        var entry = archive.CreateEntry(name, compression);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content);
    }

    private const string Container = """
        <?xml version="1.0"?>
        <container xmlns="urn:oasis:names:tc:opendocument:xmlns:container" version="1.0">
          <rootfiles><rootfile full-path="OPS/package.opf" media-type="application/oebps-package+xml" /></rootfiles>
        </container>
        """;

    private const string Package = """
        <?xml version="1.0" encoding="utf-8"?>
        <package xmlns="http://www.idpf.org/2007/opf" version="3.0" unique-identifier="id">
          <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
            <dc:identifier id="id">synthetic-book</dc:identifier>
            <dc:title>Á Sample Book</dc:title>
            <dc:language>en-US</dc:language>
          </metadata>
          <manifest>
            <item id="nav" href="nav/toc.xhtml" media-type="application/xhtml+xml" properties="nav" />
            <item id="book" href="text/book.xhtml" media-type="application/xhtml+xml" />
          </manifest>
          <spine><itemref idref="book" /></spine>
        </package>
        """;

    internal const string DefaultNavigation = """
        <?xml version="1.0" encoding="utf-8"?>
        <html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops">
          <body><nav epub:type="toc"><ol>
            <li><a href="../text/book.xhtml#title">Title</a></li>
            <li><a href="../text/book.xhtml#part-one">PART ONE</a></li>
            <li><a href="../text/book.xhtml#one">1</a></li>
            <li><a href="../text/book.xhtml#two">2</a></li>
            <li><a href="../text/book.xhtml#three">3</a></li>
            <li><a href="../text/book.xhtml#coda">CODA</a></li>
          </ol></nav></body>
        </html>
        """;

    internal const string DefaultContent = """
        <?xml version="1.0" encoding="utf-8"?>
        <!DOCTYPE html>
        <html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops">
          <head><title>Fixture</title><style>.hidden { display:none; }</style></head>
          <body>
            <section id="title"><h1>A Sample Book</h1></section>
            <section id="part-one"><h1>PART ONE</h1></section>
            <section id="one"><h2>1</h2><p>First <em>synthetic</em> paragraph — safe.</p><p>Second paragraph<a epub:type="noteref" href="#note">1</a>.</p><script>private script text</script></section>
            <section id="two"><h2>2</h2><p>Only chapter two text.</p><nav>navigation noise</nav></section>
            <section id="three"><h2>3</h2><p>Only chapter three text.</p><img src="cover.png" alt="image words" /></section>
            <section id="coda"><h1>CODA</h1><p>Not a numbered chapter.</p></section>
          </body>
        </html>
        """;
}
