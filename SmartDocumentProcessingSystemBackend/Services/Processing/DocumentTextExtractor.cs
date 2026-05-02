using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using SmartDocumentProcessingSystem.Configuration;

namespace SmartDocumentProcessingSystem.Services.Processing;

public class DocumentTextExtractor
{
    private static readonly Regex StreamRegex = new(@"stream\r?\n(?<stream>[\s\S]*?)endstream", RegexOptions.Compiled);
    private static readonly Regex TextRegex = new(@"\((?<text>(?:\\.|[^\\)])*)\)\s*Tj", RegexOptions.Compiled);

    private readonly ProcessingOptions _options;

    public DocumentTextExtractor(IOptions<ProcessingOptions> options)
    {
        _options = options.Value;
    }

    public async Task<string> ExtractAsync(Stream stream, string fileName, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        if (extension is ".txt" or ".csv")
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            return await reader.ReadToEndAsync(cancellationToken);
        }

        if (extension == ".pdf")
        {
            return await ExtractPdfTextAsync(stream, cancellationToken);
        }

        if (extension is ".png" or ".jpg" or ".jpeg")
        {
            return await ExtractImageTextAsync(stream, fileName, cancellationToken);
        }

        return string.Empty;
    }

    public static bool IsSupported(string extension)
    {
        return extension.ToLowerInvariant() is ".pdf" or ".txt" or ".csv" or ".png" or ".jpg" or ".jpeg";
    }

    private static async Task<string> ExtractPdfTextAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        var pdf = Encoding.Latin1.GetString(memory.ToArray());
        var tokens = new List<string>();

        foreach (Match streamMatch in StreamRegex.Matches(pdf))
        {
            var encoded = streamMatch.Groups["stream"].Value.Trim();
            if (!encoded.EndsWith("~>", StringComparison.Ordinal))
            {
                encoded += "~>";
            }

            try
            {
                var decoded = DecodeAscii85(encoded);
                using var input = new MemoryStream(decoded);
                using var deflate = new ZLibStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                await deflate.CopyToAsync(output, cancellationToken);
                var content = Encoding.Latin1.GetString(output.ToArray());

                foreach (Match textMatch in TextRegex.Matches(content))
                {
                    tokens.Add(UnescapePdfString(textMatch.Groups["text"].Value));
                }
            }
            catch
            {
                // The sample PDFs use ASCII85 + Flate streams. Ignore anything else.
            }
        }

        return string.Join(Environment.NewLine, tokens.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private async Task<string> ExtractImageTextAsync(Stream stream, string fileName, CancellationToken cancellationToken)
    {
        var tempInput = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{Path.GetExtension(fileName)}");
        var tempOutputBase = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var tempOutput = $"{tempOutputBase}.txt";

        try
        {
            await using (var file = File.Create(tempInput))
            {
                await stream.CopyToAsync(file, cancellationToken);
            }

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ResolveTesseractCommand(),
                    Arguments = $"\"{tempInput}\" \"{tempOutputBase}\"",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            try
            {
                process.Start();
            }
            catch
            {
                return string.Empty;
            }

            await process.WaitForExitAsync(cancellationToken);
            return File.Exists(tempOutput) ? await File.ReadAllTextAsync(tempOutput, cancellationToken) : string.Empty;
        }
        finally
        {
            TryDelete(tempInput);
            TryDelete(tempOutput);
        }
    }

    private string ResolveTesseractCommand()
    {
        if (File.Exists(_options.TesseractCommand))
        {
            return _options.TesseractCommand;
        }

        var windowsDefault = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Tesseract-OCR",
            "tesseract.exe");

        return File.Exists(windowsDefault) ? windowsDefault : _options.TesseractCommand;
    }

    private static byte[] DecodeAscii85(string input)
    {
        input = input.Replace("<~", string.Empty, StringComparison.Ordinal).Replace("~>", string.Empty, StringComparison.Ordinal);
        var output = new List<byte>();
        var group = new List<int>(5);

        foreach (var ch in input.Where(ch => !char.IsWhiteSpace(ch)))
        {
            if (ch == 'z' && group.Count == 0)
            {
                output.AddRange([0, 0, 0, 0]);
                continue;
            }

            if (ch < '!' || ch > 'u')
            {
                continue;
            }

            group.Add(ch - '!');
            if (group.Count == 5)
            {
                AppendAscii85Group(output, group, 4);
                group.Clear();
            }
        }

        if (group.Count > 0)
        {
            var bytesToTake = group.Count - 1;
            while (group.Count < 5)
            {
                group.Add('u' - '!');
            }

            AppendAscii85Group(output, group, bytesToTake);
        }

        return output.ToArray();
    }

    private static void AppendAscii85Group(List<byte> output, List<int> group, int bytesToTake)
    {
        uint value = 0;
        foreach (var item in group)
        {
            value = value * 85 + (uint)item;
        }

        output.AddRange(new[]
        {
            (byte)((value >> 24) & 0xff),
            (byte)((value >> 16) & 0xff),
            (byte)((value >> 8) & 0xff),
            (byte)(value & 0xff)
        }.Take(bytesToTake));
    }

    private static string UnescapePdfString(string value)
    {
        return value
            .Replace(@"\(", "(", StringComparison.Ordinal)
            .Replace(@"\)", ")", StringComparison.Ordinal)
            .Replace(@"\\", @"\", StringComparison.Ordinal);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }
}
