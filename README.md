# PL/I to .NET Transaction Demo

This is a small ASP.NET Core MVC app that simulates a three-screen PL/I/CICS customer maintenance transaction.

## Mainframe Flow

1. `CUSTSRCH`: Customer Search
2. `CUSTDTL`: Customer Details / Update
3. `CUSTCONF`: Update Confirmation

## .NET Mapping

| Mainframe concept | .NET equivalent |
| --- | --- |
| CICS transaction `CUST` | `CustomerController` |
| PL/I business logic | `CustomerService` |
| VSAM/DB2 access | `CustomerRepository` |
| BMS maps | Razor views |
| PF keys | Buttons and links |

## Run

Install the .NET 8 SDK first if `dotnet --version` says no SDK is installed:

https://dotnet.microsoft.com/download/dotnet/8.0

Then run:

```powershell
cd Pl1MigrationDemo
dotnet run
```

## Enable Real Local Ollama Agents

The migration workflow uses Ollama by default, so no cloud API key is required.

Install Ollama, then pull a model:

```powershell
ollama pull llama3.2
```

Run the app:

```powershell
cd Pl1MigrationDemo
$env:OLLAMA_BASE_URL="http://localhost:11434"
$env:OLLAMA_MODEL="qwen 2.5"
dotnet run --urls http://localhost:5000
```

Open:

```text
http://localhost:5000/MigrationWorkflow
```

If Ollama is not running or the model is missing, the live run panel will show a checkpoint error with the command to fix it.

Each run calls the configured LLM for producer and checkpoint agents. Run outputs are saved under:

```text
migration-workflow\runs
```

By default, the app uses a single Ollama call per workflow to reduce local compute usage. To force one call per agent, use:

```powershell
$env:AGENT_WORKFLOW_MODE="multi"
```

Open the URL printed by `dotnet run`, or go to:

```text
http://localhost:5000/Customer/Search
```

Try these customer IDs:

- `10001`
- `10002`
- `10003`

## See PL/I and .NET Screens Side by Side

Open this file directly in a browser:

```text
Open pl1-to-dotnet-screens.html directly in a browser from the project root folder.
```

The left side shows the original PL/I/CICS-style screen. The right side shows the converted .NET MVC screen.

## VS Code

Open this folder in VS Code:

```powershell
code .
```

After the .NET SDK is installed, press `F5` and choose `Run PL/I Migration Demo`.
