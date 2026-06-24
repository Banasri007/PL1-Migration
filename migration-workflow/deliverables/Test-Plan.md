# Test Plan

## 1. Objective

Validate that the converted .NET customer maintenance transaction matches the legacy PL/I transaction behavior.

## 2. Scope

In scope:

- Customer search
- Customer details display
- Status update
- Confirmation screen
- Error handling
- PL/I-to-.NET screen parity

Out of scope for demo:

- Authentication
- Authorization
- Real database integration
- Performance testing at production scale

## 3. Test Levels

| Level | Purpose |
| --- | --- |
| Unit testing | Validate service and repository behavior. |
| Functional testing | Validate end-to-end screen behavior. |
| Regression testing | Confirm existing behavior remains stable after changes. |
| Migration parity testing | Compare PL/I screen behavior with .NET behavior. |
| UAT | Business users confirm the converted flow. |

## 4. Test Data

| Customer ID | Name | Initial Status | Balance |
| --- | --- | --- | --- |
| 10001 | JOHN SMITH | ACTIVE | 2500.75 |
| 10002 | MARY JONES | ACTIVE | 5100.00 |
| 10003 | DAVID BROWN | CLOSED | 0.00 |

## 5. Entry Criteria

- Application builds successfully.
- Screens are available.
- Test data is loaded.
- BRD and technical design are baselined.

## 6. Exit Criteria

- All critical test cases pass.
- No open high-severity defects.
- Business owner signs off on migrated behavior.
- Known limitations are documented.

## 7. Risks

| Risk | Mitigation |
| --- | --- |
| Legacy behavior misunderstood | Use screen parity review and business walkthroughs. |
| Data rules missing | Validate with SMEs and production samples. |
| UI differs from mainframe expectations | Use side-by-side comparison during UAT. |
| Repository differs from future database behavior | Add integration tests when database is introduced. |

