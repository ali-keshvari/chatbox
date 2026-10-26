using System.Text;
using Microsoft.EntityFrameworkCore;
using ChatBox.Web.Data;
using ChatBox.Web.Models.DTOs;
using ChatBox.Web.Models.Entities;

namespace ChatBox.Web.Services;

public class ChatService : IChatService
{
    private readonly ChatBoxDbContext _dbContext;
    private readonly IOllamaService _ollamaService;
    private readonly IFileExtractorService _fileExtractorService;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        ChatBoxDbContext dbContext,
        IOllamaService ollamaService,
        IFileExtractorService fileExtractorService,
        ILogger<ChatService> logger)
    {
        _dbContext = dbContext;
        _ollamaService = ollamaService;
        _fileExtractorService = fileExtractorService;
        _logger = logger;
    }

    public async Task<ChatResultDto> ProcessPromptAsync(SendPromptRequest request, CancellationToken cancellationToken = default)
    {
        var modelUsed = string.IsNullOrWhiteSpace(request.Model) ? "qwen3.8" : request.Model.Trim();
        var userPrompt = request.Prompt?.Trim() ?? string.Empty;

        // 1. Extract content from uploaded files (PDF, Word, Text, etc.)
        var extractedFiles = new List<ExtractedFileResult>();
        if (request.Files != null && request.Files.Count > 0)
        {
            extractedFiles = await _fileExtractorService.ExtractAllAsync(request.Files, cancellationToken);
        }

        // 2. Manage Conversation (Find existing or create new)
        Conversation? conversation = null;
        if (request.ConversationId.HasValue && request.ConversationId.Value > 0)
        {
            conversation = await _dbContext.Conversations
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c => c.Id == request.ConversationId.Value, cancellationToken);
        }

        if (conversation == null)
        {
            var title = userPrompt.Length > 60 ? userPrompt[..60] + "..." : userPrompt;
            if (string.IsNullOrWhiteSpace(title) && extractedFiles.Count > 0)
            {
                title = $"تحلیل {extractedFiles.Count} سند: {extractedFiles[0].FileName}";
            }
            if (string.IsNullOrWhiteSpace(title))
            {
                title = "گفتگوی جدید";
            }

            conversation = new Conversation
            {
                Title = title,
                ModelName = modelUsed,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _dbContext.Conversations.Add(conversation);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            conversation.UpdatedAt = DateTime.UtcNow;
            conversation.ModelName = modelUsed;
        }

        // 3. Save User Message and File Attachments in SSMS via EF Core
        var userMessage = new ChatMessage
        {
            ConversationId = conversation.Id,
            Role = "user",
            Content = userPrompt,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.ChatMessages.Add(userMessage);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var processedFilesDto = new List<UploadedFileSummaryDto>();
        foreach (var fileRes in extractedFiles)
        {
            var attachment = new FileAttachment
            {
                ChatMessageId = userMessage.Id,
                FileName = fileRes.FileName,
                ContentType = fileRes.ContentType,
                FileExtension = fileRes.FileExtension,
                FileSizeBytes = fileRes.FileSizeBytes,
                ExtractedText = fileRes.ExtractedText,
                UploadedAt = DateTime.UtcNow
            };
            _dbContext.FileAttachments.Add(attachment);

            processedFilesDto.Add(new UploadedFileSummaryDto
            {
                Id = 0, // will be generated
                FileName = fileRes.FileName,
                FileExtension = fileRes.FileExtension,
                FileSizeBytes = fileRes.FileSizeBytes,
                FileSizeFormatted = _fileExtractorService.FormatFileSize(fileRes.FileSizeBytes),
                CharacterCount = fileRes.CharacterCount
            });
        }
        await _dbContext.SaveChangesAsync(cancellationToken);

        // 4. Construct Prompt Context for Ollama
        var ollamaMessages = new List<OllamaMessage>();

        // System prompt guiding Ollama for multi-document comparison / analysis
        ollamaMessages.Add(new OllamaMessage
        {
            Role = "system",
            Content = "شما یک دستیار هوش مصنوعی تحلیل‌گر و دقیق اسناد و متون هستید. به زبان فارسی روان، ساختاریافته و با قالب‌بندی مرتب مارک‌داون پاسخ دهید. اگر چندین فایل ارسال شده، محتوا و تفاوت‌های آن‌ها را با دقت مقایسه کنید و هر سوالی پیرامون جزئیات اسناد (مانند امضاکننده، تاریخ، ارقام و تعهدات) مطرح شده بود، بر اساس متن صریح اسناد پاسخ دهید."
        });

        // Add previous message history if continuing conversation
        var pastMessages = await _dbContext.ChatMessages
            .Where(m => m.ConversationId == conversation.Id && m.Id != userMessage.Id)
            .OrderBy(m => m.CreatedAt)
            .Take(10) // keep last 10 messages for context
            .ToListAsync(cancellationToken);

        foreach (var past in pastMessages)
        {
            ollamaMessages.Add(new OllamaMessage
            {
                Role = past.Role,
                Content = past.Content
            });
        }

        // Build composite prompt combining file contents and the user question
        var userMessageContentBuilder = new StringBuilder();
        if (extractedFiles.Count > 0)
        {
            userMessageContentBuilder.AppendLine("اطلاعات فایل‌های بارگذاری شده توسط کاربر به شرح زیر است:");
            userMessageContentBuilder.AppendLine();

            for (int i = 0; i < extractedFiles.Count; i++)
            {
                var f = extractedFiles[i];
                userMessageContentBuilder.AppendLine($"══════════════════════════════════════════════════");
                userMessageContentBuilder.AppendLine($"📄 فایل {i + 1}: {f.FileName} (نوع: {f.FileExtension} | حجم: {_fileExtractorService.FormatFileSize(f.FileSizeBytes)})");
                userMessageContentBuilder.AppendLine($"══════════════════════════════════════════════════");
                userMessageContentBuilder.AppendLine(f.ExtractedText);
                userMessageContentBuilder.AppendLine();
            }

            userMessageContentBuilder.AppendLine("──────────────────────────────────────────────────");
            userMessageContentBuilder.AppendLine("دستور / سوال کاربر در مورد فایل‌های فوق:");
            userMessageContentBuilder.AppendLine(string.IsNullOrWhiteSpace(userPrompt) ? "لطفاً این اسناد را تحلیل، مقایسه و خلاصه کنید." : userPrompt);
        }
        else
        {
            userMessageContentBuilder.Append(userPrompt);
        }

        ollamaMessages.Add(new OllamaMessage
        {
            Role = "user",
            Content = userMessageContentBuilder.ToString()
        });

        // 5. Call Ollama (STRICT NO-RETRY on model unavailable)
        var ollamaResponse = await _ollamaService.SendChatAsync(modelUsed, ollamaMessages, cancellationToken);

        string assistantReply;
        bool isSuccess = true;
        string? errorMessage = null;

        if (!string.IsNullOrEmpty(ollamaResponse.Error))
        {
            isSuccess = false;
            errorMessage = ollamaResponse.Error;
            assistantReply = $"⚠️ **خطا در اجرای مدل Ollama:**\n\n{ollamaResponse.Error}";
        }
        else
        {
            assistantReply = ollamaResponse.Message?.Content?.Trim() ?? "پاسخی از مدل دریافت نشد.";
        }

        // 6. Save Assistant Response in SSMS via EF Core
        var assistantMessage = new ChatMessage
        {
            ConversationId = conversation.Id,
            Role = "assistant",
            Content = assistantReply,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.ChatMessages.Add(assistantMessage);

        conversation.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ChatResultDto
        {
            Success = isSuccess,
            ErrorMessage = errorMessage,
            ConversationId = conversation.Id,
            ConversationTitle = conversation.Title,
            MessageId = assistantMessage.Id,
            Role = "assistant",
            Content = assistantReply,
            ModelUsed = modelUsed,
            CreatedAt = assistantMessage.CreatedAt,
            ProcessedFiles = processedFilesDto
        };
    }

    public async Task<List<ConversationSummaryDto>> GetConversationsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Conversations
            .AsNoTracking()
            .OrderByDescending(c => c.UpdatedAt)
            .Select(c => new ConversationSummaryDto
            {
                Id = c.Id,
                Title = c.Title,
                ModelName = c.ModelName,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
                MessageCount = c.Messages.Count
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<ConversationDetailDto?> GetConversationDetailAsync(int id, CancellationToken cancellationToken = default)
    {
        var conversation = await _dbContext.Conversations
            .AsNoTracking()
            .Include(c => c.Messages)
                .ThenInclude(m => m.Attachments)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (conversation == null)
            return null;

        return new ConversationDetailDto
        {
            Id = conversation.Id,
            Title = conversation.Title,
            ModelName = conversation.ModelName,
            CreatedAt = conversation.CreatedAt,
            Messages = conversation.Messages
                .OrderBy(m => m.CreatedAt)
                .Select(m => new ChatMessageDto
                {
                    Id = m.Id,
                    Role = m.Role,
                    Content = m.Content,
                    CreatedAt = m.CreatedAt,
                    Attachments = m.Attachments.Select(a => new UploadedFileSummaryDto
                    {
                        Id = a.Id,
                        FileName = a.FileName,
                        FileExtension = a.FileExtension,
                        FileSizeBytes = a.FileSizeBytes,
                        FileSizeFormatted = _fileExtractorService.FormatFileSize(a.FileSizeBytes),
                        CharacterCount = a.ExtractedText.Length
                    }).ToList()
                }).ToList()
        };
    }

    public async Task<bool> DeleteConversationAsync(int id, CancellationToken cancellationToken = default)
    {
        var conversation = await _dbContext.Conversations.FindAsync(new object[] { id }, cancellationToken);
        if (conversation == null)
            return false;

        _dbContext.Conversations.Remove(conversation);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
