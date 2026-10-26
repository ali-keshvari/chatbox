namespace ChatBox.Web.Services;

public class ExtractedFileResult
{
    public string FileName { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string ExtractedText { get; set; } = string.Empty;
    public int CharacterCount => ExtractedText.Length;
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
}

public interface IFileExtractorService
{
    Task<ExtractedFileResult> ExtractTextAsync(IFormFile file, CancellationToken cancellationToken = default);
    Task<List<ExtractedFileResult>> ExtractAllAsync(IEnumerable<IFormFile> files, CancellationToken cancellationToken = default);
    string FormatFileSize(long bytes);
}
