namespace SpecBridge.Models;

public sealed class SpecResponse
{
    public string Status { get; init; } = "Success";
    public string Message { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public SpecificationDocument? Specification { get; init; }
}
