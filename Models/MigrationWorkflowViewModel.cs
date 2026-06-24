namespace Pl1MigrationDemo.Models;

public class MigrationWorkflowViewModel
{
    public string RequestText { get; set; } =
        "Reverse engineer the legacy PL/I customer maintenance transaction and convert it to .NET.";

    public List<MigrationAgentStage> Stages { get; set; } = [];
    public List<MigrationDeliverable> Deliverables { get; set; } = [];
    public List<MigrationTimelineItem> Timeline { get; set; } = [];
}

public class MigrationAgentStage
{
    public int Sequence { get; set; }
    public string ProducerAgent { get; set; } = "";
    public string ProducerPurpose { get; set; } = "";
    public string ProducerStatus { get; set; } = "";
    public string CheckpointAgent { get; set; } = "";
    public string CheckpointPurpose { get; set; } = "";
    public string CheckpointStatus { get; set; } = "";
    public string OutputName { get; set; } = "";
}

public class MigrationDeliverable
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Path { get; set; } = "";
    public string Status { get; set; } = "";
}

public class MigrationTimelineItem
{
    public string Agent { get; set; } = "";
    public string Action { get; set; } = "";
    public string Status { get; set; } = "";
}

public class MigrationRunInput
{
    public string RequestText { get; set; } = "";
    public string LegacyCode { get; set; } = "";
}

public class MigrationRunResult
{
    public string RunId { get; set; } = "";
    public string ProviderMode { get; set; } = "";
    public string SavedRunPath { get; set; } = "";
    public string Domain { get; set; } = "";
    public string TransactionName { get; set; } = "";
    public string Summary { get; set; } = "";
    public List<AgentRunStep> Steps { get; set; } = [];
    public List<GeneratedArtifactPreview> Artifacts { get; set; } = [];
    public List<string> NextActions { get; set; } = [];
}

public class AgentRunStep
{
    public int Sequence { get; set; }
    public string AgentName { get; set; } = "";
    public string AgentType { get; set; } = "";
    public string Status { get; set; } = "";
    public string Finding { get; set; } = "";
    public List<string> Evidence { get; set; } = [];
}

public class GeneratedArtifactPreview
{
    public string Name { get; set; } = "";
    public string Status { get; set; } = "";
    public string Content { get; set; } = "";
}

public class LlmAgentResponse
{
    public string Status { get; set; } = "Needs Review";
    public string Finding { get; set; } = "";
    public List<string> Evidence { get; set; } = [];
    public string ArtifactName { get; set; } = "";
    public string ArtifactContent { get; set; } = "";
}
