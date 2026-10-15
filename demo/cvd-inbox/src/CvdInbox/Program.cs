using System.Text.Json;
using System.Text.Json.Serialization;
using CvdInbox;
using CvdInbox.Data;
using CvdInbox.Disclosure;
using CvdInbox.Hosting;
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

await using var app = builder.Build();

if (runCommand)
    return await OperatorCommand.RunAsync(app.Services, args, Console.In, Console.Out, Console.Error);

await app.Services.PrepareDatabaseAsync();

app.UseTrustedProxies();
app.UseExceptionHandler();
app.UseSecurityHeaders();
app.UseBodySizeLimit();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();

app.MapDisclosure();
app.MapReports();
app.Map("/api/{**path}", () => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found."));
app.MapFallbackToFile("index.html");

await app.RunAsync();
return 0;
