namespace SpecBridge.Services;

using SpecBridge.Models;

public interface ISpecGeneratorService
{
    Task<SpecificationDocument> GenerateSpecAsync(string prompt, CancellationToken cancellationToken = default);
}
