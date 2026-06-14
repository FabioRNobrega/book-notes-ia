using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using TtsService.Api.Models;
using TtsService.Api.Options;

namespace TtsService.Api.Services;

public sealed class SupertonicTtsService : ITtsService, IDisposable
{
    private readonly SupertonicOptions _options;
    private readonly VoiceResolver _voiceResolver;
    private readonly ILogger<SupertonicTtsService> _logger;

    // Loaded once at startup and shared across all requests.
    private readonly SupertonicEngine _engine;

    public SupertonicTtsService(
        IOptions<SupertonicOptions> options,
        VoiceResolver voiceResolver,
        ILogger<SupertonicTtsService> logger)
    {
        _options = options.Value;
        _voiceResolver = voiceResolver;
        _logger = logger;

        var onnxDir = Path.Combine(_options.AssetsPath, "onnx");
        var assetsExist = Directory.Exists(onnxDir);

        _logger.LogInformation(
            "[Supertonic] Initializing -- assets_path={AssetsPath} onnx_dir={OnnxDir} exists={Exists}",
            _options.AssetsPath, onnxDir, assetsExist);

        if (!assetsExist)
        {
            _logger.LogError(
                "[Supertonic] ONNX directory not found: {OnnxDir}. " +
                "Run the asset download script or set Tts:UsePlaceholder=true.",
                onnxDir);
            throw new InvalidOperationException(
                $"Supertonic ONNX assets not found at '{onnxDir}'. " +
                "Download assets from https://huggingface.co/Supertone/supertonic-3 " +
                "or set Tts:UsePlaceholder=true for development.");
        }

        _logger.LogInformation("[Supertonic] Loading ONNX models...");
        _engine = new SupertonicEngine(onnxDir, _logger);
        _logger.LogInformation("[Supertonic] Ready -- sample_rate={SampleRate}Hz", _engine.SampleRate);
    }

    public async Task<TtsAudioResult> SynthesizeAsync(TtsRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            throw new ArgumentException("Text is required.", nameof(request));

        var language = VoiceResolver.NormalizeLanguage(request.Language);
        var voiceName = _voiceResolver.Resolve(language, request.VoiceGender, request.VoiceName);
        var speed = request.Speed ?? _options.DefaultSpeed;
        var totalSteps = request.TotalSteps ?? _options.DefaultTotalSteps;

        var voiceStylePath = Path.Combine(_options.AssetsPath, "voice_styles", $"{voiceName}.json");
        if (!File.Exists(voiceStylePath))
        {
            _logger.LogError("[Supertonic] Voice style file not found: {Path}", voiceStylePath);
            throw new FileNotFoundException($"Voice style '{voiceName}' not found at '{voiceStylePath}'.");
        }

        _logger.LogInformation(
            "[Supertonic] Synthesizing -- language={Language} voice={Voice} text_length={Len} total_steps={Steps} speed={Speed}",
            language, voiceName, request.Text.Length, totalSteps, speed);

        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Offload CPU-intensive ONNX inference off the ASP.NET thread pool.
        var (wavData, durationSeconds) = await Task.Run(() =>
            _engine.Synthesize(request.Text, language, voiceStylePath, totalSteps, speed),
            cancellationToken);

        sw.Stop();

        var wavBytes = WavEncoder.Encode(wavData, _engine.SampleRate);

        _logger.LogInformation(
            "[Supertonic] Done -- language={Language} voice={Voice} duration={Duration:0.00}s wav_bytes={Bytes} elapsed={Elapsed:0.00}s",
            language, voiceName, durationSeconds, wavBytes.Length, sw.Elapsed.TotalSeconds);

        return new TtsAudioResult
        {
            WavBytes = wavBytes,
            Language = language,
            VoiceName = voiceName,
            DurationSeconds = durationSeconds
        };
    }

    public void Dispose() => _engine.Dispose();
}

// ---------------------------------------------------------------------------
// Ported from the official Supertonic csharp/ example (MIT licensed).
// https://github.com/supertone-inc/supertonic/tree/main/csharp
// ---------------------------------------------------------------------------

internal sealed class SupertonicEngine : IDisposable
{
    private readonly SupertonicConfig _cfg;
    private readonly UnicodeProcessor _textProcessor;
    private readonly InferenceSession _dp;
    private readonly InferenceSession _textEnc;
    private readonly InferenceSession _vectorEst;
    private readonly InferenceSession _vocoder;

    public int SampleRate => _cfg.Ae.SampleRate;
    private int BaseChunkSize => _cfg.Ae.BaseChunkSize;
    private int ChunkCompressFactor => _cfg.Ttl.ChunkCompressFactor;
    private int LatentDim => _cfg.Ttl.LatentDim;

    public SupertonicEngine(string onnxDir, ILogger logger)
    {
        _cfg = LoadConfig(onnxDir);
        _textProcessor = new UnicodeProcessor(Path.Combine(onnxDir, "unicode_indexer.json"));

        var opts = new Microsoft.ML.OnnxRuntime.SessionOptions();
        opts.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;

        logger.LogInformation("[Supertonic] Loading duration_predictor.onnx...");
        _dp = new InferenceSession(Path.Combine(onnxDir, "duration_predictor.onnx"), opts);

        logger.LogInformation("[Supertonic] Loading text_encoder.onnx...");
        _textEnc = new InferenceSession(Path.Combine(onnxDir, "text_encoder.onnx"), opts);

        logger.LogInformation("[Supertonic] Loading vector_estimator.onnx...");
        _vectorEst = new InferenceSession(Path.Combine(onnxDir, "vector_estimator.onnx"), opts);

        logger.LogInformation("[Supertonic] Loading vocoder.onnx...");
        _vocoder = new InferenceSession(Path.Combine(onnxDir, "vocoder.onnx"), opts);
    }

    public (float[] wav, double durationSeconds) Synthesize(
        string text, string lang, string voiceStylePath, int totalSteps, float speed)
    {
        var style = LoadVoiceStyle(voiceStylePath);
        int maxLen = (lang == "ko" || lang == "ja") ? 120 : 300;
        var chunks = ChunkText(text, maxLen);

        var wavCat = new List<float>();
        double durTotal = 0.0;
        const float silenceDuration = 0.3f;

        foreach (var chunk in chunks)
        {
            var (wav, dur) = Infer(chunk, lang, style, totalSteps, speed);
            if (wavCat.Count > 0)
            {
                wavCat.AddRange(new float[(int)(silenceDuration * SampleRate)]);
                durTotal += silenceDuration;
            }
            wavCat.AddRange(wav);
            durTotal += dur;
        }

        return (wavCat.ToArray(), durTotal);
    }

    private (float[] wav, double durationSeconds) Infer(
        string text, string lang, VoiceStyle style, int totalSteps, float speed)
    {
        var (textIds, textMask) = _textProcessor.Process(text, lang);
        int bsz = 1;
        long seqLen = textIds.Length;

        var textIdsTensor = new DenseTensor<long>(textIds, [bsz, (int)seqLen]);
        var textMaskTensor = new DenseTensor<float>(textMask, [bsz, 1, (int)seqLen]);
        var styleTtlTensor = new DenseTensor<float>(style.Ttl, style.TtlShape);
        var styleDpTensor = new DenseTensor<float>(style.Dp, style.DpShape);

        // Duration predictor
        using var dpOut = _dp.Run([
            NamedOnnxValue.CreateFromTensor("text_ids", textIdsTensor),
            NamedOnnxValue.CreateFromTensor("style_dp", styleDpTensor),
            NamedOnnxValue.CreateFromTensor("text_mask", textMaskTensor)
        ]);
        var dur = dpOut.First(o => o.Name == "duration").AsTensor<float>().ToArray();
        for (int i = 0; i < dur.Length; i++) dur[i] /= speed;

        // Text encoder
        using var textEncOut = _textEnc.Run([
            NamedOnnxValue.CreateFromTensor("text_ids", textIdsTensor),
            NamedOnnxValue.CreateFromTensor("style_ttl", styleTtlTensor),
            NamedOnnxValue.CreateFromTensor("text_mask", textMaskTensor)
        ]);
        var textEmb = textEncOut.First(o => o.Name == "text_emb").AsTensor<float>();

        // Sample noisy latent
        float durationSeconds = dur[0];
        long wavLen = (long)(durationSeconds * SampleRate);
        int chunkSize = BaseChunkSize * ChunkCompressFactor;
        int latentLen = (int)((wavLen + chunkSize - 1) / chunkSize);
        int latentDim = LatentDim * ChunkCompressFactor;

        var xt = SampleNoise(latentDim, latentLen);
        var latentMask = BuildLatentMask(wavLen, BaseChunkSize, ChunkCompressFactor, latentLen);

        // Mask noisy latent
        for (int d = 0; d < latentDim; d++)
            for (int t = 0; t < latentLen; t++)
                xt[d * latentLen + t] *= latentMask[t];

        int[] latentShape = [bsz, latentDim, latentLen];
        int[] latentMaskShape = [bsz, 1, latentLen];

        // Iterative denoising
        for (int step = 0; step < totalSteps; step++)
        {
            var xtTensor = new DenseTensor<float>(xt, latentShape);
            var latentMaskTensor = new DenseTensor<float>(latentMask, latentMaskShape);
            var totalStepTensor = new DenseTensor<float>(new float[] { (float)totalSteps }, new int[] { bsz });
            var currentStepTensor = new DenseTensor<float>(new float[] { (float)step }, new int[] { bsz });

            using var estOut = _vectorEst.Run([
                NamedOnnxValue.CreateFromTensor("noisy_latent", xtTensor),
                NamedOnnxValue.CreateFromTensor("text_emb", textEmb),
                NamedOnnxValue.CreateFromTensor("style_ttl", styleTtlTensor),
                NamedOnnxValue.CreateFromTensor("text_mask", textMaskTensor),
                NamedOnnxValue.CreateFromTensor("latent_mask", latentMaskTensor),
                NamedOnnxValue.CreateFromTensor("total_step", totalStepTensor),
                NamedOnnxValue.CreateFromTensor("current_step", currentStepTensor)
            ]);
            xt = estOut.First(o => o.Name == "denoised_latent").AsTensor<float>().ToArray();
        }

        // Vocoder
        using var vocOut = _vocoder.Run([
            NamedOnnxValue.CreateFromTensor("latent", new DenseTensor<float>(xt, latentShape))
        ]);
        var wav = vocOut.First(o => o.Name == "wav_tts").AsTensor<float>().ToArray();

        // Trim to actual duration
        int expectedSamples = (int)(durationSeconds * SampleRate);
        if (wav.Length > expectedSamples)
        {
            var trimmed = new float[expectedSamples];
            Array.Copy(wav, trimmed, expectedSamples);
            wav = trimmed;
        }

        return (wav, durationSeconds);
    }

    private static float[] SampleNoise(int latentDim, int latentLen)
    {
        var rng = new Random();
        var noise = new float[latentDim * latentLen];
        for (int i = 0; i < noise.Length; i++)
        {
            // Box-Muller transform
            double u1 = 1.0 - rng.NextDouble();
            double u2 = 1.0 - rng.NextDouble();
            noise[i] = (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
        }
        return noise;
    }

    private static float[] BuildLatentMask(long wavLen, int baseChunkSize, int chunkCompressFactor, int latentLen)
    {
        long latentSize = baseChunkSize * chunkCompressFactor;
        long validLatentLen = (wavLen + latentSize - 1) / latentSize;
        var mask = new float[latentLen];
        for (int t = 0; t < latentLen; t++)
            mask[t] = t < validLatentLen ? 1.0f : 0.0f;
        return mask;
    }

    private static VoiceStyle LoadVoiceStyle(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;

        var ttlDims = ParseDims(root.GetProperty("style_ttl").GetProperty("dims"));
        var dpDims = ParseDims(root.GetProperty("style_dp").GetProperty("dims"));

        var ttl = FlattenFloats(root.GetProperty("style_ttl").GetProperty("data"));
        var dp = FlattenFloats(root.GetProperty("style_dp").GetProperty("data"));

        return new VoiceStyle(ttl, ttlDims, dp, dpDims);
    }

    private static int[] ParseDims(JsonElement el)
        => el.EnumerateArray().Select(x => (int)x.GetInt64()).ToArray();

    private static float[] FlattenFloats(JsonElement el)
    {
        var result = new List<float>();
        Flatten(el, result);
        return result.ToArray();

        static void Flatten(JsonElement e, List<float> acc)
        {
            if (e.ValueKind == JsonValueKind.Array)
                foreach (var child in e.EnumerateArray()) Flatten(child, acc);
            else
                acc.Add(e.GetSingle());
        }
    }

    private static SupertonicConfig LoadConfig(string onnxDir)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(onnxDir, "tts.json")));
        var root = doc.RootElement;
        return new SupertonicConfig
        {
            Ae = new SupertonicConfig.AeConfig
            {
                SampleRate = root.GetProperty("ae").GetProperty("sample_rate").GetInt32(),
                BaseChunkSize = root.GetProperty("ae").GetProperty("base_chunk_size").GetInt32()
            },
            Ttl = new SupertonicConfig.TtlConfig
            {
                ChunkCompressFactor = root.GetProperty("ttl").GetProperty("chunk_compress_factor").GetInt32(),
                LatentDim = root.GetProperty("ttl").GetProperty("latent_dim").GetInt32()
            }
        };
    }

    private static List<string> ChunkText(string text, int maxLen)
    {
        var paragraphSplit = new Regex(@"\n\s*\n+");
        var sentenceSplit = new Regex(
            @"(?<!Mr\.|Mrs\.|Ms\.|Dr\.|Prof\.|Sr\.|Jr\.|Ph\.D\.|etc\.|e\.g\.|i\.e\.|vs\.|Inc\.|Ltd\.|Co\.|Corp\.|St\.|Ave\.|Blvd\.)(?<!\b[A-Z]\.)(?<=[.!?])\s+");

        var chunks = new List<string>();
        var paragraphs = paragraphSplit.Split(text.Trim())
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrEmpty(p));

        foreach (var para in paragraphs)
        {
            var sentences = sentenceSplit.Split(para);
            var current = new StringBuilder();
            foreach (var sentence in sentences)
            {
                if (string.IsNullOrEmpty(sentence)) continue;
                if (current.Length > 0 && current.Length + sentence.Length + 1 <= maxLen)
                {
                    current.Append(' ');
                    current.Append(sentence);
                }
                else
                {
                    if (current.Length > 0) chunks.Add(current.ToString().Trim());
                    current.Clear();
                    current.Append(sentence);
                }
            }
            if (current.Length > 0) chunks.Add(current.ToString().Trim());
        }

        if (chunks.Count == 0) chunks.Add(text.Trim());
        return chunks;
    }

    public void Dispose()
    {
        _dp.Dispose();
        _textEnc.Dispose();
        _vectorEst.Dispose();
        _vocoder.Dispose();
    }

    private sealed record SupertonicConfig
    {
        public AeConfig Ae { get; init; } = default!;
        public TtlConfig Ttl { get; init; } = default!;
        public sealed record AeConfig { public int SampleRate { get; init; } public int BaseChunkSize { get; init; } }
        public sealed record TtlConfig { public int ChunkCompressFactor { get; init; } public int LatentDim { get; init; } }
    }

    private sealed record VoiceStyle(float[] Ttl, int[] TtlShape, float[] Dp, int[] DpShape);
}

// ---------------------------------------------------------------------------
// Text processor -- ported from Helper.cs (official example)
// ---------------------------------------------------------------------------

internal sealed class UnicodeProcessor
{
    private static readonly string[] SupportedLangs =
        ["en", "ko", "ja", "ar", "bg", "cs", "da", "de", "el", "es", "et", "fi", "fr",
         "hi", "hr", "hu", "id", "it", "lt", "lv", "nl", "pl", "pt", "ro", "ru", "sk",
         "sl", "sv", "tr", "uk", "vi", "na"];

    private readonly Dictionary<int, long> _indexer;

    // Keys that are non-ASCII strings are fine in C# -- only char literals need \uXXXX.
    // We use string keys with actual Unicode chars for the replacements dict.
    private static readonly Dictionary<string, string> Replacements = new()
    {
        ["–"] = "-", ["‑"] = "-", ["—"] = "-", ["_"] = " ",
        ["“"] = "\"", ["”"] = "\"", ["‘"] = "'", ["’"] = "'",
        ["´"] = "'", ["`"] = "'", ["["] = " ", ["]"] = " ",
        ["|"] = " ", ["/"] = " ", ["#"] = " ", ["→"] = " ", ["←"] = " "
    };

    private static readonly Dictionary<string, string> ExprReplacements = new()
    {
        ["@"] = " at ", ["e.g.,"] = "for example, ", ["i.e.,"] = "that is, "
    };

    public UnicodeProcessor(string indexerPath)
    {
        var raw = JsonSerializer.Deserialize<long[]>(File.ReadAllText(indexerPath))
            ?? throw new InvalidOperationException("Failed to parse unicode_indexer.json");
        _indexer = raw.Select((v, i) => (i, v)).ToDictionary(x => x.i, x => x.v);
    }

    public (long[] textIds, float[] textMask) Process(string text, string lang)
    {
        var processed = Preprocess(text, lang);
        var chars = processed.Select(c => (int)c).ToArray();
        var textIds = chars.Select(c => _indexer.TryGetValue(c, out var v) ? v : 0L).ToArray();
        var textMask = Enumerable.Repeat(1.0f, textIds.Length).ToArray();
        return (textIds, textMask);
    }

    private static string Preprocess(string text, string lang)
    {
        text = text.Normalize(NormalizationForm.FormKD);
        text = StripEmojis(text);

        foreach (var (k, v) in Replacements) text = text.Replace(k, v);
        text = Regex.Replace(text, @"[♥☆♡©\\]", "");
        foreach (var (k, v) in ExprReplacements) text = text.Replace(k, v);

        text = Regex.Replace(text, @" ,", ",");
        text = Regex.Replace(text, @" \.", ".");
        text = Regex.Replace(text, @" !", "!");
        text = Regex.Replace(text, @" \?", "?");
        text = Regex.Replace(text, @" ;", ";");
        text = Regex.Replace(text, @" :", ":");
        text = Regex.Replace(text, @" '", "'");
        while (text.Contains("\"\"")) text = text.Replace("\"\"", "\"");
        while (text.Contains("''")) text = text.Replace("''", "'");
        text = Regex.Replace(text, @"\s+", " ").Trim();

        // Ensure text ends with terminal punctuation.
        if (!EndsWithTerminalPunctuation(text))
            text += ".";

        if (!SupportedLangs.Contains(lang))
            throw new ArgumentException($"Unsupported language: {lang}");

        return $"<{lang}>{text}</{lang}>";
    }

    // All char literals use \uXXXX -- no literal non-ASCII chars as char literal content.
    private static bool EndsWithTerminalPunctuation(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        char last = text[^1];
        return ".!?;:,'\"()[]{}".IndexOf(last) >= 0
            || last == '\u201C' || last == '\u201D'
            || last == '\u2018' || last == '\u2019'
            || last == '\u2026'
            || last == '\u3002'
            || last == '\u300D' || last == '\u300F'
            || last == '\u3011'
            || last == '\u3009' || last == '\u300B'
            || last == '\u203A' || last == '\u00BB';
    }

    private static string StripEmojis(string text)
    {
        var sb = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            int cp = char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1])
                ? char.ConvertToUtf32(text[i], text[++i])
                : text[i];

            bool isEmoji =
                (cp >= 0x1F600 && cp <= 0x1F64F) || (cp >= 0x1F300 && cp <= 0x1F5FF) ||
                (cp >= 0x1F680 && cp <= 0x1F6FF) || (cp >= 0x1F700 && cp <= 0x1FAFF) ||
                (cp >= 0x2600 && cp <= 0x27BF) || (cp >= 0x1F1E6 && cp <= 0x1F1FF);

            if (!isEmoji)
                sb.Append(cp > 0xFFFF ? char.ConvertFromUtf32(cp) : ((char)cp).ToString());
        }
        return sb.ToString();
    }
}

// ---------------------------------------------------------------------------
// WAV encoder -- ported from Helper.WriteWavFile (official example)
// ---------------------------------------------------------------------------

internal static class WavEncoder
{
    public static byte[] Encode(float[] samples, int sampleRate)
    {
        const int bitsPerSample = 16;
        int dataSize = samples.Length * 2;

        using var ms = new MemoryStream(44 + dataSize);
        using var w = new BinaryWriter(ms);

        w.Write(Encoding.ASCII.GetBytes("RIFF"));
        w.Write(36 + dataSize);
        w.Write(Encoding.ASCII.GetBytes("WAVE"));
        w.Write(Encoding.ASCII.GetBytes("fmt "));
        w.Write(16);
        w.Write((short)1);          // PCM
        w.Write((short)1);          // mono
        w.Write(sampleRate);
        w.Write(sampleRate * 2);    // byte rate
        w.Write((short)2);          // block align
        w.Write((short)bitsPerSample);
        w.Write(Encoding.ASCII.GetBytes("data"));
        w.Write(dataSize);

        foreach (var s in samples)
        {
            float clamped = Math.Max(-1.0f, Math.Min(1.0f, s));
            w.Write((short)(clamped * 32767));
        }

        return ms.ToArray();
    }
}
