using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SpecBridge.Models;
using SpecBridge.Services;

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
        var document = new SpecificationDocument
        {
            Title = "Leave management",
            FunctionalRequirements = [new FunctionalRequirement { Id = "FR-001", Description = "Submit leave" }],
            Gaps = [new RequirementGap { Area = "Notifications", Question = "Which channels?" }]
        };

        var json = JsonSerializer.Serialize(document);
        var parsed = JsonSerializer.Deserialize<SpecificationDocument>(json);

        Assert.Equal("FR-001", parsed!.FunctionalRequirements[0].Id);
        Assert.Equal("Which channels?", parsed.Gaps[0].Question);
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
    public Task<SpecificationDocument> GenerateSpecAsync(string prompt, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new SpecificationDocument { Title = prompt });
    }
}