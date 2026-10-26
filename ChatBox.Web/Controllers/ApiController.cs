using Microsoft.AspNetCore.Mvc;
using ChatBox.Web.Models.DTOs;
using ChatBox.Web.Services;

namespace ChatBox.Web.Controllers;

[ApiController]
[Route("api")]
public class ApiController : ControllerBase
{
    private readonly IOllamaService _ollamaService;
    private readonly IChatService _chatService;
    private readonly ILogger<ApiController> _logger;

    public ApiController(
        IOllamaService ollamaService,
        IChatService chatService,
        ILogger<ApiController> logger)
    {
        _ollamaService = ollamaService;
        _chatService = chatService;
        _logger = logger;
    }

    /// <summary>
    /// Returns the list of installed models from local Ollama.
    /// STRICT RULE: NO RETRY if Ollama or models are unavailable.
    /// </summary>
    [HttpGet("models")]
    public async Task<IActionResult> GetModels(CancellationToken cancellationToken)
    {
        var result = await _ollamaService.GetInstalledModelsAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Processes a user prompt and optional attached documents (PDF, Word, Text, etc.).
    /// Sends context to Ollama (STRICT NO-RETRY on model unavailable) and stores result in SSMS.
    /// </summary>
    [HttpPost("chat/send")]
    [RequestSizeLimit(100_000_000)] // 100 MB max request size
    public async Task<IActionResult> SendMessage([FromForm] SendPromptRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt) && (request.Files == null || request.Files.Count == 0))
        {
            return BadRequest(new { message = "لطفاً یک دستور متنی وارد کنید یا حداقل یک فایل پیوست نمایید." });
        }

        try
        {
            var result = await _chatService.ProcessPromptAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در پردازش چت");
            return StatusCode(500, new { message = $"خطای سرور: {ex.Message}" });
        }
    }

    /// <summary>
    /// Retrieves all saved chat sessions from SSMS.
    /// </summary>
    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations(CancellationToken cancellationToken)
    {
        var list = await _chatService.GetConversationsAsync(cancellationToken);
        return Ok(list);
    }

    /// <summary>
    /// Retrieves chat history and attached files for a given session.
    /// </summary>
    [HttpGet("conversations/{id:int}")]
    public async Task<IActionResult> GetConversation(int id, CancellationToken cancellationToken)
    {
        var conversation = await _chatService.GetConversationDetailAsync(id, cancellationToken);
        if (conversation == null)
            return NotFound(new { message = "گفتگوی مورد نظر یافت نشد." });

        return Ok(conversation);
    }

    /// <summary>
    /// Deletes a conversation and all its messages/files from SSMS.
    /// </summary>
    [HttpDelete("conversations/{id:int}")]
    public async Task<IActionResult> DeleteConversation(int id, CancellationToken cancellationToken)
    {
        var success = await _chatService.DeleteConversationAsync(id, cancellationToken);
        if (!success)
            return NotFound(new { message = "گفتگوی مورد نظر یافت نشد." });

        return Ok(new { success = true, message = "گفتگو با موفقیت حذف شد." });
    }
}
