namespace ChatBox.Web.Models.DTOs;

public class SendPromptRequest
{
    public int? ConversationId { get; set; }
    public string? Model { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public List<IFormFile>? Files { get; set; }
}

public class ChatResultDto
{
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public int ConversationId { get; set; }
    public string ConversationTitle { get; set; } = string.Empty;
    public int MessageId { get; set; }
    public string Role { get; set; } = "assistant";
    public string Content { get; set; } = string.Empty;
    public string ModelUsed { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<UploadedFileSummaryDto> ProcessedFiles { get; set; } = new();
}

public class UploadedFileSummaryDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string FileSizeFormatted { get; set; } = string.Empty;
    public int CharacterCount { get; set; }
}

public class ConversationSummaryDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ModelName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int MessageCount { get; set; }
}

public class ConversationDetailDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ModelName { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<ChatMessageDto> Messages { get; set; } = new();
}

public class ChatMessageDto
{
    public int Id { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<UploadedFileSummaryDto> Attachments { get; set; } = new();
}
