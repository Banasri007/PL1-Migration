# Business Requirement Document

## 1. Business Context

The legacy PL/I/CICS transaction supports customer account maintenance. The business user searches for a customer using a customer ID, views the customer details, updates the customer status, and receives confirmation that the update was completed.

## 2. Reverse-Engineered Legacy Scope

The following PL/I/CICS-style screens were analyzed:

- `CUSTSRCH`: Customer Search
- `CUSTDTL`: Customer Details and Status Update
- `CUSTCONF`: Update Confirmation

## 3. Business Actors

- Customer service user: Searches customer records and updates customer status.
- Operations/support user: Validates migrated behavior and investigates defects.
- Business approver: Confirms that the converted .NET behavior matches the legacy transaction.

## 4. Current-State Legacy Flow

1. User starts transaction `CUST`.
2. System displays `CUSTSRCH`.
3. User enters customer ID.
4. PL/I program retrieves customer details.
5. System displays `CUSTDTL`.
6. User enters a new status.
7. PL/I program updates the record.
8. System displays `CUSTCONF`.

## 5. Target-State .NET Flow

1. User opens the Customer Search screen.
2. User enters customer ID.
3. System retrieves and displays customer details when the customer exists.
4. User enters a new customer status.
5. System updates the customer status.
6. System shows an update confirmation with old status, new status, and success message.

## 6. Functional Requirements

| ID | Requirement |
| --- | --- |
| BRD-FR-001 | The system shall allow a user to search for a customer by customer ID. |
| BRD-FR-002 | The system shall display customer ID, name, status, and balance for a valid customer. |
| BRD-FR-003 | The system shall display an error message when the customer ID is missing. |
| BRD-FR-004 | The system shall display an error message when the customer ID is not found. |
| BRD-FR-005 | The system shall allow the user to update customer status. |
| BRD-FR-006 | The system shall require New Status before allowing update. |
| BRD-FR-007 | The system shall display a confirmation screen after successful update. |
| BRD-FR-008 | The confirmation screen shall show customer ID, old status, new status, and update success message. |
| BRD-FR-009 | The Customer Search screen shall provide an equivalent action for Enter=Search. |
| BRD-FR-010 | The Customer Search screen shall provide an equivalent action for PF3=Exit or Clear. |
| BRD-FR-011 | The Customer Details screen shall provide an equivalent action for PF3=Back. |
| BRD-FR-012 | The Customer Details screen shall provide an equivalent action for PF5=Update. |
| BRD-FR-013 | The Confirmation screen shall provide an equivalent action for Enter=New Search. |
| BRD-FR-014 | The Confirmation screen shall provide an equivalent action for PF3=Exit. |

## 7. Data Elements

| Field | Description | Source |
| --- | --- | --- |
| Customer ID | Unique customer identifier | User input |
| Name | Customer name | Customer record |
| Status | Current customer status | Customer record |
| Balance | Customer balance | Customer record |
| New Status | Replacement status value | User input |
| Old Status | Customer status before update | Customer record |
| Success Message | Message confirming update completion | System generated |

## 8. Business Rules

| ID | Rule |
| --- | --- |
| BRD-BR-001 | Customer ID is mandatory for search. |
| BRD-BR-002 | A valid Customer ID must match an existing customer record. |
| BRD-BR-003 | New Status is mandatory for update. |
| BRD-BR-004 | Customer Name and Balance are display-only in this transaction. |
| BRD-BR-005 | The system shall retain and display Old Status on the confirmation screen. |
| BRD-BR-006 | The system shall display `Update completed successfully.` after a successful update. |

## 9. Acceptance Criteria

- User can search customer `10001` and see `JOHN SMITH`.
- User can search customer `10002` and see `MARY JONES`.
- User can search customer `10003` and see `DAVID BROWN`.
- User receives an error for blank customer ID.
- User receives an error for an unknown customer ID.
- User receives an error for blank New Status.
- User can update status from `ACTIVE` to `CLOSED`.
- Confirmation displays customer ID, old status, new status, and `Update completed successfully.`
- User can start a new search from confirmation.

## 10. Modernization Decisions And Assumptions

- The sample customer records are demo-only and do not represent a complete production data population.
- Displaying customer name on the .NET confirmation screen is a modernization enhancement. The reverse-engineered PL/I confirmation screen shows customer ID, old status, new status, and success message.
- Uppercase normalization of New Status is implemented in the .NET demo. It should be treated as a modernization assumption unless confirmed from full legacy PL/I business logic beyond the screen files.
- In production, the data access implementation will be replaced by DB2, SQL Server, an API, or another enterprise data source.
- Authentication and authorization are outside the current demo scope.
