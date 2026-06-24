namespace Pl1MigrationDemo.Models;

public class CustomerUpdateResult
{
    public Customer Customer { get; set; } = new();
    public string OldStatus { get; set; } = "";
    public string NewStatus { get; set; } = "";
}
