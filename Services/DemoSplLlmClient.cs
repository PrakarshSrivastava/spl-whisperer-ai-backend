using Microsoft.Extensions.Logging;

namespace SplWhisperer.Api.Services;

/// <summary>
/// Demo-friendly implementation of ISplLlmClient.
/// Does NOT call any real LLM provider or require secrets.
/// Instead, it fabricates plausible SPL and guidance for a given question.
/// </summary>
public sealed class DemoSplLlmClient : ISplLlmClient
{
    private readonly ILogger<DemoSplLlmClient> _logger;

    public DemoSplLlmClient(ILogger<DemoSplLlmClient> logger)
    {
        _logger = logger;
    }

    public Task<SplLlmResult> GetSplAnswerAsync(string question, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Demo LLM generating SPL for question: {Question}", question);

        // Extremely simplified, safe-to-demo "generation".
        // In reality, this would call an actual LLM using configuration/secret keys.
        var sanitizedQuestion = question.Replace("\"", string.Empty);
        var pseudoSearchTerm = sanitizedQuestion.Length > 40
            ? sanitizedQuestion[..40] + "..."
            : sanitizedQuestion;

        var spl = $"search \"{pseudoSearchTerm}\" | stats count by host, source";

        var explanation = new List<string>
        {
            "This is a demo-only SPL generated without any external LLM.",
            "The search looks for events that roughly match your question text.",
            "The pipeline then aggregates results by host and source using stats."
        };

        var optimizations = new List<string>
        {
            "Add an index or sourcetype filter to narrow the dataset.",
            "Constrain the time range (e.g., last 24 hours) for faster execution.",
            "Project only the fields you actually need downstream."
        };

        var guardrails = new List<string>
        {
            "Do not run this SPL against unrestricted production indexes without validation.",
            "Treat user-provided text as untrusted; sanitize before integrating into SPL.",
            "Review and test generated SPL in a non-production environment first."
        };

        var result = new SplLlmResult(
            spl,
            explanation,
            optimizations,
            guardrails
        );

        return Task.FromResult(result);
    }
}

