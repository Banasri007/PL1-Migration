using Pl1MigrationDemo.Models;

namespace Pl1MigrationDemo.Services;

public interface IAgentWorkflowEngine
{
    Task<MigrationRunResult> RunAsync(MigrationRunInput input, CancellationToken cancellationToken);
}
