using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Kaboom.Data;
using Kaboom.Services;

namespace Kaboom.Api;

public static class KaboomApiExtensions
{
    public static RouteGroupBuilder MapKaboomApi(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api");
        var auth = api.MapGroup("/auth");

        auth.MapGet("/me", async (
            HttpContext httpContext,
            UserManager<ApplicationUser> userManager,
            CurrentBudgetService currentBudgetService,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            var providers = GetExternalProviders(configuration);
            if (httpContext.User.Identity?.IsAuthenticated != true)
            {
                return Results.Ok(new AuthStatusDto(false, null, providers));
            }

            var user = await userManager.GetUserAsync(httpContext.User);
            if (user is null)
            {
                return Results.Ok(new AuthStatusDto(false, null, providers));
            }

            var access = await currentBudgetService.GetOrCreateForUserAsync(user, cancellationToken);
            return Results.Ok(BuildAuthStatus(access, providers));
        });

        auth.MapPost("/register", async (
            RegisterRequest request,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            BudgetProvisioningService budgetProvisioningService,
            CurrentBudgetService currentBudgetService,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            var email = RequireValue(request.Email, "Email is required.").ToLowerInvariant();
            var password = RequireValue(request.Password, "Password is required.");
            var displayName = request.DisplayName?.Trim();

            var existingUser = await userManager.FindByEmailAsync(email);
            if (existingUser is not null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["email"] = ["An account with that email already exists."]
                });
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? email : displayName,
                CreatedUtc = DateTime.UtcNow
            };

            var createResult = await userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                return Results.ValidationProblem(ToErrorDictionary(createResult));
            }

            await budgetProvisioningService.EnsureBudgetForUserAsync(user, cancellationToken);
            await signInManager.SignInAsync(user, isPersistent: false);

            var access = await currentBudgetService.GetOrCreateForUserAsync(user, cancellationToken);
            return Results.Ok(BuildAuthStatus(access, GetExternalProviders(configuration)));
        });

        auth.MapPost("/login", async (
            LoginRequest request,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            CurrentBudgetService currentBudgetService,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            var email = RequireValue(request.Email, "Email is required.").ToLowerInvariant();
            var password = RequireValue(request.Password, "Password is required.");
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["request"] = ["Email or password is incorrect."]
                });
            }

            var passwordResult = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: false);
            if (!passwordResult.Succeeded)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["request"] = ["Email or password is incorrect."]
                });
            }

            await signInManager.SignInAsync(user, isPersistent: false);
            var access = await currentBudgetService.GetOrCreateForUserAsync(user, cancellationToken);
            return Results.Ok(BuildAuthStatus(access, GetExternalProviders(configuration)));
        });

        auth.MapPost("/logout", async (
            SignInManager<ApplicationUser> signInManager,
            IConfiguration configuration) =>
        {
            await signInManager.SignOutAsync();
            return Results.Ok(new AuthStatusDto(false, null, GetExternalProviders(configuration)));
        });

        auth.MapGet("/google/login", (
            string? returnUrl,
            SignInManager<ApplicationUser> signInManager,
            IConfiguration configuration) =>
        {
            if (!IsGoogleEnabled(configuration))
            {
                return Results.NotFound(new { message = "Google sign-in is not configured." });
            }

            var safeReturnUrl = SanitizeReturnUrl(returnUrl, configuration);
            var callbackUrl = $"/api/auth/google/callback?returnUrl={Uri.EscapeDataString(safeReturnUrl)}";
            var properties = signInManager.ConfigureExternalAuthenticationProperties(
                GoogleDefaults.AuthenticationScheme,
                callbackUrl);

            return Results.Challenge(properties, [GoogleDefaults.AuthenticationScheme]);
        });

        auth.MapGet("/google/callback", async (
            string? returnUrl,
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            BudgetProvisioningService budgetProvisioningService,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            var safeReturnUrl = SanitizeReturnUrl(returnUrl, configuration);
            if (!IsGoogleEnabled(configuration))
            {
                return Results.LocalRedirect(BuildLoginErrorRedirect("Google sign-in is not configured.", safeReturnUrl));
            }

            var info = await signInManager.GetExternalLoginInfoAsync();
            if (info is null)
            {
                return Results.LocalRedirect(BuildLoginErrorRedirect("Google sign-in did not complete.", safeReturnUrl));
            }

            var signInResult = await signInManager.ExternalLoginSignInAsync(
                info.LoginProvider,
                info.ProviderKey,
                isPersistent: false,
                bypassTwoFactor: true);

            ApplicationUser? user;
            if (signInResult.Succeeded)
            {
                user = await userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
                if (user is null)
                {
                    return Results.LocalRedirect(BuildLoginErrorRedirect("Unable to load the Google account.", safeReturnUrl));
                }
            }
            else
            {
                var email = info.Principal.FindFirstValue(ClaimTypes.Email);
                if (string.IsNullOrWhiteSpace(email))
                {
                    return Results.LocalRedirect(BuildLoginErrorRedirect("Google did not provide an email address.", safeReturnUrl));
                }

                user = await userManager.FindByEmailAsync(email);
                if (user is null)
                {
                    user = new ApplicationUser
                    {
                        UserName = email.ToLowerInvariant(),
                        Email = email.ToLowerInvariant(),
                        EmailConfirmed = true,
                        DisplayName = info.Principal.FindFirstValue(ClaimTypes.Name) ?? email,
                        CreatedUtc = DateTime.UtcNow
                    };

                    var createResult = await userManager.CreateAsync(user);
                    if (!createResult.Succeeded)
                    {
                        return Results.LocalRedirect(BuildLoginErrorRedirect("Unable to create the Google-backed account.", safeReturnUrl));
                    }
                }

                var addLoginResult = await userManager.AddLoginAsync(user, info);
                if (!addLoginResult.Succeeded)
                {
                    return Results.LocalRedirect(BuildLoginErrorRedirect("Unable to link the Google login.", safeReturnUrl));
                }
            }

            await budgetProvisioningService.EnsureBudgetForUserAsync(user, cancellationToken);
            await signInManager.SignInAsync(user, isPersistent: false, info.LoginProvider);
            await signInManager.UpdateExternalAuthenticationTokensAsync(info);
            return Results.LocalRedirect(safeReturnUrl);
        });

        var appApi = api.RequireAuthorization();

        appApi.MapGet("/accounts", async (KaboomApiService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetAccountsAsync(cancellationToken)));

        appApi.MapPost("/accounts", async (AccountUpsertRequest request, KaboomApiService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.AddAccountAsync(request, cancellationToken)));

        appApi.MapPut("/accounts/{accountId}", async (string accountId, AccountUpsertRequest request, KaboomApiService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.UpdateAccountAsync(accountId, request, cancellationToken)));

        appApi.MapDelete("/accounts/{accountId}", async (string accountId, KaboomApiService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.DeleteAccountAsync(accountId, cancellationToken)));

        appApi.MapGet("/categories", async (KaboomApiService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetCategoryGroupsAsync(cancellationToken)));

        appApi.MapPost("/categories", async (AddCategoryRequest request, KaboomApiService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.AddCategoryAsync(request, cancellationToken)));

        appApi.MapPatch("/category-groups/{groupId}", async (string groupId, RenameEntityRequest request, KaboomApiService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.RenameCategoryGroupAsync(groupId, request, cancellationToken)));

        appApi.MapPatch("/categories/{categoryId}", async (string categoryId, RenameEntityRequest request, KaboomApiService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.RenameCategoryAsync(categoryId, request, cancellationToken)));

        appApi.MapGet("/budget", async (string? month, int? months, KaboomApiService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetBudgetPageAsync(month, months ?? 3, cancellationToken)));

        appApi.MapPost("/budget/allocations", async (SaveBudgetRequest request, KaboomApiService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.SaveBudgetAsync(request, cancellationToken)));

        appApi.MapGet("/transactions", async (KaboomApiService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetTransactionsPageAsync(cancellationToken)));

        appApi.MapPost("/transactions", async (TransactionUpsertRequest request, KaboomApiService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.AddTransactionAsync(request, cancellationToken)));

        appApi.MapPut("/transactions/{transactionId}", async (string transactionId, TransactionUpsertRequest request, KaboomApiService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.UpdateTransactionAsync(transactionId, request, cancellationToken)));

        appApi.MapGet("/reports", async (string? month, KaboomApiService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetReportsAsync(month, cancellationToken)));

        return api;
    }

    private static async Task<IResult> ExecuteAsync<T>(Func<Task<T>> callback)
    {
        try
        {
            return Results.Ok(await callback());
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Unauthorized();
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["request"] = [exception.Message]
            });
        }
        catch (InvalidOperationException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
        catch (KeyNotFoundException exception)
        {
            return Results.NotFound(new { message = exception.Message });
        }
    }

    private static AuthStatusDto BuildAuthStatus(BudgetAccessContext access, IReadOnlyList<string> providers)
    {
        return new AuthStatusDto(
            true,
            new AuthUserDto(
                access.UserId,
                access.Email,
                access.DisplayName,
                access.BudgetId,
                access.BudgetName),
            providers);
    }

    private static Dictionary<string, string[]> ToErrorDictionary(IdentityResult result)
    {
        return result.Errors
            .GroupBy(error => string.IsNullOrWhiteSpace(error.Code) ? "request" : error.Code)
            .ToDictionary(
                group => group.Key.Length > 0
                    ? $"{char.ToLowerInvariant(group.Key[0])}{group.Key[1..]}"
                    : "request",
                group => group.Select(error => error.Description).ToArray());
    }

    private static IReadOnlyList<string> GetExternalProviders(IConfiguration configuration)
    {
        return IsGoogleEnabled(configuration) ? ["google"] : [];
    }

    private static bool IsGoogleEnabled(IConfiguration configuration)
    {
        return !string.IsNullOrWhiteSpace(configuration["Authentication:Google:ClientId"])
            && !string.IsNullOrWhiteSpace(configuration["Authentication:Google:ClientSecret"]);
    }

    private static string RequireValue(string? value, string message)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value.Trim();
        }

        throw new ArgumentException(message);
    }

    private static string SanitizeReturnUrl(string? returnUrl, IConfiguration configuration)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl)
            && returnUrl.StartsWith("/", StringComparison.Ordinal)
            && !returnUrl.StartsWith("//", StringComparison.Ordinal))
        {
            return returnUrl;
        }

        if (Uri.TryCreate(returnUrl, UriKind.Absolute, out var absoluteUri))
        {
            var origin = absoluteUri.GetLeftPart(UriPartial.Authority);
            var configuredOrigin = configuration["Application:PublicOrigin"]?.TrimEnd('/');
            var allowedOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "http://localhost:5173",
                "https://localhost:5173"
            };

            if (!string.IsNullOrWhiteSpace(configuredOrigin))
            {
                allowedOrigins.Add(configuredOrigin);
            }

            if (allowedOrigins.Contains(origin))
            {
                return absoluteUri.ToString();
            }
        }

        return "/budget";
    }

    private static string BuildLoginErrorRedirect(string message, string returnUrl)
    {
        return $"/login?error={Uri.EscapeDataString(message)}&returnTo={Uri.EscapeDataString(returnUrl)}";
    }
}
