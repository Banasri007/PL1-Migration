using Pl1MigrationDemo.Data;
using Pl1MigrationDemo.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<CustomerRepository>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddHttpClient<ILlmClient, OllamaLlmClient>()
    .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromMinutes(10));
builder.Services.AddScoped<IAgentWorkflowEngine, LlmAgentWorkflowEngine>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(
    name: "migrationWorkflow",
    pattern: "MigrationWorkflow/{action=Index}/{file?}",
    defaults: new { controller = "MigrationWorkflow" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Customer}/{action=Search}/{id?}");

app.Run();
