namespace SpecBridge.Models;

public sealed class SpecificationDocument
{
    public string Title { get; init; } = "Generated Specification";
    public string Summary { get; init; } = string.Empty;
    public List<FunctionalRequirement> FunctionalRequirements { get; init; } = [];
    public List<NonFunctionalRequirement> NonFunctionalRequirements { get; init; } = [];
    public List<UserStory> UserStories { get; init; } = [];
    public List<RequirementGap> Gaps { get; init; } = [];
    public List<RequirementRisk> Risks { get; init; } = [];
}

public sealed class FunctionalRequirement
{
    public string Id { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Priority { get; init; } = "Must";
    public List<string> AcceptanceCriteria { get; init; } = [];
}

public sealed class NonFunctionalRequirement
{
    public string Category { get; init; } = string.Empty;
    public string Requirement { get; init; } = string.Empty;
    public string Target { get; init; } = string.Empty;
}

public sealed class UserStory
{
    public string Id { get; init; } = string.Empty;
    public string AsA { get; init; } = string.Empty;
    public string IWant { get; init; } = string.Empty;
    public string SoThat { get; init; } = string.Empty;
    public List<string> AcceptanceCriteria { get; init; } = [];
}

public sealed class RequirementGap
{
    public string Area { get; init; } = string.Empty;
    public string Question { get; init; } = string.Empty;
    public string Impact { get; init; } = string.Empty;
}

public sealed class RequirementRisk
{
    public string Description { get; init; } = string.Empty;
    public string Severity { get; init; } = "Medium";
    public string Mitigation { get; init; } = string.Empty;
}