using System.Security.Claims;
using CvdInbox.Hosting;
using CvdInbox.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Options;

namespace CvdInbox.Operators;

public sealed record SignInRequest(string? Name, string? Password);

public sealed record SessionView(string? Operator);

public static class OperatorSignIn
{
    public const string SessionCookie = "__Host-cvd-session";
    public const string AntiforgeryCookie = "__Host-cvd-antiforgery";
    public const string TokenCookie = "XSRF-TOKEN";
    public const string TokenHeader = "X-XSRF-TOKEN";

    private const string UnknownName = "(unknown name)";

    public static IServiceCollection AddOperatorSignIn(this IServiceCollection services)
    {
        // The keys protect the session and antiforgery cookies. They live next to the database, in the data volume,
        // so sessions survive a restart.
        services.AddDataProtection().SetApplicationName("cvd-inbox");
        services.AddOptions<KeyManagementOptions>()
            .Configure<IOptions<InboxOptions>, IHostEnvironment, ILoggerFactory>((keys, inbox, environment, loggers) =>
                keys.XmlRepository = new FileSystemXmlRepository(
                    new DirectoryInfo(Path.Combine(inbox.Value.DataDirectory(environment), "keys")), loggers));

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(cookie =>
            {
                cookie.Cookie.Name = SessionCookie;
                cookie.Cookie.HttpOnly = true;
                cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                cookie.Cookie.SameSite = SameSiteMode.Strict;
                cookie.ExpireTimeSpan = TimeSpan.FromHours(1);
                cookie.SlidingExpiration = true;
                // The client app handles navigation, so the API answers 401 and 403 instead of redirecting.
                cookie.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                cookie.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });
        services.AddAuthorization();
        services.AddAntiforgery(antiforgery =>
        {
            antiforgery.HeaderName = TokenHeader;
            antiforgery.Cookie.Name = AntiforgeryCookie;
            // No SecurePolicy: Always makes antiforgery throw on plain http, so the cookie policy in Program.cs adds Secure.
            antiforgery.Cookie.SameSite = SameSiteMode.Strict;
        });
        return services;
    }

    public static IEndpointRouteBuilder MapAuth(this IEndpointRouteBuilder routes)
    {
        var auth = routes.MapGroup("/api/auth");
        auth.MapGet("/session", GetSession);
        auth.MapPost("/sign-in", HandleSignInAsync)
            .RequireRateLimiting(RateLimits.SignIns)
            .AddEndpointFilter(RequireHttpsAsync)
            .AddEndpointFilter<AntiforgeryCheck>();
        auth.MapPost("/sign-out", HandleSignOutAsync)
            .RequireAuthorization()
            .AddEndpointFilter<AntiforgeryCheck>();
        return routes;
    }

    // Browsers treat plain http as secure only on loopback names. Anywhere else the password has just crossed the
    // network in cleartext, so the sign-in is refused before it is checked. A trusted proxy reports https with X-Forwarded-Proto.
    private static ValueTask<object?> RequireHttpsAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.HttpContext.Request;
        var host = request.Host.Host;
        if (request.IsHttps || host is "127.0.0.1" or "[::1]" or "::1" || string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
            return next(context);
        return ValueTask.FromResult<object?>(Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Operator sign-in needs HTTPS."));
    }

    // Also hands the client a fresh request token. The client calls it again after signing in or out,
    // because the token is bound to the user.
    private static IResult GetSession(HttpContext context, IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(context);
        context.Response.Cookies.Append(TokenCookie, tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
        });
        var name = context.User.Identity?.IsAuthenticated == true ? context.User.Identity.Name : null;
        return Results.Ok(new SessionView(name));
    }

    private static async Task<IResult> HandleSignInAsync(
        SignInRequest request, HttpContext context, OperatorAccounts accounts, SecurityEvents events, CancellationToken cancellationToken)
    {
        var client = SecurityEvents.ClientAddress(context);
        var check = await accounts.CheckPasswordAsync(request.Name, request.Password, cancellationToken);
        if (check.Outcome == SignInOutcome.Succeeded)
        {
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, check.Name!)], CookieAuthenticationDefaults.AuthenticationScheme);
            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
            events.SignedIn(check.Name!, client);
            return Results.NoContent();
        }

        // The name field is logged only when it names an operator: people type passwords there too.
        var attempted = await accounts.ExistingNameAsync(request.Name, cancellationToken) ?? UnknownName;
        if (check.Outcome == SignInOutcome.LockedOut)
            events.LockedOut(attempted, client);
        else
            events.SignInFailed(attempted, client);
        // Same answer for unknown names, wrong passwords and locked accounts.
        return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Wrong name or password.");
    }

    private static async Task<IResult> HandleSignOutAsync(HttpContext context, SecurityEvents events)
    {
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        events.SignedOut(context.User.Identity!.Name!);
        return Results.NoContent();
    }
}
