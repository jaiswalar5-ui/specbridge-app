namespace SpecBridge.Models;

public class SpecResponse
{
    public string Status { get; set; } = "Success";
    public string Message { get; set; } = "Spec generation endpoint reached successfully.";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
