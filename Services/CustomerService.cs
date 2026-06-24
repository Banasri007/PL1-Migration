using Pl1MigrationDemo.Data;
using Pl1MigrationDemo.Models;

namespace Pl1MigrationDemo.Services;

public class CustomerService
{
    private readonly CustomerRepository _repository;

    public CustomerService(CustomerRepository repository)
    {
        _repository = repository;
    }

    public async Task<Customer> SearchCustomerAsync(string customerId)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            throw new InvalidOperationException("Customer ID is required.");
        }

        var customer = await _repository.GetCustomerAsync(customerId);

        return customer ?? throw new InvalidOperationException("Customer not found.");
    }

    public async Task<CustomerUpdateResult> UpdateCustomerStatusAsync(string customerId, string newStatus)
    {
        if (string.IsNullOrWhiteSpace(newStatus))
        {
            throw new InvalidOperationException("New status is required.");
        }

        var customer = await SearchCustomerAsync(customerId);
        var oldStatus = customer.Status;
        var normalizedStatus = newStatus.Trim().ToUpperInvariant();

        await _repository.UpdateStatusAsync(customerId, normalizedStatus);

        customer.Status = normalizedStatus;

        return new CustomerUpdateResult
        {
            Customer = customer,
            OldStatus = oldStatus,
            NewStatus = normalizedStatus
        };
    }
}
