namespace SplWhisperer.Api.Models;

public record SplWhispererResponse(
    string Spl,
    string[] Explanation,
    string[] Optimizations,
    string[] Guardrails
);
