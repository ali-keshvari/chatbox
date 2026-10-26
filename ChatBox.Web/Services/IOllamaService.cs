using ChatBox.Web.Models.DTOs;

namespace ChatBox.Web.Services;

public interface IOllamaService
{
    /// <summary>
    /// Fetches all available models from local Ollama (/api/tags).
    /// STRICT RULE: NO RETRY if service is down, unreachable, or returns no models.
    /// </summary>
    Task<OllamaModelsListResult> GetInstalledModelsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a prompt and messages to Ollama model.
    /// STRICT RULE: NO RETRY if model is unavailable, missing, or returns 404/500/timeout.
    /// Returns immediate error result without retrying.
    /// </summary>
    Task<OllamaChatResponse> SendChatAsync(string model, List<OllamaMessage> messages, CancellationToken cancellationToken = default);
}
