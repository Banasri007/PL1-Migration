using Pl1MigrationDemo.Models;

namespace Pl1MigrationDemo.Data;

public class CustomerRepository
{
    private readonly Dictionary<string, Customer> _customers = new()
    {
        ["10001"] = new Customer
        {
            CustomerId = "10001",
            Name = "JOHN SMITH",
            Status = "ACTIVE",
            Balance = 2500.75m
        },
        ["10002"] = new Customer
        {
            CustomerId = "10002",
            Name = "MARY JONES",
            Status = "ACTIVE",
            Balance = 5100.00m
        },
        ["10003"] = new Customer
        {
            CustomerId = "10003",
            Name = "DAVID BROWN",
            Status = "CLOSED",
            Balance = 0.00m
        }
    };

    public Task<Customer?> GetCustomerAsync(string customerId)
    {
        _customers.TryGetValue(customerId.Trim(), out var customer);

        if (customer is null)
        {
            return Task.FromResult<Customer?>(null);
        }

        return Task.FromResult<Customer?>(new Customer
        {
            CustomerId = customer.CustomerId,
            Name = customer.Name,
            Status = customer.Status,
            Balance = customer.Balance
        });
    }

    public Task UpdateStatusAsync(string customerId, string newStatus)
    {
        var key = customerId.Trim();

        if (_customers.TryGetValue(key, out var customer))
        {
            customer.Status = newStatus.Trim().ToUpperInvariant();
        }

        return Task.CompletedTask;
    }
}
