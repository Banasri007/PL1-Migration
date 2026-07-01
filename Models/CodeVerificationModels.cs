namespace Pl1MigrationDemo.Models;

public class CodeVerificationReport
{
    public BuildCheckResult BuildResult { get; set; } = new();
    public List<FunctionalCheckResult> FunctionalChecks { get; set; } = [];
    public List<LayeringCheckResult> LayeringChecks { get; set; } = [];

    /// <summary>
    /// "Pass", "Fail", or "Needs Review" — computed deterministically from the checks above,
    /// not from an LLM opinion.
    /// </summary>
    public string OverallStatus { get; set; } = "Needs Review";
}

public class BuildCheckResult
{
    public bool Passed { get; set; }
    public int ExitCode { get; set; }
    public string Output { get; set; } = "";
}

public class FunctionalCheckResult
{
    public string Name { get; set; } = "";
    public bool Passed { get; set; }
    public string Expected { get; set; } = "";
    public string Actual { get; set; } = "";
}

public class LayeringCheckResult
{
    public string Name { get; set; } = "";
    public bool Passed { get; set; }
    public string Detail { get; set; } = "";
}
