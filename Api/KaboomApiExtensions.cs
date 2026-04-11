using Kaboom.Services;

namespace Kaboom.Api;

public static class KaboomApiExtensions
{
    public static RouteGroupBuilder MapKaboomApi(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api");

        api.MapGet("/accounts", async (KaboomApiService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetAccountsAsync(cancellationToken)));

        api.MapPost("/accounts", async (AccountUpsertRequest request, KaboomApiService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.AddAccountAsync(request, cancellationToken)));

        api.MapPut("/accounts/{accountId}", async (string accountId, AccountUpsertRequest request, KaboomApiService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.UpdateAccountAsync(accountId, request, cancellationToken)));

        api.MapDelete("/accounts/{accountId}", async (string accountId, KaboomApiService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.DeleteAccountAsync(accountId, cancellationToken)));

        api.MapGet("/categories", async (KaboomApiService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetCategoryGroupsAsync(cancellationToken)));

        api.MapPost("/categories", async (AddCategoryRequest request, KaboomApiService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.AddCategoryAsync(request, cancellationToken)));

        api.MapPatch("/category-groups/{groupId}", async (string groupId, RenameEntityRequest request, KaboomApiService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.RenameCategoryGroupAsync(groupId, request, cancellationToken)));

        api.MapPatch("/categories/{categoryId}", async (string categoryId, RenameEntityRequest request, KaboomApiService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.RenameCategoryAsync(categoryId, request, cancellationToken)));

        api.MapGet("/budget", async (string? month, int? months, KaboomApiService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetBudgetPageAsync(month, months ?? 3, cancellationToken)));

        api.MapPost("/budget/allocations", async (SaveBudgetRequest request, KaboomApiService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.SaveBudgetAsync(request, cancellationToken)));

        api.MapGet("/transactions", async (KaboomApiService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetTransactionsPageAsync(cancellationToken)));

        api.MapPost("/transactions", async (TransactionUpsertRequest request, KaboomApiService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.AddTransactionAsync(request, cancellationToken)));

        api.MapPut("/transactions/{transactionId}", async (string transactionId, TransactionUpsertRequest request, KaboomApiService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.UpdateTransactionAsync(transactionId, request, cancellationToken)));

        api.MapGet("/reports", async (string? month, KaboomApiService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetReportsAsync(month, cancellationToken)));

        return api;
    }

    private static async Task<IResult> ExecuteAsync<T>(Func<Task<T>> callback)
    {
        try
        {
            return Results.Ok(await callback());
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["request"] = [exception.Message]
            });
        }
        catch (KeyNotFoundException exception)
        {
            return Results.NotFound(new { message = exception.Message });
        }
    }
}
