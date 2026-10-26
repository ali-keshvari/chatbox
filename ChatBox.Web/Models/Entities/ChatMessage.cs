using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChatBox.Web.Models.Entities;

[Table("ChatMessages")]
public class ChatMessage
{
    [Key]
    public int Id { get; set; }

    public int ConversationId { get; set; }

    [ForeignKey(nameof(ConversationId))]
    public virtual Conversation Conversation { get; set; } = null!;

    [Required]
    [MaxLength(30)]
    public string Role { get; set; } = "user"; // "user", "assistant", "system"

    [Required]
    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<FileAttachment> Attachments { get; set; } = new List<FileAttachment>();
}
