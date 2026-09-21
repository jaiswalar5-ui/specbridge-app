namespace SpecBridge.Services;

public interface ISpecGeneratorService
{
    Task<string> GenerateSpecAsync(string prompt, CancellationToken cancellationToken = default);
}
