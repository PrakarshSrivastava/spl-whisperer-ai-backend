using SplWhisperer.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Register services
builder.Services.AddScoped<ISplLlmClient, DemoSplLlmClient>();
builder.Services.AddScoped<ISplWhispererService, SplWhispererService>();
builder.Services.AddScoped<ISplGuardrailService, SplGuardrailService>();

var app = builder.Build();

// Request DTO
public record SplWhispererRequest(string Question);

// Response DTO
public record SplWhispererResponse(
    string Spl,
    string[] Explanation,
    string[] Optimizations,
    string[] Guardrails
);

// POST /api/spl-whisperer
app.MapPost("/api/spl-whisperer", async (SplWhispererRequest request, ISplWhispererService service, CancellationToken ct) =>
{
    var result = await service.GetSplAsync(request.Question, ct);

    var response = new SplWhispererResponse(
        result.Spl,
        result.Explanation.ToArray(),
        result.Optimizations.ToArray(),
        result.Guardrails.ToArray()
    );

    return Results.Ok(response);
});

app.Run();