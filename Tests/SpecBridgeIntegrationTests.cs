using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SpecBridge.Models;
using SpecBridge.Services;
using Xunit;

namespace SpecBridge.Tests;

public sealed class SpecBridgeIntegrationTests : IClassFixture<TestApplicationFactory>
{
    private readonly TestApplicationFactory _factory;
    private readonly HttpClient _client;

    public SpecBridgeIntegrationTests(TestApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HomePage_ContainsSecurityHeaders()
    {
        var response = await _client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("Content-Security-Policy", response.Headers.Select(header => header.Key));
    }

    [Fact]
    public async Task GenerateSpec_RejectsTheEleventhRequest()
    {
        var responses = new List<HttpResponseMessage>();
        for (var index = 0; index < 11; index++)
        {
            using var content = JsonContent.Create(new SpecRequest { Prompt = "Build a leave management workflow." });
            responses.Add(await _client.PostAsync("/api/generate-spec", content));
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, responses[^1].StatusCode);
    }

    [Fact]
    public void SpecificationContract_RoundTripsStructuredSections()
    {
        var document = new SpecResponse(
            Title: "Leave management",
            Summary: "",
            FunctionalRequirements: [new FunctionalRequirement("FR-001", "Submit leave", "Must", [])],
            NonFunctionalRequirements: [],
            UserStories: [],
            ClarifyingQuestions: [new ClarifyingQuestion("Notifications", "Which channels?", "")],
            Risks: []
        );

        var json = JsonSerializer.Serialize(document, SpecContext.Default.SpecResponse);
        var parsed = JsonSerializer.Deserialize(json, SpecContext.Default.SpecResponse);

        Assert.Equal("FR-001", parsed!.FunctionalRequirements[0].Id);
        Assert.Equal("Which channels?", parsed.ClarifyingQuestions[0].Question);
    }

    [Fact]
    public async Task ExportPdf_ReturnsAnAttachmentContainingPdfBytes()
    {
        var specification = new SpecResponse(
            Title: "Leave management",
            Summary: "A workflow for requesting leave.",
            FunctionalRequirements: [new FunctionalRequirement("FR-001", "Submit leave", "Must", ["Show confirmation."])],
            NonFunctionalRequirements: [new NonFunctionalRequirement("Performance", "Respond quickly", "Under two seconds")],
            UserStories: [new UserStory("US-001", "employee", "request leave", "I can plan time off", ["Request is recorded."])],
            ClarifyingQuestions: [new ClarifyingQuestion("Notifications", "Which channels?", "Affects delivery design.")],
            Risks: [new Risk("Requests may be missed.", "High", "Send a confirmation.")]
        );

        using var content = JsonContent.Create(specification);
        using var response = await _client.PostAsync("/api/export-pdf", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Contains("specbridge-specification.pdf", response.Content.Headers.ContentDisposition?.FileName);

        var pdfBytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(pdfBytes.Length > 5);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(pdfBytes, 0, 5));
    }

    [Fact]
    public async Task AiService_ReturnsParsingErrorForMalformedProviderEnvelope()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Llm:Provider"] = "openai",
                ["Llm:ApiKey"] = "test-key"
            })
            .Build();
        var service = new AiService(
            new StubHttpClientFactory("{}"),
            configuration,
            NullLogger<AiService>.Instance);

        var response = await service.GenerateSpecAsync("Create a leave workflow.");

        Assert.Equal("Parsing Error", response.Title);
    }

    [Fact]
    public async Task AiService_DemoModeReturnsMockDataWithoutCallingProvider()
    {
        var configuration = _factory.Services.GetRequiredService<IConfiguration>();
        var httpClientFactory = new StubHttpClientFactory("{}");
        var service = new AiService(
            httpClientFactory,
            configuration,
            NullLogger<AiService>.Instance);

        Assert.True(configuration.GetValue<bool>("UseDemoMode"));

        var response = await service.GenerateSpecAsync("Create a leave workflow.");

        Assert.Equal("Helpdesk Ticketing System", response.Title);
        Assert.StartsWith("An internal IT helpdesk system", response.Summary);
        Assert.Equal(0, httpClientFactory.RequestCount);
    }

    [Fact]
    public async Task AiService_DisabledDemoModeCallsConfiguredProvider()
    {
        const string endpoint = "https://openai.test/v1/chat/completions";
        const string providerResponse = """
            {"choices":[{"message":{"content":"{\"title\":\"Provider Specification\",\"summary\":\"From provider.\",\"functionalRequirements\":[],\"nonFunctionalRequirements\":[],\"userStories\":[],\"clarifyingQuestions\":[],\"risks\":[]}"}}]}
            """;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UseDemoMode"] = "false",
                ["Llm:Provider"] = "openai",
                ["Llm:ApiKey"] = "test-key",
                ["Llm:OpenAiEndpoint"] = endpoint
            })
            .Build();
        var httpClientFactory = new StubHttpClientFactory(providerResponse);
        var service = new AiService(
            httpClientFactory,
            configuration,
            NullLogger<AiService>.Instance);

        var response = await service.GenerateSpecAsync("Create a leave workflow.");

        Assert.Equal("Provider Specification", response.Title);
        Assert.Equal(1, httpClientFactory.RequestCount);
    }

    [Fact]
    public async Task FakeCrash_InProductionShowsFriendlyErrorWithoutStackTrace()
    {
        using var productionClient = _factory
            .WithWebHostBuilder(builder => builder.UseEnvironment("Production"))
            .CreateClient();

        using var response = await productionClient.GetAsync("/crash");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("An error occurred while processing your request.", content);
        Assert.DoesNotContain("Fake crash!", content);
        Assert.DoesNotContain("System.Exception", content);
        Assert.DoesNotContain(" at Program", content);
    }

    [Fact]
    public async Task SpecDashboard_EncodesHtmlToPreventXss()
    {
        var response = await _client.GetAsync("/TestDashboard");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();

        // The malicious script should not be present as-is
        Assert.DoesNotContain("<script>alert(1)</script>", content);

        // It should be HTML-encoded
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", content);
    }
}

internal sealed class StubHttpClientFactory : IHttpClientFactory
{
    private readonly StubHttpMessageHandler _handler;

    public int RequestCount => _handler.RequestCount;

    public StubHttpClientFactory(string responseBody)
    {
        _handler = new StubHttpMessageHandler(responseBody);
    }

    public HttpClient CreateClient(string name)
    {
        return new HttpClient(_handler, disposeHandler: false);
    }
}

internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly string _responseBody;

    public int RequestCount { get; private set; }

    public StubHttpMessageHandler(string responseBody)
    {
        _responseBody = responseBody;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        RequestCount++;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(_responseBody)
        });
    }
}

public sealed class TestApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IAiService, FakeAiService>();
        });
    }
}

internal sealed class FakeAiService : IAiService
{
    public Task<SpecResponse> GenerateSpecAsync(string prompt, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new SpecResponse(
            Title: prompt,
            Summary: "",
            FunctionalRequirements: [],
            NonFunctionalRequirements: [],
            UserStories: [],
            ClarifyingQuestions: [],
            Risks: []
        ));
    }
}