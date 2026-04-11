using Spendwise.Services;

namespace Spendwise.Api;

public static class SpendwiseApiExtensions
{
    public static RouteGroupBuilder MapSpendwiseApi(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api");

        api.MapGet("/accounts", async (SpendwiseApiService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetAccountsAsync(cancellationToken)));

        api.MapPost("/accounts", async (AccountUpsertRequest request, SpendwiseApiService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.AddAccountAsync(request, cancellationToken)));

        api.MapPut("/accounts/{accountId}", async (string accountId, AccountUpsertRequest request, SpendwiseApiService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.UpdateAccountAsync(accountId, request, cancellationToken)));

        api.MapGet("/categories", async (SpendwiseApiService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetCategoryGroupsAsync(cancellationToken)));

        api.MapPost("/categories", async (AddCategoryRequest request, SpendwiseApiService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.AddCategoryAsync(request, cancellationToken)));

        api.MapPatch("/category-groups/{groupId}", async (string groupId, RenameEntityRequest request, SpendwiseApiService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.RenameCategoryGroupAsync(groupId, request, cancellationToken)));

        api.MapPatch("/categories/{categoryId}", async (string categoryId, RenameEntityRequest request, SpendwiseApiService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.RenameCategoryAsync(categoryId, request, cancellationToken)));

        api.MapGet("/budget", async (string? month, int? months, SpendwiseApiService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetBudgetPageAsync(month, months ?? 3, cancellationToken)));

        api.MapPost("/budget/allocations", async (SaveBudgetRequest request, SpendwiseApiService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.SaveBudgetAsync(request, cancellationToken)));

        api.MapGet("/transactions", async (SpendwiseApiService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetTransactionsPageAsync(cancellationToken)));

        api.MapPost("/transactions", async (TransactionUpsertRequest request, SpendwiseApiService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.AddTransactionAsync(request, cancellationToken)));

        api.MapPut("/transactions/{transactionId}", async (string transactionId, TransactionUpsertRequest request, SpendwiseApiService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.UpdateTransactionAsync(transactionId, request, cancellationToken)));

        api.MapGet("/reports", async (string? month, SpendwiseApiService service, CancellationToken cancellationToken) =>
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
