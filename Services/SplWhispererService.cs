using Microsoft.Extensions.Logging;

namespace SplWhisperer.Api.Services;

/// <summary>
/// Service that turns a plain-English question into SPL plus explanations,
/// using an abstracted, mockable LLM client.
/// </summary>
public interface ISplWhispererService
{
    Task<SplWhispererResult> GetSplAsync(string question, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result shape returned by the SplWhispererService.
/// Mirrors what the API ultimately returns.
/// </summary>
public sealed record SplWhispererResult(
    string Spl,
    IReadOnlyList<string> Explanation,
    IReadOnlyList<string> Optimizations,
    IReadOnlyList<string> Guardrails
);

/// <summary>
/// Default implementation of ISplWhispererService.
/// Uses an injected ISplLlmClient which can be mocked in tests.
/// </summary>
public sealed class SplWhispererService : ISplWhispererService
{
    private readonly ISplLlmClient _llmClient;
    private readonly ILogger<SplWhispererService> _logger;

    public SplWhispererService(ISplLlmClient llmClient, ILogger<SplWhispererService> logger)
    {
        _llmClient = llmClient;
        _logger = logger;
    }

    public async Task<SplWhispererResult> GetSplAsync(string question, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            throw new ArgumentException("Question must not be empty.", nameof(question));
        }

        _logger.LogInformation("Generating SPL for question: {Question}", question);

        // Delegate to the LLM abstraction – in a real app this would call
        // an external model, but for demo purposes we can keep it simple.
        var llmResult = await _llmClient.GetSplAnswerAsync(question, cancellationToken);

        return new SplWhispererResult(
            llmResult.Spl,
            llmResult.Explanation,
            llmResult.Optimizations,
            llmResult.Guardrails
        );
    }
}

