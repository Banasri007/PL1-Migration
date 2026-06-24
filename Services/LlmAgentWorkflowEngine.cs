using System.Text.Json;
using System.Text.RegularExpressions;
using Pl1MigrationDemo.Models;

namespace Pl1MigrationDemo.Services;

public class LlmAgentWorkflowEngine : IAgentWorkflowEngine
{
    private readonly ILlmClient _llmClient;
    private readonly IConfiguration _configuration;

    public LlmAgentWorkflowEngine(ILlmClient llmClient, IConfiguration configuration)
    {
        _llmClient = llmClient;
        _configuration = configuration;
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
            new("Code Checkpoint Agent", "Checkpoint", "Review generated code for build feasibility, separation of concerns, missing validation, security gaps, and legacy parity risks."),
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
