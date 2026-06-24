# Technical Design Document

## 1. Purpose

This document describes the proposed design for converting the legacy PL/I/CICS customer maintenance transaction into an ASP.NET Core MVC application.

## 2. Legacy-to-Target Mapping

| Legacy Component | .NET Component |
| --- | --- |
| CICS transaction `CUST` | MVC route `/Customer/Search` |
| PL/I program `CUSTPGM` | `CustomerController` and `CustomerService` |
| BMS map `CUSTSRCH` | `Views/Customer/Search.cshtml` |
| BMS map `CUSTDTL` | `Views/Customer/Details.cshtml` |
| BMS map `CUSTCONF` | `Views/Customer/Confirmation.cshtml` |
| DB2/VSAM access | `CustomerRepository` |
| Screen fields / COMMAREA | C# models and action parameters |
| PF keys | HTML buttons and links |

## 3. Target Architecture

The converted application uses a layered MVC structure:

```text
Razor Views
    |
CustomerController
    |
CustomerService
    |
CustomerRepository
    |
Data Source
```

## 4. Components

| Component | Responsibility |
| --- | --- |
| `CustomerController` | Handles web requests and screen navigation. |
| `CustomerService` | Contains business validation and update behavior. |
| `CustomerRepository` | Encapsulates customer data access. |
| `Customer` | Represents customer data. |
| `CustomerUpdateResult` | Represents old/new status confirmation result. |
| Razor views | Render search, details, and confirmation screens. |

## 5. Data Flow

### Search

1. User enters customer ID.
2. Browser posts to `CustomerController.Details`.
3. Controller calls `CustomerService.SearchCustomerAsync`.
4. Service validates input and calls repository.
5. Details view is rendered for valid customer.

### Update

1. User enters new status.
2. Browser posts customer ID and new status to `CustomerController.Update`.
3. Service validates status.
4. Repository updates customer status.
5. Confirmation view displays old and new status.

## 6. Validation

- Customer ID is required.
- New status is required.
- Unknown customer ID returns a user-facing error.
- New status is normalized to uppercase.

## 7. Error Handling

Business validation errors are handled by `InvalidOperationException` and rendered back to the search screen with a clear message. Production implementation should replace this with typed validation results or a standardized error model.

## 8. Security Considerations

Future production design should include:

- Authentication
- Role-based authorization
- Audit logging for status updates
- Input validation
- CSRF protection
- Secure database connection management

## 9. Deployment Considerations

The application can be deployed as an ASP.NET Core web application to IIS, Azure App Service, containers, or another enterprise hosting platform.

