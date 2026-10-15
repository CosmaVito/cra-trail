using System.Text.Json;
using System.Text.Json.Serialization;
using CvdInbox;
using CvdInbox.Data;
using CvdInbox.Disclosure;
using CvdInbox.Hosting;
using CvdInbox.Inbox;
using CvdInbox.Operators;
using CvdInbox.Reports;
using CvdInbox.Security;

// "cvd-inbox create-operator <name>" runs a command instead of the server. The check comes after Build(),
// because the EF Core tools stop the program there.
var runCommand = args is [OperatorCommand.Name, ..];
var builder = WebApplication.CreateBuilder(runCommand ? [] : args);

builder.WebHost.ConfigureKestrel(kestrel => kestrel.AddServerHeader = false);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(json =>
    json.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
builder.Services.AddInboxOptions();
builder.Services.AddInboxData();
builder.Services.AddDisclosure();
builder.Services.AddSingleton<SecurityEvents>();
builder.Services.AddInboxRateLimits();
builder.Services.AddOperators();
builder.Services.AddOperatorSignIn();

// Not "await using": RunAsync disposes the app. A failure before RunAsync, such as invalid Inbox settings, must leave
// the host undisposed, because WebApplicationFactory still waits on it from another thread to report that failure.
var app = builder.Build();

if (runCommand)
{
    await using (app)
        return await OperatorCommand.RunAsync(app.Services, args, Console.In, Console.Out, Console.Error);
}

await app.Services.PrepareDatabaseAsync();

app.UseTrustedProxies();
app.UseCookiePolicy(new CookiePolicyOptions { Secure = CookieSecurePolicy.Always });
app.UseExceptionHandler();
app.UseSecurityHeaders();
app.UseBodySizeLimit();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapDisclosure();
app.MapReports();
app.MapAuth();
app.MapInbox();
app.Map("/api/{**path}", () => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found."));
app.MapFallbackToFile("index.html");

await app.RunAsync();
return 0;
