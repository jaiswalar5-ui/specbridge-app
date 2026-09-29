using System.Net.Http.Headers;
using System.Text.Json;
using SpecBridge.Models;

namespace SpecBridge.Services;

public sealed class AiService : IAiService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AiService> _logger;

    public AiService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<AiService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<SpecResponse> GenerateSpecAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        if (_configuration.GetValue<bool>("UseDemoMode"))
        {
            return new SpecResponse(
                Title: "Demo Specification",
                Summary: "This is a mock specification generated in Demo Mode.",
                FunctionalRequirements: new[] { new FunctionalRequirement("FR-001", "The system shall work.", "Must", new[] { "It works." }) },
                NonFunctionalRequirements: new[] { new NonFunctionalRequirement("Performance", "Fast", "100ms") },
                UserStories: new[] { new UserStory("US-001", "User", "use the system", "I can do things.", new[] { "System is used." }) },
                ClarifyingQuestions: new[] { new ClarifyingQuestion("General", "Is this real?", "None") },
                Risks: new[] { new Risk("Fake Data", "Low", "Disable Demo Mode.") }
            );
        }

        var provider = _configuration["Llm:Provider"]?.Trim().ToLowerInvariant() ?? "openai";
        var apiKey = _configuration["Llm:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogError("LLM API key is not configured.");
            return CreateErrorResponse("Configuration Error", "LLM API key is not configured.");
        }

        string responseBody;
        try
        {
            responseBody = provider switch
            {
                "gemini" => await CallGeminiAsync(prompt, apiKey, cancellationToken),
                "openai" => await CallOpenAiAsync(prompt, apiKey, cancellationToken),
                _ => throw new InvalidOperationException($"Unsupported LLM provider '{provider}'.")
            };
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "LLM provider request failed for provider {Provider}.", provider);
            return CreateErrorResponse("API Error", "The LLM provider is currently unavailable or returned an error.");
        }

        string json;
        try
        {
            json = ExtractJson(responseBody, provider);
        }
        catch (Exception exception) when (exception is JsonException
            or InvalidOperationException
            or KeyNotFoundException
            or IndexOutOfRangeException)
        {
            _logger.LogError(exception, "Failed to extract JSON from LLM response.");
            return CreateErrorResponse("Parsing Error", "The LLM returned a response that did not contain valid JSON.");
        }

        try
        {
            return JsonSerializer.Deserialize(json, SpecContext.Default.SpecResponse)
                ?? throw new JsonException("The LLM returned an empty specification.");
        }
        catch (JsonException exception)
        {
            _logger.LogError(exception, "LLM returned invalid specification JSON.");
            return CreateErrorResponse("Validation Error", "The LLM response could not be parsed into the required specification schema.");
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
        // Use x-goog-api-key header to avoid logging the API key in the URL
        var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(new
            {
                contents = new[] { new { parts = new[] { new { text = $"{BuildSystemPrompt()}\n\n{prompt}" } } } },
                generationConfig = new { temperature = 0.1, responseMimeType = "application/json" }
            })
        };
        request.Headers.Add("x-goog-api-key", apiKey);

        return await SendAsync(request, cancellationToken);
    }

    private async Task<string> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var client = _httpClientFactory.CreateClient("AiServiceClient");
        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("LLM request failed with status {StatusCode} and response length {ResponseLength}.",
                response.StatusCode,
                body.Length);
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
        Return only valid JSON matching this exact shape:
        {
          "title": "",
          "summary": "",
          "functionalRequirements": [{"id": "FR-001", "description": "", "priority": "Must|Should|Could|Won't", "acceptanceCriteria": [""]}],
          "nonFunctionalRequirements": [{"category": "", "requirement": "", "target": ""}],
          "userStories": [{"id": "US-001", "asA": "", "iWant": "", "soThat": "", "acceptanceCriteria": [""]}],
          "clarifyingQuestions": [{"area": "", "question": "", "impact": ""}],
          "risks": [{"description": "", "severity": "Low|Medium|High|Critical", "mitigation": ""}]
        }
        Infer carefully, label assumptions as clarifyingQuestions, and identify risks. Never wrap the JSON in markdown.
        If the raw input contains PII, passwords, or sensitive data, redact it immediately and note the redaction in the risks section.
        """;

    private static SpecResponse CreateErrorResponse(string title, string message)
    {
        return new SpecResponse(
            Title: title,
            Summary: message,
            FunctionalRequirements: Array.Empty<FunctionalRequirement>(),
            NonFunctionalRequirements: Array.Empty<NonFunctionalRequirement>(),
            UserStories: Array.Empty<UserStory>(),
            ClarifyingQuestions: Array.Empty<ClarifyingQuestion>(),
            Risks: Array.Empty<Risk>()
        );
    }
}
