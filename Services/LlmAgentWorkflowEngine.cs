using System.Text.Json;
using System.Text.RegularExpressions;
using Pl1MigrationDemo.Models;

namespace Pl1MigrationDemo.Services;

public class LlmAgentWorkflowEngine : IAgentWorkflowEngine
{
    private const string CodeCheckpointAgentName = "Code Checkpoint Agent";

    private readonly ILlmClient _llmClient;
    private readonly IConfiguration _configuration;
    private readonly ICodeVerificationService _codeVerificationService;

    public LlmAgentWorkflowEngine(ILlmClient llmClient, IConfiguration configuration, ICodeVerificationService codeVerificationService)
    {
        _llmClient = llmClient;
        _configuration = configuration;
        _codeVerificationService = codeVerificationService;
    }

    public async Task<MigrationRunResult> RunAsync(MigrationRunInput input, CancellationToken cancellationToken)
    {
        var request = string.IsNullOrWhiteSpace(input.RequestText)
            ? "Migrate the supplied PL/I mainframe transaction to .NET."
            : input.RequestText.Trim();
        var legacyCode = string.IsNullOrWhiteSpace(input.LegacyCode)
            ? ""
            : input.LegacyCode.Trim();
        var domain = DetectDomain(request, legacyCode);
        var transaction = DetectTransactionName(legacyCode);
        var run = new MigrationRunResult
        {
            RunId = $"RUN-{DateTime.UtcNow:yyyyMMddHHmmss}",
            ProviderMode = _llmClient.ProviderName,
            Domain = domain,
            TransactionName = transaction,
            Summary = "LLM-backed multi-agent migration workflow."
        };

        if (!_llmClient.IsConfigured)
        {
            run.Steps.Add(new AgentRunStep
            {
                Sequence = 1,
                AgentName = "LLM Configuration Checkpoint Agent",
                AgentType = "Checkpoint",
                Status = "Fail",
                Finding = "LLM provider is not configured, so real agent reasoning cannot run.",
                Evidence =
                [
                    "For Ollama, ensure the service is running and reachable.",
                    "Optional: set OLLAMA_MODEL to the model you want to use.",
                    "No hardcoded agent outputs were generated for this run."
                ]
            });
            run.NextActions =
            [
                "Start Ollama, restart the app, and run the workflow again.",
                "Use OLLAMA_MODEL to choose a local model."
            ];
            return run;
        }

        var mode = _configuration["AgentWorkflow:Mode"] ?? Environment.GetEnvironmentVariable("AGENT_WORKFLOW_MODE") ?? "single";
        if (!mode.Equals("multi", StringComparison.OrdinalIgnoreCase))
        {
            return await RunSingleCallAsync(request, legacyCode, run, cancellationToken);
        }

        var agentDefinitions = BuildAgentDefinitions();

        for (var index = 0; index < agentDefinitions.Count; index++)
        {
            var agent = agentDefinitions[index];

            if (agent.Name.Equals(CodeCheckpointAgentName, StringComparison.OrdinalIgnoreCase))
            {
                var (deterministicStep, deterministicArtifact) = await BuildDeterministicCodeCheckpointAsync(
                    index + 1, request, legacyCode, cancellationToken);

                run.Steps.Add(deterministicStep);
                run.Artifacts.Add(deterministicArtifact);
                continue;
            }

            var response = await RunAgentSafelyAsync(agent, request, legacyCode, run, cancellationToken);

            run.Steps.Add(new AgentRunStep
            {
                Sequence = index + 1,
                AgentName = agent.Name,
                AgentType = agent.Type,
                Status = NormalizeStatus(response.Status),
                Finding = response.Finding,
                Evidence = response.Evidence.Count == 0 ? ["Agent returned no explicit evidence."] : response.Evidence
            });

            if (!string.IsNullOrWhiteSpace(response.ArtifactName) || !string.IsNullOrWhiteSpace(response.ArtifactContent))
            {
                run.Artifacts.Add(new GeneratedArtifactPreview
                {
                    Name = string.IsNullOrWhiteSpace(response.ArtifactName) ? agent.Name : response.ArtifactName,
                    Status = NormalizeStatus(response.Status),
                    Content = response.ArtifactContent
                });
            }
        }

        run.Summary = BuildSummary(run);
        run.NextActions =
        [
            "Review any Fail or Needs Review checkpoint cards.",
            "Export approved artifact content into BRD, TDD, code, and test documents.",
            "Run generated code through build, unit tests, and UAT parity validation.",
            "Connect the repository layer to the target enterprise data source."
        ];

        return run;
    }

    private async Task<MigrationRunResult> RunSingleCallAsync(string request, string legacyCode, MigrationRunResult run, CancellationToken cancellationToken)
    {
        var systemPrompt = """
            You are a PL/I mainframe to .NET migration factory composed of multiple specialized agents.
            In one response, simulate the full agent workflow using real reasoning over the supplied PL/I/BMS/JCL text.
            Do not invent legacy behavior. If evidence is missing, mark that checkpoint as Needs Review or Fail.
            Return strict JSON only with this exact shape:
            {
              "summary": "short migration readiness summary",
              "steps": [
                {
                  "sequence": 1,
                  "agentName": "Legacy Intake Agent",
                  "agentType": "Discovery | Producer | Checkpoint",
                  "status": "Pass | Fail | Needs Review | Remediated",
                  "finding": "concise finding",
                  "evidence": ["specific evidence"]
                }
              ],
              "artifacts": [
                {
                  "name": "artifact name",
                  "status": "Pass | Fail | Needs Review | Remediated",
                  "content": "markdown artifact content"
                }
              ],
              "nextActions": ["action"]
            }

            Required agents:
            1. Legacy Intake Agent
            2. BRD Producer Agent
            3. BRD Checkpoint Agent
            4. Technical Design Agent
            5. Technical Design Checkpoint Agent
            6. Converted Code Agent
            7. Code Checkpoint Agent
            8. Test Plan Agent
            9. Test Case Agent
            10. Final Readiness Checkpoint Agent
            """;

        var userPrompt = $"""
            Migration request:
            {request}

            Legacy PL/I / BMS / mainframe input:
            {legacyCode}
            """;

        try
        {
            var raw = await _llmClient.GenerateAsync(systemPrompt, userPrompt, cancellationToken);
            var llmRun = ParseSingleCallResponse(raw);
            run.Summary = string.IsNullOrWhiteSpace(llmRun.Summary) ? run.Summary : llmRun.Summary;
            run.Steps = llmRun.Steps;
            run.Artifacts = llmRun.Artifacts;
            run.NextActions = llmRun.NextActions.Count == 0
                ? ["Review generated artifacts and checkpoint failures."]
                : llmRun.NextActions;

            await ApplyDeterministicCodeCheckpointAsync(run, request, legacyCode, cancellationToken);
            FlagMissingAgents(run);

            return run;
        }
        catch (Exception ex)
        {
            run.Summary = "The single-call Ollama workflow failed.";
            run.Steps =
            [
                new AgentRunStep
                {
                    Sequence = 1,
                    AgentName = "Ollama Workflow Checkpoint Agent",
                    AgentType = "Checkpoint",
                    Status = "Fail",
                    Finding = ex.Message,
                    Evidence =
                    [
                        ex.GetType().Name,
                        "Default mode uses one Ollama call per workflow to reduce local resource usage."
                    ]
                }
            ];
            run.NextActions =
            [
                "Verify Ollama is running at OLLAMA_BASE_URL.",
                "Try OLLAMA_MODEL=llama3.2 or another model you have pulled locally.",
                "Run 'ollama pull <model>' if the model is missing.",
                "Use AGENT_WORKFLOW_MODE=multi only when your local machine has enough resources."
            ];
            return run;
        }
    }

    private static readonly string[] RequiredAgentNames =
    [
        "Legacy Intake Agent",
        "BRD Producer Agent",
        "BRD Checkpoint Agent",
        "Technical Design Agent",
        "Technical Design Checkpoint Agent",
        "Converted Code Agent",
        CodeCheckpointAgentName,
        "Test Plan Agent",
        "Test Case Agent",
        "Final Readiness Checkpoint Agent"
    ];

    /// <summary>
    /// Small local models can truncate a single-call response before producing all 10 agents.
    /// Rather than silently showing only whichever agents made it through, this makes the gap
    /// visible so it doesn't look like the workflow quietly finished early.
    /// </summary>
    private static void FlagMissingAgents(MigrationRunResult run)
    {
        var presentNames = run.Steps
            .Select(step => step.AgentName.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = RequiredAgentNames.Where(name => !presentNames.Contains(name)).ToList();

        if (missing.Count == 0)
        {
            return;
        }

        foreach (var name in missing)
        {
            run.Steps.Add(new AgentRunStep
            {
                Sequence = Array.IndexOf(RequiredAgentNames, name) + 1,
                AgentName = name,
                AgentType = "Unknown",
                Status = "Needs Review",
                Finding = "This agent did not appear in the LLM's response. The single-call response was likely truncated before this agent's output was produced.",
                Evidence =
                [
                    "Try increasing Ollama's num_predict/num_ctx, using a more capable local model, or switching to AGENT_WORKFLOW_MODE=multi so each agent gets its own dedicated call."
                ]
            });
        }

        run.Steps = run.Steps.OrderBy(step => step.Sequence).ToList();
        run.Summary = BuildSummary(run);
    }

    private static MigrationRunResult ParseSingleCallResponse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException("The LLM returned an empty response.");
        }

        var json = ExtractJson(raw);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var result = new MigrationRunResult
        {
            Summary = root.TryGetProperty("summary", out var summary)
                ? summary.GetString() ?? ""
                : ""
        };

        if (root.TryGetProperty("steps", out var steps))
        {
            foreach (var step in steps.EnumerateArray())
            {
                result.Steps.Add(new AgentRunStep
                {
                    Sequence = step.TryGetProperty("sequence", out var sequence) ? sequence.GetInt32() : result.Steps.Count + 1,
                    AgentName = GetString(step, "agentName"),
                    AgentType = GetString(step, "agentType"),
                    Status = NormalizeStatus(GetString(step, "status")),
                    Finding = GetString(step, "finding"),
                    Evidence = GetStringList(step, "evidence")
                });
            }
        }

        if (root.TryGetProperty("artifacts", out var artifacts))
        {
            foreach (var artifact in artifacts.EnumerateArray())
            {
                result.Artifacts.Add(new GeneratedArtifactPreview
                {
                    Name = GetString(artifact, "name"),
                    Status = NormalizeStatus(GetString(artifact, "status")),
                    Content = GetString(artifact, "content")
                });
            }
        }

        result.NextActions = root.TryGetProperty("nextActions", out var nextActions)
            ? nextActions.EnumerateArray().Select(item => item.GetString() ?? "").Where(item => item.Length > 0).ToList()
            : [];

        if (result.Steps.Count == 0)
        {
            result.Steps.Add(new AgentRunStep
            {
                Sequence = 1,
                AgentName = "Single-Call Parser Checkpoint Agent",
                AgentType = "Checkpoint",
                Status = "Needs Review",
                Finding = "The LLM returned JSON but no agent steps.",
                Evidence = [raw.Length > 2000 ? raw[..2000] : raw]
            });
        }

        return result;
    }

    private static string GetString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var property) ? property.GetString() ?? "" : "";
    }

    private static List<string> GetStringList(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return property.EnumerateArray()
            .Select(item => item.GetString() ?? "")
            .Where(item => item.Length > 0)
            .ToList();
    }

    private async Task<LlmAgentResponse> RunAgentAsync(AgentDefinition agent, string request, string legacyCode, MigrationRunResult run, CancellationToken cancellationToken)
    {
        var systemPrompt = """
            You are one agent in a PL/I mainframe to .NET migration factory.
            You must reason from the supplied request and PL/I/BMS/JCL text only.
            Do not invent legacy behavior. If evidence is missing, mark status as Needs Review or Fail.
            Return strict JSON only with this shape:
            {
              "status": "Pass | Fail | Needs Review | Remediated",
              "finding": "one concise finding",
              "evidence": ["specific evidence from input or previous agent outputs"],
              "artifactName": "optional artifact name",
              "artifactContent": "markdown content, code, or test material produced by this agent"
            }
            """;

        var previous = JsonSerializer.Serialize(run.Steps.Select(step => new
        {
            step.AgentName,
            step.AgentType,
            step.Status,
            step.Finding,
            step.Evidence
        }));

        var userPrompt = $"""
            Migration request:
            {request}

            Legacy PL/I / BMS / mainframe input:
            {legacyCode}

            Previous agent outputs:
            {previous}

            Agent name:
            {agent.Name}

            Agent type:
            {agent.Type}

            Agent responsibility:
            {agent.Responsibility}
            """;

        var raw = await _llmClient.GenerateAsync(systemPrompt, userPrompt, cancellationToken);
        return ParseAgentResponse(raw);
    }

    private async Task<LlmAgentResponse> RunAgentSafelyAsync(AgentDefinition agent, string request, string legacyCode, MigrationRunResult run, CancellationToken cancellationToken)
    {
        try
        {
            return await RunAgentAsync(agent, request, legacyCode, run, cancellationToken);
        }
        catch (Exception ex)
        {
            return new LlmAgentResponse
            {
                Status = "Fail",
                Finding = $"{agent.Name} failed: {ex.Message}",
                Evidence = [ex.GetType().Name],
                ArtifactName = $"{agent.Name} Error",
                ArtifactContent = $"# {agent.Name} Error\n\n{ex.Message}"
            };
        }
    }

    private static LlmAgentResponse ParseAgentResponse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new LlmAgentResponse
            {
                Status = "Fail",
                Finding = "The LLM returned an empty response.",
                Evidence = ["Empty response body"]
            };
        }

        try
        {
            var json = ExtractJson(raw);
            var response = JsonSerializer.Deserialize<LlmAgentResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return response ?? new LlmAgentResponse
            {
                Status = "Needs Review",
                Finding = "The LLM response could not be parsed.",
                Evidence = [raw]
            };
        }
        catch (JsonException ex)
        {
            return new LlmAgentResponse
            {
                Status = "Needs Review",
                Finding = $"The LLM response was not valid JSON: {ex.Message}",
                Evidence = [raw.Length > 2000 ? raw[..2000] : raw]
            };
        }
    }

    private static string ExtractJson(string raw)
    {
        var trimmed = raw.Trim();

        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            trimmed = Regex.Replace(trimmed, "^```(?:json)?", "", RegexOptions.IgnoreCase).Trim();
            trimmed = Regex.Replace(trimmed, "```$", "").Trim();
        }

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');

        return start >= 0 && end > start ? trimmed[start..(end + 1)] : trimmed;
    }

    /// <summary>
    /// Re-runs the Code Checkpoint Agent's verdict using real ground-truth checks (a real dotnet build,
    /// real execution of the customer search/update logic, and static layering checks) instead of trusting
    /// whatever Pass/Fail the LLM produced in the single-call JSON response. The deterministic result
    /// always wins; the LLM-authored step (if any) is replaced.
    /// </summary>
    private async Task ApplyDeterministicCodeCheckpointAsync(MigrationRunResult run, string request, string legacyCode, CancellationToken cancellationToken)
    {
        try
        {
            var existingIndex = run.Steps.FindIndex(step =>
                step.AgentName.Equals(CodeCheckpointAgentName, StringComparison.OrdinalIgnoreCase));
            var sequence = existingIndex >= 0 ? run.Steps[existingIndex].Sequence : run.Steps.Count + 1;

            var (deterministicStep, deterministicArtifact) = await BuildDeterministicCodeCheckpointAsync(
                sequence, request, legacyCode, cancellationToken);

            if (existingIndex >= 0)
            {
                run.Steps[existingIndex] = deterministicStep;
            }
            else
            {
                run.Steps.Add(deterministicStep);
            }

            var existingArtifactIndex = run.Artifacts.FindIndex(artifact =>
                artifact.Name.Contains("Code Checkpoint", StringComparison.OrdinalIgnoreCase)
                || artifact.Name.Contains("Code-Checkpoint", StringComparison.OrdinalIgnoreCase));

            if (existingArtifactIndex >= 0)
            {
                run.Artifacts[existingArtifactIndex] = deterministicArtifact;
            }
            else
            {
                run.Artifacts.Add(deterministicArtifact);
            }

            run.Summary = BuildSummary(run);
        }
        catch (Exception ex)
        {
            // If ground-truth verification itself blows up (e.g. dotnet not on PATH), surface that
            // honestly as a Needs Review step rather than silently falling back to the LLM's opinion.
            run.Steps.Add(new AgentRunStep
            {
                Sequence = run.Steps.Count + 1,
                AgentName = CodeCheckpointAgentName,
                AgentType = "Checkpoint",
                Status = "Needs Review",
                Finding = "Deterministic ground-truth verification could not run.",
                Evidence = [$"{ex.GetType().Name}: {ex.Message}"]
            });
        }
    }

    /// <summary>
    /// Runs real ground-truth checks and produces the Code Checkpoint Agent's step + artifact.
    /// The LLM is only asked for a short narrative explanation of results it cannot contradict;
    /// it never decides Pass/Fail itself.
    /// </summary>
    private async Task<(AgentRunStep Step, GeneratedArtifactPreview Artifact)> BuildDeterministicCodeCheckpointAsync(
        int sequence, string request, string legacyCode, CancellationToken cancellationToken)
    {
        var report = await _codeVerificationService.VerifyAsync(cancellationToken);
        var evidence = BuildGroundTruthEvidence(report);

        var fallbackFinding = report.OverallStatus switch
        {
            "Pass" => "All deterministic ground-truth checks passed: the application builds, customer search/update behavior matched expected results, and layering rules were respected.",
            "Fail" => "One or more deterministic ground-truth checks failed. See the evidence for the specific build, functional, or layering failure.",
            _ => "The build and functional behavior checks passed, but a layering review item needs attention before this is fully ready."
        };

        var narrative = await TryGetLlmNarrativeAsync(request, legacyCode, report, evidence, cancellationToken);

        var step = new AgentRunStep
        {
            Sequence = sequence,
            AgentName = CodeCheckpointAgentName,
            AgentType = "Checkpoint",
            Status = report.OverallStatus,
            Finding = string.IsNullOrWhiteSpace(narrative) ? fallbackFinding : narrative,
            Evidence = evidence
        };

        var artifactContent = "# Code Checkpoint - Ground Truth Verification\n\n"
            + $"**Overall status:** {report.OverallStatus}\n\n"
            + $"{step.Finding}\n\n"
            + "## Evidence\n\n"
            + string.Join("\n", evidence.Select(line => $"- {line}"));

        var artifact = new GeneratedArtifactPreview
        {
            Name = "Code-Checkpoint-Ground-Truth",
            Status = report.OverallStatus,
            Content = artifactContent
        };

        return (step, artifact);
    }

    private async Task<string> TryGetLlmNarrativeAsync(
        string request, string legacyCode, CodeVerificationReport report, List<string> evidence, CancellationToken cancellationToken)
    {
        try
        {
            const string systemPrompt = """
                You are the Code Checkpoint Agent in a PL/I to .NET migration factory.
                Real, deterministic ground-truth checks have already been run against the actual application:
                a real "dotnet build", real execution of the customer search/update logic against known test
                data, and static layering checks. These results are authoritative. You must not contradict
                them, invent a different Pass/Fail outcome, or claim something passed that the evidence shows
                failed. Your only job is to write a short (2-4 sentence) plain-language explanation of what
                the ground-truth results mean for migration readiness.
                Return strict JSON only with this exact shape: {"narrative": "..."}
                """;

            var userPrompt = $"""
                Migration request:
                {request}

                Legacy PL/I / BMS / mainframe input:
                {legacyCode}

                Ground-truth verification results (authoritative, do not contradict):
                {string.Join("\n", evidence)}

                Overall deterministic status: {report.OverallStatus}
                """;

            var raw = await _llmClient.GenerateAsync(systemPrompt, userPrompt, cancellationToken);
            var json = ExtractJson(raw);
            using var document = JsonDocument.Parse(json);

            return document.RootElement.TryGetProperty("narrative", out var narrativeProperty)
                ? narrativeProperty.GetString() ?? ""
                : "";
        }
        catch
        {
            // The narrative is a nice-to-have. The deterministic status and evidence stand on their own
            // even if the LLM is unavailable or returns something unparseable.
            return "";
        }
    }

    private static List<string> BuildGroundTruthEvidence(CodeVerificationReport report)
    {
        var evidence = new List<string>
        {
            $"[Build] {(report.BuildResult.Passed ? "PASS" : "FAIL")} (exit code {report.BuildResult.ExitCode}): {TruncateForEvidence(report.BuildResult.Output)}"
        };

        evidence.AddRange(report.FunctionalChecks.Select(check =>
            $"[Functional] {(check.Passed ? "PASS" : "FAIL")} - {check.Name}. Expected: {check.Expected}. Actual: {check.Actual}"));

        evidence.AddRange(report.LayeringChecks.Select(check =>
            $"[Layering] {(check.Passed ? "PASS" : "FAIL")} - {check.Name}. {check.Detail}"));

        return evidence;
    }

    private static string TruncateForEvidence(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "(no build output captured)";
        }

        const int max = 600;
        return text.Length <= max ? text : text[..max] + "...";
    }

    private static List<AgentDefinition> BuildAgentDefinitions()
    {
        return
        [
            new("Legacy Intake Agent", "Discovery", "Identify transaction name, screens, data fields, input/output actions, files, SQL, CICS commands, and uncertainties."),
            new("BRD Producer Agent", "Producer", "Create a business requirement document reverse engineered from the legacy evidence. Keep it business-focused."),
            new("BRD Checkpoint Agent", "Checkpoint", "Review the BRD output for traceability to legacy evidence, missing business rules, assumptions, and testability."),
            new("Technical Design Agent", "Producer", "Create the target .NET technical design, including architecture, component mapping, data flow, validation, errors, security, and deployment."),
            new("Technical Design Checkpoint Agent", "Checkpoint", "Review the technical design for implementability, BRD alignment, production gaps, and unsupported assumptions."),
            new("Converted Code Agent", "Producer", "Generate a concise .NET implementation blueprint and representative C# / Razor code for the supplied transaction."),
            new(CodeCheckpointAgentName, "Checkpoint", "Review generated code for build feasibility, separation of concerns, missing validation, security gaps, and legacy parity risks."),
            new("Test Plan Agent", "Producer", "Create the test plan covering functional, negative, migration parity, regression, integration, UAT, and non-functional areas."),
            new("Test Case Agent", "Producer", "Create executable test cases with IDs, data, steps, and expected results."),
            new("Final Readiness Checkpoint Agent", "Checkpoint", "Decide whether the migration package is ready for review. List blocking and non-blocking gaps.")
        ];
    }

    private static string NormalizeStatus(string status)
    {
        return status.Trim().ToLowerInvariant() switch
        {
            "pass" => "Pass",
            "fail" => "Fail",
            "remediated" => "Remediated",
            _ => "Needs Review"
        };
    }

    private static string BuildSummary(MigrationRunResult run)
    {
        var failed = run.Steps.Count(step => step.Status == "Fail");
        var review = run.Steps.Count(step => step.Status == "Needs Review");

        return failed == 0 && review == 0
            ? "All LLM-backed agents completed without blocking findings."
            : $"Completed with {failed} failed checkpoint(s) and {review} item(s) needing review.";
    }

    private static string DetectDomain(string request, string legacyCode)
    {
        var text = $"{request} {legacyCode}";

        if (text.Contains("EMP", StringComparison.OrdinalIgnoreCase) || text.Contains("employee", StringComparison.OrdinalIgnoreCase))
        {
            return "Employee";
        }

        if (text.Contains("ACCOUNT", StringComparison.OrdinalIgnoreCase) || text.Contains("acct", StringComparison.OrdinalIgnoreCase))
        {
            return "Account";
        }

        if (text.Contains("POLICY", StringComparison.OrdinalIgnoreCase))
        {
            return "Policy";
        }

        if (text.Contains("CLAIM", StringComparison.OrdinalIgnoreCase))
        {
            return "Claim";
        }

        return "Legacy";
    }

    private static string DetectTransactionName(string legacyCode)
    {
        var match = Regex.Match(legacyCode, @"\b[A-Z]{3,8}(?:PGM|TRAN|SRCH|DTL|CONF|INQ|UPD)?\b", RegexOptions.IgnoreCase);
        return match.Success ? match.Value.ToUpperInvariant() : "LEGACY-TXN";
    }

    private sealed record AgentDefinition(string Name, string Type, string Responsibility);
}
