using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Spendwise.Data;
using Spendwise.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var dataProtectionPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtection-Keys");
Directory.CreateDirectory(dataProtectionPath);

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath))
    .SetApplicationName("Spendwise");

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AddPageRoute("/Budget/Index", "");
});

builder.Services.AddDbContextFactory<SpendwiseDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Spendwise")));

builder.Services.AddScoped<PostgresBudgetRepository>();
builder.Services.AddScoped<IBudgetRepository>(serviceProvider => serviceProvider.GetRequiredService<PostgresBudgetRepository>());
builder.Services.AddScoped<DatabaseBootstrapper>();
builder.Services.AddSingleton<BudgetCalculator>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var bootstrapper = scope.ServiceProvider.GetRequiredService<DatabaseBootstrapper>();
    await bootstrapper.InitializeAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

app.Run();
