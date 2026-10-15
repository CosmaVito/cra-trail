using System.Text.Json;
using System.Text.Json.Serialization;
using CvdInbox;
using CvdInbox.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(kestrel => kestrel.AddServerHeader = false);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(json =>
    json.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
builder.Services.AddInboxOptions();

await using var app = builder.Build();

app.UseExceptionHandler();
app.UseSecurityHeaders();
app.UseBodySizeLimit();
app.UseStaticFiles();
app.UseRouting();

app.Map("/api/{**path}", () => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found."));
app.MapFallbackToFile("index.html");

await app.RunAsync();
return 0;
