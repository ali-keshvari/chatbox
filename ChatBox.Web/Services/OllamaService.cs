using System.Net.Http.Json;
using System.Text.Json;
using ChatBox.Web.Models.DTOs;

namespace ChatBox.Web.Services;

public class OllamaService : IOllamaService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaService> _logger;
    private readonly string _defaultModel;

    public OllamaService(HttpClient httpClient, IConfiguration configuration, ILogger<OllamaService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _defaultModel = configuration.GetValue<string>("Ollama:DefaultModel") ?? "qwen3.8";

        var baseUrl = configuration.GetValue<string>("Ollama:BaseUrl") ?? "http://localhost:11434";
        _httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    }

    /// <summary>
    /// Reads installed models from Ollama (/api/tags).
    /// NO RETRY POLICY: If Ollama is offline or returns error, fails immediately without any retries.
    /// </summary>
    public async Task<OllamaModelsListResult> GetInstalledModelsAsync(CancellationToken cancellationToken = default)
    {
        var result = new OllamaModelsListResult();

        try
        {
            // Use a short timeout for checking tags so UI loads instantly without hanging
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var response = await _httpClient.GetAsync("api/tags", linkedCts.Token);

            if (!response.IsSuccessStatusCode)
            {
                result.IsAvailable = false;
                result.Message = $"سرویس Ollama وضعیت {response.StatusCode} را بازگرداند.";
                return result;
            }

            var data = await response.Content.ReadFromJsonAsync<OllamaTagsResponse>(cancellationToken: linkedCts.Token);
            if (data?.Models == null || data.Models.Count == 0)
            {
                result.IsAvailable = true;
                result.Message = "هیچ مدلی روی Ollama سیستم نصب نشده است.";
                return result;
            }

            result.IsAvailable = true;
            result.Models = data.Models.Select(m => new OllamaModelUiItem
            {
                Name = m.Name,
                DisplayName = m.Name,
                ParameterSize = m.Details?.ParameterSize,
                SizeFormatted = FormatBytes(m.Size)
            }).ToList();

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning("Ollama is not reachable (NO RETRY): {Message}", ex.Message);
            result.IsAvailable = false;
            result.Message = "ارتباط با سرویس Ollama برقرار نشد (سرویس خاموش یا در دسترس نیست). هیچ تلاش مجددی (Retry) انجام نشد.";
            return result;
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Ollama connection timed out (NO RETRY).");
            result.IsAvailable = false;
            result.Message = "مهلت اتصال به Ollama به پایان رسید. تلاش مجدد غیرفعال است.";
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching Ollama models (NO RETRY).");
            result.IsAvailable = false;
            result.Message = $"خطا در خواندن مدل‌های Ollama: {ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// Sends prompt to Ollama.
    /// NO RETRY POLICY: Strictly calls Ollama once. If model is unavailable or missing, returns immediate error.
    /// </summary>
    public async Task<OllamaChatResponse> SendChatAsync(string model, List<OllamaMessage> messages, CancellationToken cancellationToken = default)
    {
        var targetModel = string.IsNullOrWhiteSpace(model) ? _defaultModel : model;

        var requestBody = new OllamaChatRequest
        {
            Model = targetModel,
            Messages = messages,
            Stream = false
        };

        try
        {
            // Direct single attempt - strictly no retry loop
            var response = await _httpClient.PostAsJsonAsync("api/chat", requestBody, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Ollama returned status {StatusCode}: {ErrorText}", response.StatusCode, errorText);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return new OllamaChatResponse
                    {
                        Done = true,
                        Error = $"مدل '{targetModel}' روی سیستم شما یافت نشد (Model Unavailable). طبق تنظیمات هیچ تلاش مجددی (Retry) انجام نشد. برای نصب مدل می‌توانید دستور 'ollama pull {targetModel}' را اجرا کنید."
                    };
                }

                return new OllamaChatResponse
                {
                    Done = true,
                    Error = $"خطای سرویس Ollama ({response.StatusCode}): {errorText}"
                };
            }

            var chatResponse = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(cancellationToken: cancellationToken);
            return chatResponse ?? new OllamaChatResponse
            {
                Done = true,
                Error = "پاسخ معتبری از Ollama دریافت نشد."
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError("Ollama HTTP Error (NO RETRY): {Message}", ex.Message);
            return new OllamaChatResponse
            {
                Done = true,
                Error = $"امکان اتصال به Ollama در آدرس {_httpClient.BaseAddress} وجود ندارد. لطفاً اطمینان حاصل کنید که Ollama در حال اجراست. (بدون تلاش مجدد)"
            };
        }
        catch (TaskCanceledException)
        {
            return new OllamaChatResponse
            {
                Done = true,
                Error = "زمان انتظار برای پاسخ مدل Ollama به پایان رسید (Timeout)."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in SendChatAsync (NO RETRY)");
            return new OllamaChatResponse
            {
                Done = true,
                Error = $"خطای پیش‌بینی نشده: {ex.Message}"
            };
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        return $"{number:n1} {suffixes[counter]}";
    }
}
