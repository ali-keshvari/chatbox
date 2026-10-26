using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;

namespace ChatBox.Web.Services;

public class FileExtractorService : IFileExtractorService
{
    private readonly ILogger<FileExtractorService> _logger;

    public FileExtractorService(ILogger<FileExtractorService> logger)
    {
        _logger = logger;
    }

    public async Task<ExtractedFileResult> ExtractTextAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        var result = new ExtractedFileResult
        {
            FileName = file.FileName,
            FileExtension = Path.GetExtension(file.FileName).ToLowerInvariant(),
            ContentType = file.ContentType,
            FileSizeBytes = file.Length
        };

        if (file.Length == 0)
        {
            result.ExtractedText = string.Empty;
            return result;
        }

        try
        {
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;

            switch (result.FileExtension)
            {
                case ".pdf":
                    result.ExtractedText = ExtractFromPdf(memoryStream);
                    break;

                case ".docx":
                    result.ExtractedText = ExtractFromDocx(memoryStream);
                    break;

                case ".doc":
                    result.ExtractedText = ExtractFromLegacyDoc(memoryStream);
                    break;

                case ".txt":
                case ".text":
                case ".md":
                case ".markdown":
                case ".json":
                case ".csv":
                case ".xml":
                case ".log":
                case ".yaml":
                case ".yml":
                case ".cs":
                case ".js":
                case ".ts":
                case ".html":
                case ".css":
                case ".sql":
                    result.ExtractedText = await ExtractFromPlainTextAsync(memoryStream, cancellationToken);
                    break;

                default:
                    // Try plain text for other file types
                    try
                    {
                        result.ExtractedText = await ExtractFromPlainTextAsync(memoryStream, cancellationToken);
                    }
                    catch
                    {
                        result.ExtractedText = $"[فایل باینری یا پشتیبانی‌نشده: {file.FileName}]";
                    }
                    break;
            }

            result.Success = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در استخراج متن از فایل {FileName}", file.FileName);
            result.Success = false;
            result.ErrorMessage = $"خطا در استخراج متن: {ex.Message}";
            result.ExtractedText = $"[خطا در پردازش فایل {file.FileName}: {ex.Message}]";
        }

        return result;
    }

    public async Task<List<ExtractedFileResult>> ExtractAllAsync(IEnumerable<IFormFile> files, CancellationToken cancellationToken = default)
    {
        var results = new List<ExtractedFileResult>();
        foreach (var file in files)
        {
            var res = await ExtractTextAsync(file, cancellationToken);
            results.Add(res);
        }
        return results;
    }

    public string FormatFileSize(long bytes)
    {
        string[] suffixes = { "بایت", "کیلوبایت", "مگابایت", "گیگابایت" };
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        return $"{number:n1} {suffixes[counter]}";
    }

    private string ExtractFromPdf(MemoryStream stream)
    {
        var sb = new StringBuilder();
        using var document = PdfDocument.Open(stream);

        foreach (var page in document.GetPages())
        {
            var pageText = page.Text;
            if (!string.IsNullOrWhiteSpace(pageText))
            {
                sb.AppendLine($"--- [صفحه {page.Number}] ---");
                sb.AppendLine(pageText.Trim());
                sb.AppendLine();
            }
        }

        var text = sb.ToString().Trim();
        return string.IsNullOrEmpty(text) ? "[فایل PDF حاوی متن قابل استخراج نیست (احتمالاً اسکن تصویر است)]" : text;
    }

    private string ExtractFromDocx(MemoryStream stream)
    {
        var sb = new StringBuilder();
        using var doc = WordprocessingDocument.Open(stream, false);

        var body = doc.MainDocumentPart?.Document?.Body;
        if (body == null)
            return string.Empty;

        // Extract paragraphs
        foreach (var element in body.Elements())
        {
            if (element is Paragraph paragraph)
            {
                var text = paragraph.InnerText?.Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    sb.AppendLine(text);
                }
            }
            else if (element is Table table)
            {
                sb.AppendLine();
                foreach (var row in table.Descendants<TableRow>())
                {
                    var cells = row.Descendants<TableCell>()
                                   .Select(c => c.InnerText?.Trim() ?? string.Empty)
                                   .Where(c => !string.IsNullOrEmpty(c));
                    var rowLine = string.Join(" | ", cells);
                    if (!string.IsNullOrEmpty(rowLine))
                    {
                        sb.AppendLine($"| {rowLine} |");
                    }
                }
                sb.AppendLine();
            }
        }

        return sb.ToString().Trim();
    }

    private string ExtractFromLegacyDoc(MemoryStream stream)
    {
        // Try opening as docx first (many .doc files are actually OpenXml)
        try
        {
            stream.Position = 0;
            return ExtractFromDocx(stream);
        }
        catch
        {
            // Fallback for binary .doc: extract ASCII and UTF-16 strings
            stream.Position = 0;
            var bytes = stream.ToArray();
            var sb = new StringBuilder();
            
            // Extract printable strings of length >= 4
            var currentWord = new StringBuilder();
            foreach (var b in bytes)
            {
                // Basic printable ASCII and Persian/Arabic characters range
                if ((b >= 32 && b <= 126) || b == 10 || b == 13)
                {
                    currentWord.Append((char)b);
                }
                else
                {
                    if (currentWord.Length >= 4)
                    {
                        sb.AppendLine(currentWord.ToString());
                    }
                    currentWord.Clear();
                }
            }
            if (currentWord.Length >= 4)
            {
                sb.AppendLine(currentWord.ToString());
            }

            var text = sb.ToString().Trim();
            return string.IsNullOrEmpty(text) ? "[متن قابل خواندن در فایل .doc باینری یافت نشد]" : text;
        }
    }

    private async Task<string> ExtractFromPlainTextAsync(MemoryStream stream, CancellationToken cancellationToken)
    {
        stream.Position = 0;
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }
}
