namespace SplWhisperer.Api.Services;

/// <summary>
/// Abstraction over an LLM client so it can be mocked or swapped out.
/// This is demo-friendly: no real secrets or external dependencies required.
/// </summary>
public interface ISplLlmClient
{
    /// <summary>
    /// Given a plain-English question, returns a structured SPL result.
    /// In a real implementation this would call an LLM provider.
    /// </summary>
    Task<SplLlmResult> GetSplAnswerAsync(string question, CancellationToken cancellationToken = default);
}

/// <summary>
/// Structured response that contains all fields needed by the API.
/// </summary>
public sealed record SplLlmResult(
    string Spl,
    IReadOnlyList<string> Explanation,
    IReadOnlyList<string> Optimizations,
    IReadOnlyList<string> Guardrails
);

