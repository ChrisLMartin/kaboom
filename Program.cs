using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Kaboom.Api;
using Kaboom.Data;
using Kaboom.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var dataProtectionPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtection-Keys");
Directory.CreateDirectory(dataProtectionPath);

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath))
    .SetApplicationName("Kaboom");

builder.Services.AddDbContextFactory<KaboomDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Kaboom")));

builder.Services.AddScoped<PostgresBudgetRepository>();
builder.Services.AddScoped<IBudgetRepository>(serviceProvider => serviceProvider.GetRequiredService<PostgresBudgetRepository>());
builder.Services.AddScoped<DatabaseBootstrapper>();
builder.Services.AddScoped<KaboomApiService>();
builder.Services.AddSingleton<BudgetCalculator>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("ClientApp", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "https://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();
var clientDistPath = Path.Combine(app.Environment.ContentRootPath, "ClientApp", "dist");
var hasClientDist = Directory.Exists(clientDistPath);
var clientFileProvider = hasClientDist ? new PhysicalFileProvider(clientDistPath) : null;

await using (var scope = app.Services.CreateAsyncScope())
{
    var bootstrapper = scope.ServiceProvider.GetRequiredService<DatabaseBootstrapper>();
    await bootstrapper.InitializeAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { message = "An unexpected error occurred." });
        });
    });
    app.UseHsts();
}

app.UseHttpsRedirection();
if (clientFileProvider is not null)
{
    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = clientFileProvider
    });

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = clientFileProvider
    });
}

app.UseStaticFiles();
app.UseRouting();
app.UseCors("ClientApp");
app.MapKaboomApi();

if (clientFileProvider is not null)
{
    app.MapFallbackToFile("{*path:nonfile}", "index.html", new StaticFileOptions
    {
        FileProvider = clientFileProvider
    });
}

app.Run();
