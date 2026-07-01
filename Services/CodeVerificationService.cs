using System.Diagnostics;
using System.Text.RegularExpressions;
using Pl1MigrationDemo.Data;
using Pl1MigrationDemo.Models;

namespace Pl1MigrationDemo.Services;

public class CodeVerificationService : ICodeVerificationService
{
    private readonly IWebHostEnvironment _environment;

    public CodeVerificationService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<CodeVerificationReport> VerifyAsync(CancellationToken cancellationToken)
    {
        var report = new CodeVerificationReport
        {
            BuildResult = await RunBuildAsync(cancellationToken),
            FunctionalChecks = RunFunctionalChecks(),
            LayeringChecks = RunLayeringChecks()
        };

        var allPassed = report.BuildResult.Passed
            && report.FunctionalChecks.All(check => check.Passed)
            && report.LayeringChecks.All(check => check.Passed);

        var buildOrFunctionalFailed = !report.BuildResult.Passed
            || report.FunctionalChecks.Any(check => !check.Passed);

        // The deterministic verdict, not an LLM's opinion, decides Pass/Fail/Needs Review:
        // - Pass only if the real build succeeded AND every functional check AND every layering check passed.
        // - Fail if the build itself failed, or behavior didn't match expected ground-truth results.
        // - Needs Review only for non-blocking layering/style issues when build + behavior are sound.
        report.OverallStatus = allPassed
            ? "Pass"
            : buildOrFunctionalFailed
                ? "Fail"
                : "Needs Review";

        return report;
    }

    private async Task<BuildCheckResult> RunBuildAsync(CancellationToken cancellationToken)
    {
        var csprojFiles = Directory.GetFiles(_environment.ContentRootPath, "*.csproj", SearchOption.TopDirectoryOnly);

        if (csprojFiles.Length == 0)
        {
            return new BuildCheckResult
            {
                Passed = false,
                ExitCode = -1,
                Output = "No .csproj file was found under the application content root, so the build could not run."
            };
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"build \"{csprojFiles[0]}\" --nologo -v minimal",
            WorkingDirectory = _environment.ContentRootPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var process = Process.Start(startInfo);

            if (process is null)
            {
                return new BuildCheckResult
                {
                    Passed = false,
                    ExitCode = -1,
                    Output = "The dotnet build process could not be started."
                };
            }

            var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            var stdOut = await stdOutTask;
            var stdErr = await stdErrTask;
            var combined = string.Join(
                "\n",
                new[] { stdOut, stdErr }.Where(text => !string.IsNullOrWhiteSpace(text)));

            return new BuildCheckResult
            {
                Passed = process.ExitCode == 0,
                ExitCode = process.ExitCode,
                Output = combined.Length > 4000 ? combined[^4000..] : combined
            };
        }
        catch (Exception ex)
        {
            return new BuildCheckResult
            {
                Passed = false,
                ExitCode = -1,
                Output = $"The build could not run: {ex.GetType().Name}: {ex.Message}"
            };
        }
    }

    private static List<FunctionalCheckResult> RunFunctionalChecks()
    {
        var results = new List<FunctionalCheckResult>();

        // Fresh repository/service instances so this never touches real app state.
        var repository = new CustomerRepository();
        var service = new CustomerService(repository);

        results.Add(RunCheckSafely(
            "Search valid customer 10001 returns ACTIVE status",
            () =>
            {
                var customer = service.SearchCustomerAsync("10001").GetAwaiter().GetResult();
                var passed = customer.Status == "ACTIVE" && customer.Name == "JOHN SMITH";
                return (passed,
                    "CustomerId=10001, Name=JOHN SMITH, Status=ACTIVE",
                    $"CustomerId={customer.CustomerId}, Name={customer.Name}, Status={customer.Status}");
            }));

        results.Add(RunCheckSafely(
            "Search valid customer 10003 returns CLOSED status",
            () =>
            {
                var customer = service.SearchCustomerAsync("10003").GetAwaiter().GetResult();
                return (customer.Status == "CLOSED",
                    "CustomerId=10003, Status=CLOSED",
                    $"CustomerId={customer.CustomerId}, Status={customer.Status}");
            }));

        results.Add(RunCheckSafely(
            "Searching an unknown customer ID produces a clear error",
            () =>
            {
                try
                {
                    service.SearchCustomerAsync("99999").GetAwaiter().GetResult();
                    return (false, "InvalidOperationException containing 'not found'", "No exception was thrown.");
                }
                catch (InvalidOperationException ex)
                {
                    var passed = ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase);
                    return (passed, "InvalidOperationException containing 'not found'", ex.Message);
                }
            }));

        results.Add(RunCheckSafely(
            "Updating customer 10001 status to CLOSED preserves the old status and applies the new one",
            () =>
            {
                var result = service.UpdateCustomerStatusAsync("10001", "CLOSED").GetAwaiter().GetResult();
                var passed = result.OldStatus == "ACTIVE"
                    && result.NewStatus == "CLOSED"
                    && result.Customer.Status == "CLOSED";
                return (passed,
                    "OldStatus=ACTIVE, NewStatus=CLOSED, Customer.Status=CLOSED",
                    $"OldStatus={result.OldStatus}, NewStatus={result.NewStatus}, Customer.Status={result.Customer.Status}");
            }));

        return results;
    }

    private static FunctionalCheckResult RunCheckSafely(string name, Func<(bool Passed, string Expected, string Actual)> check)
    {
        try
        {
            var (passed, expected, actual) = check();
            return new FunctionalCheckResult { Name = name, Passed = passed, Expected = expected, Actual = actual };
        }
        catch (Exception ex)
        {
            return new FunctionalCheckResult
            {
                Name = name,
                Passed = false,
                Expected = "No exception",
                Actual = $"{ex.GetType().Name}: {ex.Message}"
            };
        }
    }

    private List<LayeringCheckResult> RunLayeringChecks()
    {
        // Scope this to the converted Customer transaction only — not the migration tool's own
        // dashboard pages under Views/MigrationWorkflow, which aren't part of the migrated output.
        var customerViewsPath = Path.Combine(_environment.ContentRootPath, "Views", "Customer");
        var controllersPath = Path.Combine(_environment.ContentRootPath, "Controllers");

        return
        [
            CheckViewsForBusinessLogic(customerViewsPath),
            CheckControllersForDirectDataAccess(controllersPath),
            CheckConfirmationShowsOldAndNewStatus(customerViewsPath)
        ];
    }

    private static LayeringCheckResult CheckViewsForBusinessLogic(string viewsPath)
    {
        const string name = "Razor views contain no embedded business logic or direct data access";

        if (!Directory.Exists(viewsPath))
        {
            return new LayeringCheckResult { Name = name, Passed = false, Detail = "The Views/Customer folder was not found." };
        }

        // Require actual SQL-statement shapes (e.g. "UPDATE x SET", "SELECT ... FROM") rather than
        // bare keywords, so plain English UI text like "Update Confirmation" doesn't false-positive.
        var forbiddenPatterns = new[]
        {
            new Regex(@"\bCustomerRepository\b", RegexOptions.IgnoreCase),
            new Regex(@"new\s+SqlConnection", RegexOptions.IgnoreCase),
            new Regex(@"\bSELECT\b[\s\S]{0,200}\bFROM\b", RegexOptions.IgnoreCase),
            new Regex(@"\bUPDATE\b[\s\S]{0,100}\bSET\b", RegexOptions.IgnoreCase),
            new Regex(@"\bINSERT\s+INTO\b", RegexOptions.IgnoreCase),
            new Regex(@"\bDELETE\s+FROM\b", RegexOptions.IgnoreCase)
        };

        var offenders = Directory.GetFiles(viewsPath, "*.cshtml", SearchOption.AllDirectories)
            .Where(file => forbiddenPatterns.Any(pattern => pattern.IsMatch(File.ReadAllText(file))))
            .Select(Path.GetFileName)
            .ToList();

        return new LayeringCheckResult
        {
            Name = name,
            Passed = offenders.Count == 0,
            Detail = offenders.Count == 0
                ? "No forbidden patterns (repository access, raw SQL statements) were found in any Customer view."
                : $"Forbidden patterns were found in: {string.Join(", ", offenders)}"
        };
    }

    private static LayeringCheckResult CheckControllersForDirectDataAccess(string controllersPath)
    {
        const string name = "Controllers delegate to services instead of accessing data directly";

        // Only the converted transaction's controller is in scope here.
        var customerControllerPath = Path.Combine(controllersPath, "CustomerController.cs");

        if (!File.Exists(customerControllerPath))
        {
            return new LayeringCheckResult { Name = name, Passed = false, Detail = "CustomerController.cs was not found." };
        }

        var forbiddenPatterns = new[]
        {
            new Regex(@"new\s+SqlConnection", RegexOptions.IgnoreCase),
            new Regex(@"\bSELECT\b[\s\S]{0,200}\bFROM\b", RegexOptions.IgnoreCase),
            new Regex(@"_customers\s*\[", RegexOptions.IgnoreCase)
        };

        var content = File.ReadAllText(customerControllerPath);
        var matched = forbiddenPatterns.Where(pattern => pattern.IsMatch(content)).ToList();

        return new LayeringCheckResult
        {
            Name = name,
            Passed = matched.Count == 0,
            Detail = matched.Count == 0
                ? "No direct data-access patterns were found in CustomerController.cs."
                : "CustomerController.cs appears to access data directly instead of delegating to CustomerService."
        };
    }

    private static LayeringCheckResult CheckConfirmationShowsOldAndNewStatus(string viewsPath)
    {
        const string name = "Confirmation screen displays the old and new status";

        var confirmationFile = Directory.Exists(viewsPath)
            ? Directory.GetFiles(viewsPath, "Confirmation.cshtml", SearchOption.AllDirectories).FirstOrDefault()
            : null;

        if (confirmationFile is null)
        {
            return new LayeringCheckResult { Name = name, Passed = false, Detail = "Confirmation.cshtml was not found." };
        }

        var content = File.ReadAllText(confirmationFile);
        var hasOld = content.Contains("OldStatus", StringComparison.OrdinalIgnoreCase);
        var hasNew = content.Contains("NewStatus", StringComparison.OrdinalIgnoreCase);

        return new LayeringCheckResult
        {
            Name = name,
            Passed = hasOld && hasNew,
            Detail = $"OldStatus referenced: {hasOld}. NewStatus referenced: {hasNew}."
        };
    }
}
