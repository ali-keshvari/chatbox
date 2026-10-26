using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChatBox.Web.Models.Entities;

[Table("FileAttachments")]
public class FileAttachment
{
    [Key]
    public int Id { get; set; }

    public int ChatMessageId { get; set; }

    [ForeignKey(nameof(ChatMessageId))]
    public virtual ChatMessage ChatMessage { get; set; } = null!;

    [Required]
    [MaxLength(260)]
    public string FileName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    [MaxLength(20)]
    public string FileExtension { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    /// <summary>
    /// Plain text extracted from PDF, Word (.docx/.doc), Text, etc.
    /// Used as context for Ollama.
    /// </summary>
    public string ExtractedText { get; set; } = string.Empty;

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
