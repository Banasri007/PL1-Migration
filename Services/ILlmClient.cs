namespace Pl1MigrationDemo.Services;

public interface ILlmClient
{
    bool IsConfigured { get; }
    string ProviderName { get; }
    Task<string> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken);
}
