# Test Cases

| ID | Test Case | Steps | Test Data | Expected Result |
| --- | --- | --- | --- | --- |
| TC-001 | Search valid customer 10001 | Open search screen. Enter customer ID. Click Search. | `10001` | Details screen displays JOHN SMITH, ACTIVE, 2500.75. |
| TC-002 | Search valid customer 10002 | Open search screen. Enter customer ID. Click Search. | `10002` | Details screen displays MARY JONES, ACTIVE, 5100.00. |
| TC-003 | Search valid customer 10003 | Open search screen. Enter customer ID. Click Search. | `10003` | Details screen displays DAVID BROWN, CLOSED, 0.00. |
| TC-004 | Search blank customer ID | Open search screen. Clear customer ID. Click Search. | Blank | Error message says customer ID is required. |
| TC-005 | Search unknown customer ID | Open search screen. Enter unknown ID. Click Search. | `99999` | Error message says customer not found. |
| TC-006 | Update status to CLOSED | Search `10001`. Enter `CLOSED`. Click Update. | `10001`, `CLOSED` | Confirmation displays old status ACTIVE and new status CLOSED. |
| TC-007 | Update status to ACTIVE | Search `10003`. Enter `ACTIVE`. Click Update. | `10003`, `ACTIVE` | Confirmation displays old status CLOSED and new status ACTIVE. |
| TC-008 | Update blank status | Search valid customer. Clear status. Click Update. | Blank status | Error message says new status is required. |
| TC-009 | Status normalization | Search `10001`. Enter `closed`. Click Update. | `closed` | Confirmation displays new status CLOSED. |
| TC-010 | New search after confirmation | Complete an update. Click New Search. | Any valid customer | Search screen is available for another transaction. |
| TC-011 | PL/I search screen parity | Compare `CUSTSRCH` with .NET search view. | N/A | Customer ID input and search action are represented. |
| TC-012 | PL/I details screen parity | Compare `CUSTDTL` with .NET details view. | N/A | Customer fields and update action are represented. |
| TC-013 | PL/I confirmation screen parity | Compare `CUSTCONF` with .NET confirmation view. | N/A | Confirmation result and new search action are represented. |

