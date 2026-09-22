using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SpecBridge.Models;
using SpecBridge.Services;
using Xunit;

namespace SpecBridge.Tests;

public sealed class SpecBridgeIntegrationTests : IClassFixture<TestApplicationFactory>
{
    private readonly HttpClient _client;

    public SpecBridgeIntegrationTests(TestApplicationFactory factory)
    {
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
    public async Task SpecificationContract_RoundTripsStructuredSections()
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
}

public sealed class TestApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<ISpecGeneratorService, FakeSpecGeneratorService>();
        });
    }
}

internal sealed class FakeSpecGeneratorService : ISpecGeneratorService
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