using System.Text.Json.Serialization;

namespace SpecBridge.Models;

public record SpecResponse(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("functionalRequirements")] IReadOnlyList<FunctionalRequirement> FunctionalRequirements,
    [property: JsonPropertyName("nonFunctionalRequirements")] IReadOnlyList<NonFunctionalRequirement> NonFunctionalRequirements,
    [property: JsonPropertyName("userStories")] IReadOnlyList<UserStory> UserStories,
    [property: JsonPropertyName("clarifyingQuestions")] IReadOnlyList<ClarifyingQuestion> ClarifyingQuestions,
    [property: JsonPropertyName("risks")] IReadOnlyList<Risk> Risks
);

public record FunctionalRequirement(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("priority")] string Priority,
    [property: JsonPropertyName("acceptanceCriteria")] IReadOnlyList<string> AcceptanceCriteria
);

public record NonFunctionalRequirement(
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("requirement")] string Requirement,
    [property: JsonPropertyName("target")] string Target
);

public record UserStory(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("asA")] string AsA,
    [property: JsonPropertyName("iWant")] string IWant,
    [property: JsonPropertyName("soThat")] string SoThat,
    [property: JsonPropertyName("acceptanceCriteria")] IReadOnlyList<string> AcceptanceCriteria
);

public record ClarifyingQuestion(
    [property: JsonPropertyName("area")] string Area,
    [property: JsonPropertyName("question")] string Question,
    [property: JsonPropertyName("impact")] string Impact
);

public record Risk(
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("mitigation")] string Mitigation
);

[JsonSerializable(typeof(SpecResponse))]
public partial class SpecContext : JsonSerializerContext
{
}