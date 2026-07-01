using System.Text;
using System.Text.Json;
using System.Net;

namespace Pl1MigrationDemo.Services;

public class OllamaLlmClient : ILlmClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _model;

    public OllamaLlmClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _baseUrl = (configuration["Ollama:BaseUrl"] ?? Environment.GetEnvironmentVariable("OLLAMA_BASE_URL") ?? "http://localhost:11434").TrimEnd('/');
        _model = configuration["Ollama:Model"] ?? Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "llama3.2";
    }

    public bool IsConfigured => true;
    public string ProviderName => $"Ollama ({_model})";

    public async Task<string> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken)
    {
        var payload = new
        {
            model = _model,
            system = systemPrompt,
            prompt = userPrompt,
            stream = false,
            format = "json",
            options = new
            {
                temperature = 0.2,
                num_predict = 8192,
                num_ctx = 8192
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/api/generate");
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        string body;

        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
            body = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Could not connect to Ollama at {_baseUrl}. Start Ollama and run: ollama pull {_model}", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.NotFound
                && body.Contains("model", StringComparison.OrdinalIgnoreCase)
                && body.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Ollama model '{_model}' is not installed. Run 'ollama pull {_model}' and retry, or set OLLAMA_MODEL to a model returned by 'ollama list'.");
            }

            throw new InvalidOperationException($"Ollama request failed: {(int)response.StatusCode} {response.ReasonPhrase}. {body}");
        }

        return ExtractOutputText(body);
    }

    private static string ExtractOutputText(string responseBody)
    {
        using var document = JsonDocument.Parse(responseBody);

        if (document.RootElement.TryGetProperty("response", out var response))
        {
            return response.GetString() ?? "";
        }

        return responseBody;
    }
}
