using ChatBox.Web.Models.DTOs;

namespace ChatBox.Web.Services;

public interface IChatService
{
    Task<ChatResultDto> ProcessPromptAsync(SendPromptRequest request, CancellationToken cancellationToken = default);
    Task<List<ConversationSummaryDto>> GetConversationsAsync(CancellationToken cancellationToken = default);
    Task<ConversationDetailDto?> GetConversationDetailAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> DeleteConversationAsync(int id, CancellationToken cancellationToken = default);
}
