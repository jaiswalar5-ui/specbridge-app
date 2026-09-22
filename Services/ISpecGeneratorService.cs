namespace SpecBridge.Services;

using SpecBridge.Models;

public interface ISpecGeneratorService
{
    Task<SpecResponse> GenerateSpecAsync(
        string prompt,
        CancellationToken cancellationToken = default);
}
