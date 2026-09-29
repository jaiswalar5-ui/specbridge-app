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
                Title: "Helpdesk Ticketing System",
                Summary: "An internal IT helpdesk system allowing employees to raise support tickets, technicians to manage them, and managers to oversee open ticket volume.",
                FunctionalRequirements: new[] { 
                    new FunctionalRequirement("FR-001", "Ticket Creation", "Must", new[] { "Employees can submit a new ticket with title, description, and urgency." }),
                    new FunctionalRequirement("FR-002", "Ticket Assignment", "Must", new[] { "System auto-assigns tickets to the technician queue.", "Technicians can claim tickets." }),
                    new FunctionalRequirement("FR-003", "Ticket Updates", "Must", new[] { "Technicians can update ticket status and add internal notes." }),
                    new FunctionalRequirement("FR-004", "Manager Dashboard", "Should", new[] { "Managers can view a dashboard showing open vs resolved tickets." })
                },
                NonFunctionalRequirements: new[] { 
                    new NonFunctionalRequirement("Performance", "Page Load Time", "Dashboard must load within 2 seconds under normal load."),
                    new NonFunctionalRequirement("Security", "Authentication", "Must integrate with company SSO (Single Sign-On).")
                },
                UserStories: new[] { 
                    new UserStory("US-001", "Employee", "raise an IT support ticket", "I can get my technical issues resolved quickly.", new[] { "Ticket form is accessible from intranet.", "Receives confirmation email." }),
                    new UserStory("US-002", "Technician", "respond to open tickets", "I can resolve employee issues effectively.", new[] { "Can filter by open status.", "Can change status to 'Resolved'." }),
                    new UserStory("US-003", "Manager", "see the number of open tickets", "I can balance technician workloads and monitor SLA.", new[] { "Dashboard shows total open count.", "Can see metrics by day." })
                },
                ClarifyingQuestions: new[] { 
                    new ClarifyingQuestion("Integrations", "Do tickets need to sync with Jira or ServiceNow?", "Could expand project scope significantly if required."),
                    new ClarifyingQuestion("SLA", "What are the SLA definitions for ticket resolution?", "Needed for manager dashboard metrics."),
                    new ClarifyingQuestion("Notifications", "Should technicians be notified via email or Slack when a ticket is created?", "Impacts the notification service design.")
                },
                Risks: new[] { 
                    new Risk("SSO Integration Complexity", "Medium", "Start SSO integration spike early in the sprint to mitigate technical blockers."),
                    new Risk("Scope Creep", "Low", "Strictly limit v1 to basic ticket flow, deferring SLA alerting to v2.")
                }
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
        var model = _configuration["Llm:GeminiModel"] ?? "gemini-2.0-flash";
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
