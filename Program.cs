using Spendwise.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AddPageRoute("/Budget/Index", "");
});

builder.Services.AddSingleton<IBudgetRepository, JsonBudgetRepository>();
builder.Services.AddSingleton<BudgetCalculator>();

var app = builder.Build();

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
