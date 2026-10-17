using System.Text.Json;
using System.Text.Json.Serialization;
using CvdInbox;
using CvdInbox.Data;
using CvdInbox.Disclosure;
using CvdInbox.Hosting;
using CvdInbox.Inbox;
using CvdInbox.Operators;
using CvdInbox.Reports;
using CvdInbox.Retention;
using CvdInbox.Security;

if (OperatorCommand.IsUnknownCommand(args))
{
    await Console.Error.WriteLineAsync(OperatorCommand.Usage);
    return 2;
}

// "cvd-inbox create-operator <name>" runs a command instead of the server. The check comes after Build(),
// because the EF Core tools stop the program there.
var runCommand = OperatorCommand.IsCommand(args);
// The content root is the app's folder, not the working directory: appsettings.json, wwwroot and a relative
// Inbox:DataPath are the same wherever the app is started from.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = runCommand ? [] : args, ContentRootPath = AppContext.BaseDirectory });

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
builder.Services.AddRetention();

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
app.UseStaticFiles(ClientFiles.Options());
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapDisclosure();
app.MapReports();
app.MapAuth();
app.MapInbox();
app.Map("/api/{**path}", () => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found."));
app.MapFallbackToFile("index.html", ClientFiles.Options());

await app.RunAsync();
return 0;
