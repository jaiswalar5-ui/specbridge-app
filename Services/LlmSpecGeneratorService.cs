using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SpecBridge.Models;

namespace SpecBridge.Services;

public sealed class LlmSpecGeneratorService : ISpecGeneratorService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip
    };

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LlmSpecGeneratorService> _logger;

    public LlmSpecGeneratorService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<LlmSpecGeneratorService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<SpecificationDocument> GenerateSpecAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        var provider = _configuration["Llm:Provider"]?.Trim().ToLowerInvariant() ?? "openai";
        var apiKey = _configuration["Llm:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("LLM API key is not configured. Add Llm:ApiKey using user-secrets.");
        }

        var responseBody = provider switch
        {
            "gemini" => await CallGeminiAsync(prompt, apiKey, cancellationToken),
            "openai" => await CallOpenAiAsync(prompt, apiKey, cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported LLM provider '{provider}'.")
        };

        var json = ExtractJson(responseBody, provider);
        try
        {
            return JsonSerializer.Deserialize<SpecificationDocument>(json, JsonOptions)
                ?? throw new JsonException("The LLM returned an empty specification.");
        }
        catch (JsonException exception)
        {
            _logger.LogError(exception, "LLM returned invalid specification JSON: {Response}", json);
            throw new InvalidOperationException("The LLM response did not match the specification schema.", exception);
        }
    }

    private async Task<string> CallOpenAiAsync(string prompt, string apiKey, CancellationToken cancellationToken)
    {
        var endpoint = _configuration["Llm:OpenAiEndpoint"] ?? "https://api.openai.com/v1/chat/completions";
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = JsonContent.Create(new
        {
            model = _configuration["Llm:OpenAiModel"] ?? "gpt-4o-mini",
            temperature = 0.1,
            response_format = new { type = "json_object" },
            messages = new[]
            {
                new { role = "system", content = BuildSystemPrompt() },
                new { role = "user", content = prompt }
            }
        });

        return await SendAsync(request, cancellationToken);
    }

    private async Task<string> CallGeminiAsync(string prompt, string apiKey, CancellationToken cancellationToken)
    {
        var model = _configuration["Llm:GeminiModel"] ?? "gemini-1.5-flash";
        var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(new
            {
                contents = new[] { new { parts = new[] { new { text = $"{BuildSystemPrompt()}\n\n{prompt}" } } } },
                generationConfig = new { temperature = 0.1, responseMimeType = "application/json" }
            })
        };

        return await SendAsync(request, cancellationToken);
    }

    private async Task<string> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("LLM request failed with status {StatusCode}: {Body}", response.StatusCode, body);
            throw new HttpRequestException("The LLM provider rejected the request.", null, response.StatusCode);
        }

        return body;
    }

    private static string ExtractJson(string responseBody, string provider)
    {
        using var document = JsonDocument.Parse(responseBody);
        var root = document.RootElement;
        var content = provider == "gemini"
            ? root.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString()
            : root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new JsonException("The LLM response did not contain content.");
        }

        var start = content.IndexOf('{');
        var end = content.LastIndexOf('}');
        return start >= 0 && end > start ? content[start..(end + 1)] : content;
    }

    private static string BuildSystemPrompt() => """
        You are a senior business analyst. Convert the user's raw requirements into a complete specification.
        Return only valid JSON matching this exact shape: {"title":"","summary":"","functionalRequirements":[{"id":"FR-001","description":"","priority":"Must|Should|Could|Won't","acceptanceCriteria":[""]}],"nonFunctionalRequirements":[{"category":"","requirement":"","target":""}],"userStories":[{"id":"US-001","asA":"","iWant":"","soThat":"","acceptanceCriteria":[""]}],"gaps":[{"area":"","question":"","impact":""}],"risks":[{"description":"","severity":"Low|Medium|High|Critical","mitigation":""}]}
        Infer carefully, label assumptions as gaps, and identify risks. Never wrap the JSON in markdown.
        """;
}