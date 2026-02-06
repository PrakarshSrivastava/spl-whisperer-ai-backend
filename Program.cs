using SplWhisperer.Api.Services;
using SplWhisperer.Api.Models;

var builder = WebApplication.CreateBuilder(args);

// Register services
builder.Services.AddScoped<ISplLlmClient, OpenAiSplLlmClient>();
builder.Services.AddScoped<ISplWhispererService, SplWhispererService>();
builder.Services.AddScoped<ISplGuardrailService, SplGuardrailService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}



// POST /api/spl-whisperer
app.MapPost("/api/spl-whisperer", async (
    SplWhispererRequest request,
    ISplWhispererService service,
    ISplGuardrailService guardrails,
    CancellationToken ct) =>
{
    var result = await service.GetSplAsync(request.Question, ct);
    var validation = guardrails.Validate(result.Spl);

    // Combine any model-provided guardrails with guardrail service messages.
    var combinedGuardrails = result.Guardrails
        .Concat(validation.Messages)
        .ToArray();

    var response = new SplWhispererResponse(
        result.Spl,
        result.Explanation.ToArray(),
        result.Optimizations.ToArray(),
        combinedGuardrails
    );

    return Results.Ok(response);
});

app.Run();