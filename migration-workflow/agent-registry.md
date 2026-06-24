# Agent Registry

## Producer Agents Started

| Area | Agent Nickname | Agent ID | Status |
| --- | --- | --- | --- |
| BRD Producer | Boole | `019ed99c-808a-7050-8506-0446f36059b1` | Completed |
| Test Plan Producer | Leibniz | `019ed99c-a8e9-7751-81bf-acd16a526e08` | Completed |
| Technical Design Producer | Maxwell | `019ed99c-d20e-75b2-95eb-923a85cf5852` | Completed |
| Converted Code Review | Turing | `019ed99c-ee5f-7b22-a61c-4735d780974d` | Completed |
| Test Case Producer | Avicenna | `019ed99d-186e-7932-a946-042f76b542e9` | Completed |

## Checkpoint Agents

| Area | Agent Nickname | Agent ID | Status |
| --- | --- | --- | --- |
| BRD Checkpoint | Pauli | `019ed99f-454a-7d72-82cc-76f4362a258a` | Started |
| Technical Design Checkpoint | To be launched after thread slot is available | TBD | Queued |
| Code Checkpoint | To be launched after thread slot is available | TBD | Queued |
| Test Plan Checkpoint | To be launched after thread slot is available | TBD | Queued |
| Test Case Checkpoint | To be launched after thread slot is available | TBD | Queued |

## Note

The agent runner reached the active thread limit after the first checkpoint agent was created. The remaining checkpoint agents are fully defined in `agent-workflow.md` and should be launched sequentially as agent slots become available.

## Recommended Launch Order For Remaining Checkpoint Agents

1. Technical Design Checkpoint Agent
2. Code Checkpoint Agent
3. Test Plan Checkpoint Agent
4. Test Case Checkpoint Agent

## Checkpoint Prompt Templates

### Technical Design Checkpoint Agent

Review `migration-workflow/deliverables/Technical-Design.md` against the actual .NET project files. Verify component mappings, architecture, data flow, validation, error handling, and production considerations. Final output should include Pass/Fail, findings, missing items, and recommended corrections.

### Code Checkpoint Agent

Review the converted code in `Controllers/`, `Services/`, `Data/`, `Models/`, `Views/`, and `pl1-to-dotnet-screens.html`. Verify search by customer ID, status update, old/new status confirmation, layering, and build readiness. Final output should include Pass/Fail, findings, missing items, and recommended corrections.

### Test Plan Checkpoint Agent

Review `migration-workflow/deliverables/Test-Plan.md`. Verify it covers test scope, levels, environments, entry/exit criteria, risks, regression, UAT, and migration parity. Final output should include Pass/Fail, findings, missing items, and recommended corrections.

### Test Case Checkpoint Agent

Review `migration-workflow/deliverables/Test-Cases.md` against the BRD and application behavior. Verify executable steps, correct test data, expected messages, positive/negative/regression/UI/parity coverage, and traceability. Final output should include Pass/Fail, findings, missing items, and recommended corrections.

