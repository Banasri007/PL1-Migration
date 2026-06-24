# PL/I to .NET Migration Agent Workflow

## Purpose

This workflow defines the agents required to reverse engineer the legacy PL/I transaction and produce the initial migration deliverables:

1. Business Requirement Document (BRD)
2. Test Plan
3. Technical Design Document
4. Converted Code
5. Test Cases

Each producer agent has a matching checkpoint agent that verifies the output before it moves to the next stage.

## End-to-End Flow

```text
Legacy PL/I Code and Screens
        |
        v
BRD Producer Agent
        |
        v
BRD Checkpoint Agent
        |
        v
Technical Design Producer Agent
        |
        v
Technical Design Checkpoint Agent
        |
        v
Converted Code Agent
        |
        v
Code Checkpoint Agent
        |
        v
Test Plan Producer Agent
        |
        v
Test Plan Checkpoint Agent
        |
        v
Test Case Producer Agent
        |
        v
Test Case Checkpoint Agent
        |
        v
Final Migration Readiness Review
```

## Shared Inputs

- Legacy PL/I screen files: `PL1-Screens/`
- Converted .NET MVC app: `Controllers/`, `Services/`, `Data/`, `Models/`, `Views/`
- Visual comparison page: `pl1-to-dotnet-screens.html`
- Existing README: `README.md`

## Shared Output Folder

All migration deliverables should be maintained under:

```text
migration-workflow/deliverables/
```

## Agent 1: BRD Producer Agent

### Objective

Reverse engineer business requirements from the legacy PL/I/CICS transaction.

### Inputs

- `PL1-Screens/CUSTSRCH.pli`
- `PL1-Screens/CUSTDTL.pli`
- `PL1-Screens/CUSTCONF.pli`
- Current .NET customer transaction flow

### Output

- `deliverables/BRD.md`

### Responsibilities

- Identify business purpose of the transaction.
- Document actors and user roles.
- Document current mainframe behavior.
- Document target .NET behavior.
- Identify functional requirements.
- Identify business rules and validations.
- Identify data elements.
- Define acceptance criteria.

### Handoff To

BRD Checkpoint Agent.

## Agent 1A: BRD Checkpoint Agent

### Objective

Validate that the BRD accurately reflects the legacy PL/I transaction and is complete enough for design and development.

### Verification Checklist

- Business objective is clearly stated.
- Each PL/I screen is represented in the requirements.
- Search, detail display, update, and confirmation behavior are covered.
- Data fields are listed.
- Functional requirements are testable.
- Acceptance criteria are measurable.
- Assumptions and out-of-scope items are documented.

### Output

- `deliverables/BRD-checkpoint.md`

## Agent 2: Technical Design Producer Agent

### Objective

Create the proposed .NET application design based on the approved BRD.

### Inputs

- Approved BRD
- Current converted .NET code
- PL/I-to-.NET component mapping

### Output

- `deliverables/Technical-Design.md`

### Responsibilities

- Define target architecture.
- Map PL/I/CICS components to .NET components.
- Describe MVC routes, controllers, services, models, and repository.
- Describe data flow for search and update.
- Define validation and error handling.
- Define security, logging, deployment, and extensibility considerations.

### Handoff To

Technical Design Checkpoint Agent.

## Agent 2A: Technical Design Checkpoint Agent

### Objective

Validate that the technical design is implementable and aligns with the BRD.

### Verification Checklist

- Every BRD requirement maps to a technical component.
- Architecture separates UI, business logic, and data access.
- Data flow is clear for all screens.
- Error paths are documented.
- Security and audit considerations are included.
- Design is suitable for future database replacement.

### Output

- `deliverables/Technical-Design-checkpoint.md`

## Agent 3: Converted Code Agent

### Objective

Implement or maintain the converted .NET application.

### Inputs

- Approved BRD
- Approved technical design
- Existing converted application

### Output

- Updated .NET code under the application folders.
- `deliverables/Converted-Code-Summary.md`

### Responsibilities

- Implement the target transaction flow.
- Maintain controller, service, model, repository, and Razor view layers.
- Preserve functional parity with the legacy transaction.
- Keep code simple and aligned with the technical design.
- Document key implementation choices.

### Handoff To

Code Checkpoint Agent.

## Agent 3A: Code Checkpoint Agent

### Objective

Validate converted code quality and functional completeness.

### Verification Checklist

- Application builds successfully.
- Customer search works for valid IDs.
- Invalid search produces a clear error.
- Status update works.
- Confirmation shows old and new status.
- Code follows controller-service-repository separation.
- No business logic is hidden in Razor views.
- Converted behavior matches the PL/I transaction flow.

### Output

- `deliverables/Code-checkpoint.md`

## Agent 4: Test Plan Producer Agent

### Objective

Create a test strategy for validating the converted application.

### Inputs

- Approved BRD
- Approved technical design
- Converted code summary

### Output

- `deliverables/Test-Plan.md`

### Responsibilities

- Define test scope.
- Define test levels.
- Define functional, regression, migration parity, UI, negative, and UAT coverage.
- Define environments and test data.
- Define entry and exit criteria.
- Define defect management process.
- Define risks and mitigations.

### Handoff To

Test Plan Checkpoint Agent.

## Agent 4A: Test Plan Checkpoint Agent

### Objective

Validate that the test plan is complete and traceable to requirements.

### Verification Checklist

- Test scope covers all converted screens.
- Test levels are defined.
- Migration parity testing is included.
- Negative testing is included.
- Test data is identified.
- Entry and exit criteria are measurable.
- Risks and mitigations are documented.

### Output

- `deliverables/Test-Plan-checkpoint.md`

## Agent 5: Test Case Producer Agent

### Objective

Create executable test cases for the converted application.

### Inputs

- Approved BRD
- Approved test plan
- Converted application

### Output

- `deliverables/Test-Cases.md`

### Responsibilities

- Create detailed test cases with IDs.
- Include preconditions, test data, steps, and expected results.
- Cover positive, negative, regression, UI, and migration parity scenarios.
- Ensure each requirement has at least one test case.

### Handoff To

Test Case Checkpoint Agent.

## Agent 5A: Test Case Checkpoint Agent

### Objective

Validate that test cases are complete, executable, and traceable.

### Verification Checklist

- Each test case has clear steps and expected results.
- Valid and invalid customer search scenarios are covered.
- Status update scenarios are covered.
- Confirmation scenarios are covered.
- UI and parity scenarios are covered.
- Test cases map back to BRD requirements.

### Output

- `deliverables/Test-Cases-checkpoint.md`

## Final Migration Readiness Review

The final review confirms that:

- BRD is approved.
- Technical design is approved.
- Converted code builds and runs.
- Test plan is approved.
- Test cases are ready for execution.
- All checkpoint findings are closed or accepted.

