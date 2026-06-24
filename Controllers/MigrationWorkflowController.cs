using Microsoft.AspNetCore.Mvc;
using Pl1MigrationDemo.Models;
using Pl1MigrationDemo.Services;

namespace Pl1MigrationDemo.Controllers;

public class MigrationWorkflowController : Controller
{
    private readonly IWebHostEnvironment _environment;
    private readonly IAgentWorkflowEngine _workflowEngine;

    public MigrationWorkflowController(IWebHostEnvironment environment, IAgentWorkflowEngine workflowEngine)
    {
        _environment = environment;
        _workflowEngine = workflowEngine;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View(BuildViewModel(null));
    }

    [HttpPost]
    public IActionResult Index(string requestText)
    {
        return View(BuildViewModel(requestText));
    }

    [HttpPost]
    public async Task<IActionResult> Run([FromBody] MigrationRunInput input, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _workflowEngine.RunAsync(input, cancellationToken);
            SaveRunArtifacts(result);
            return Json(result);
        }
        catch (Exception ex)
        {
            var result = new MigrationRunResult
            {
                RunId = $"RUN-{DateTime.UtcNow:yyyyMMddHHmmss}",
                ProviderMode = "Server error",
                Domain = "Unknown",
                TransactionName = "Unknown",
                Summary = "The workflow failed before it could complete.",
                NextActions =
                [
                    "Check the error card in the live run panel.",
                    "Verify Ollama is running, model name is correct, and network access to OLLAMA_BASE_URL works.",
                    "Try again with a smaller PL/I snippet if the request is very large."
                ],
                Steps =
                [
                    new AgentRunStep
                    {
                        Sequence = 1,
                        AgentName = "Workflow Error Checkpoint Agent",
                        AgentType = "Checkpoint",
                        Status = "Fail",
                        Finding = ex.Message,
                        Evidence = [ex.GetType().Name]
                    }
                ]
            };

            SaveRunArtifacts(result);
            Response.StatusCode = StatusCodes.Status500InternalServerError;
            return Json(result);
        }
    }

    [HttpGet]
    public IActionResult CompareScreens()
    {
        var path = Path.Combine(_environment.ContentRootPath, "pl1-to-dotnet-screens.html");

        if (!System.IO.File.Exists(path))
        {
            return NotFound("Screen comparison page was not found.");
        }

        return PhysicalFile(path, "text/html");
    }

    [HttpGet]
    public IActionResult Deliverable(string file)
    {
        if (string.IsNullOrWhiteSpace(file) || file.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return BadRequest("Invalid deliverable name.");
        }

        var path = Path.Combine(_environment.ContentRootPath, "migration-workflow", "deliverables", file);

        if (!System.IO.File.Exists(path))
        {
            return NotFound("Deliverable was not found.");
        }

        ViewBag.FileName = file;
        ViewBag.Content = System.IO.File.ReadAllText(path);

        return View();
    }

    private void SaveRunArtifacts(MigrationRunResult result)
    {
        var root = Path.Combine(_environment.ContentRootPath, "migration-workflow", "runs", result.RunId);
        Directory.CreateDirectory(root);

        var runJson = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
        System.IO.File.WriteAllText(Path.Combine(root, "run.json"), runJson);

        foreach (var artifact in result.Artifacts)
        {
            var fileName = string.Join("-", artifact.Name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
            fileName = string.IsNullOrWhiteSpace(fileName) ? "artifact" : fileName;
            System.IO.File.WriteAllText(Path.Combine(root, $"{fileName}.md"), artifact.Content);
        }

        result.SavedRunPath = root;
    }

    private static MigrationWorkflowViewModel BuildViewModel(string? requestText)
    {
        var model = new MigrationWorkflowViewModel();

        if (!string.IsNullOrWhiteSpace(requestText))
        {
            model.RequestText = requestText.Trim();
        }

        model.Stages =
        [
            new MigrationAgentStage
            {
                Sequence = 1,
                ProducerAgent = "BRD Producer Agent",
                ProducerPurpose = "Reverse engineer business requirements from PL/I screens and transaction behavior.",
                ProducerStatus = "LLM generated",
                CheckpointAgent = "BRD Checkpoint Agent",
                CheckpointPurpose = "Verify requirements are business-focused, traceable, and testable.",
                CheckpointStatus = "LLM reviewed",
                OutputName = "BRD.md"
            },
            new MigrationAgentStage
            {
                Sequence = 2,
                ProducerAgent = "Technical Design Agent",
                ProducerPurpose = "Design the .NET MVC architecture and map legacy components to target components.",
                ProducerStatus = "LLM generated",
                CheckpointAgent = "Technical Design Checkpoint Agent",
                CheckpointPurpose = "Verify design completeness, data flow, validation, and production concerns.",
                CheckpointStatus = "LLM reviewed",
                OutputName = "Technical-Design.md"
            },
            new MigrationAgentStage
            {
                Sequence = 3,
                ProducerAgent = "Converted Code Agent",
                ProducerPurpose = "Generate .NET code blueprint and representative implementation.",
                ProducerStatus = "LLM generated",
                CheckpointAgent = "Code Checkpoint Agent",
                CheckpointPurpose = "Verify build feasibility, parity, validation, and hardening gaps.",
                CheckpointStatus = "LLM reviewed",
                OutputName = "Converted-Code-Summary.md"
            },
            new MigrationAgentStage
            {
                Sequence = 4,
                ProducerAgent = "Test Plan Agent",
                ProducerPurpose = "Define test strategy, scope, levels, environments, risks, regression, and UAT.",
                ProducerStatus = "LLM generated",
                CheckpointAgent = "Test Plan Checkpoint Agent",
                CheckpointPurpose = "Verify coverage for all screens, parity, negative paths, and exit criteria.",
                CheckpointStatus = "LLM reviewed",
                OutputName = "Test-Plan.md"
            },
            new MigrationAgentStage
            {
                Sequence = 5,
                ProducerAgent = "Test Case Agent",
                ProducerPurpose = "Create executable functional, negative, regression, UI, parity, and E2E tests.",
                ProducerStatus = "LLM generated",
                CheckpointAgent = "Test Case Checkpoint Agent",
                CheckpointPurpose = "Verify traceability, expected results, and executable steps.",
                CheckpointStatus = "LLM reviewed",
                OutputName = "Test-Cases.md"
            }
        ];

        model.Deliverables =
        [
            new MigrationDeliverable
            {
                Name = "Business Requirement Document",
                Description = "Business requirements reverse engineered from the PL/I/CICS transaction.",
                Path = "BRD.md",
                Status = "Stored sample"
            },
            new MigrationDeliverable
            {
                Name = "Technical Design Document",
                Description = "Target .NET architecture, component mapping, and data flow.",
                Path = "Technical-Design.md",
                Status = "Stored sample"
            },
            new MigrationDeliverable
            {
                Name = "Converted Code Summary",
                Description = "Summary of implemented .NET MVC code and functional behavior.",
                Path = "Converted-Code-Summary.md",
                Status = "Stored sample"
            },
            new MigrationDeliverable
            {
                Name = "Test Plan",
                Description = "Testing strategy for validating converted behavior against legacy behavior.",
                Path = "Test-Plan.md",
                Status = "Stored sample"
            },
            new MigrationDeliverable
            {
                Name = "Test Cases",
                Description = "Executable test cases for search, update, confirmation, and parity.",
                Path = "Test-Cases.md",
                Status = "Stored sample"
            }
        ];

        model.Timeline =
        [
            new MigrationTimelineItem { Agent = "Intake", Action = "Receives request and PL/I text.", Status = "Runtime" },
            new MigrationTimelineItem { Agent = "Producer agents", Action = "Call LLM to generate BRD, TDD, code blueprint, test plan, and tests.", Status = "Runtime" },
            new MigrationTimelineItem { Agent = "Checkpoint agents", Action = "Call LLM to review traceability, quality, and gaps.", Status = "Runtime" },
            new MigrationTimelineItem { Agent = "Final review", Action = "Summarizes pass/fail readiness.", Status = "Runtime" }
        ];

        return model;
    }
}
