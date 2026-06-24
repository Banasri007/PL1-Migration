using Microsoft.AspNetCore.Mvc;
using Pl1MigrationDemo.Services;

namespace Pl1MigrationDemo.Controllers;

public class CustomerController : Controller
{
    private readonly CustomerService _service;

    public CustomerController(CustomerService service)
    {
        _service = service;
    }

    [HttpGet]
    public IActionResult Search()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Details(string customerId)
    {
        try
        {
            var customer = await _service.SearchCustomerAsync(customerId);
            return View(customer);
        }
        catch (InvalidOperationException ex)
        {
            ViewBag.ErrorMessage = ex.Message;
            return View("Search");
        }
    }

    [HttpPost]
    public async Task<IActionResult> Update(string customerId, string newStatus)
    {
        try
        {
            var customer = await _service.UpdateCustomerStatusAsync(customerId, newStatus);
            return View("Confirmation", customer);
        }
        catch (InvalidOperationException ex)
        {
            ViewBag.ErrorMessage = ex.Message;
            return View("Search");
        }
    }
}
