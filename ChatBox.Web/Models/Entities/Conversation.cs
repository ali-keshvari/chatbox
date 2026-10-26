using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChatBox.Web.Models.Entities;

[Table("Conversations")]
public class Conversation
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(250)]
    public string Title { get; set; } = "گفتگوی جدید";

    [MaxLength(100)]
    public string? ModelName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
