namespace SpecBridge.Services;

using SpecBridge.Models;

public interface IAiService
{
    Task<SpecResponse> GenerateSpecAsync(
        string prompt,
        CancellationToken cancellationToken = default);
}
