using Pl1MigrationDemo.Models;

namespace Pl1MigrationDemo.Services;

/// <summary>
/// Runs real, deterministic checks against the converted .NET application (build, functional
/// behavior, layering rules) instead of relying on an LLM's opinion of whether the code is correct.
/// </summary>
public interface ICodeVerificationService
{
    Task<CodeVerificationReport> VerifyAsync(CancellationToken cancellationToken);
}
