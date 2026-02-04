using System.Text.RegularExpressions;

namespace SplWhisperer.Api.Services;

/// <summary>
/// Result of SPL guardrail validation.
/// </summary>
public sealed record GuardrailValidationResult(
    bool IsValid,
    IReadOnlyList<string> Messages
);

/// <summary>
/// Validates generated SPL against basic safety / governance rules.
/// - Only allows read-only SPL commands
/// - Restricts indexes to a small allow-list
/// - Blocks destructive or admin commands
/// </summary>
public interface ISplGuardrailService
{
    GuardrailValidationResult Validate(string spl);
}

/// <summary>
/// Simple, demo-friendly SPL guardrail validator.
/// Uses string/regex checks only – no external dependencies or secrets.
/// </summary>
public sealed class SplGuardrailService : ISplGuardrailService
{
    // Allowed indexes
    private static readonly HashSet<string> AllowedIndexes =
        new(StringComparer.OrdinalIgnoreCase) { "security", "app", "infra" };

    // Disallowed/destructive/admin commands (simple keyword-based blocklist)
    private static readonly string[] BlockedKeywords =
    {
        "delete",
        "drop",
        "collect",
        "sendemail",
        "runshellscript",
        "script",
        "rest",
        "dbxquery",
        "outputlookup"
    };

    // Commands that are considered read-only and allowed in pipelines.
    // This is intentionally conservative; anything not in this list
    // (and not the initial implicit search) will be flagged.
    private static readonly HashSet<string> AllowedCommands =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "search",
            "tstats",
            "stats",
            "timechart",
            "chart",
            "table",
            "fields",
            "eval",
            "where",
            "rex",
            "sort",
            "dedup",
            "head",
            "tail",
            "lookup",
            "rename",
            "fillnull",
            "top",
            "rare",
            "eventstats",
            "streamstats"
        };

    private static readonly Regex IndexRegex =
        new(@"\bindex\s*=\s*(?<name>[^\s|]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public GuardrailValidationResult Validate(string spl)
    {
        var messages = new List<string>();

        if (string.IsNullOrWhiteSpace(spl))
        {
            messages.Add("SPL is empty.");
            return new GuardrailValidationResult(false, messages);
        }

        var normalized = spl.Trim();
        var lowered = normalized.ToLowerInvariant();

        // 1. Block destructive / admin commands.
        foreach (var keyword in BlockedKeywords)
        {
            if (Regex.IsMatch(lowered, $@"\b{Regex.Escape(keyword)}\b", RegexOptions.IgnoreCase))
            {
                messages.Add($"SPL uses blocked command or keyword '{keyword}', which is not allowed.");
            }
        }

        // 2. Restrict indexes to security, app, infra.
        var indexMatches = IndexRegex.Matches(normalized);
        if (indexMatches.Count == 0)
        {
            messages.Add("SPL must specify an explicit index (security, app, or infra).");
        }

        foreach (Match match in indexMatches)
        {
            var name = match.Groups["name"].Value.Trim();
            if (!AllowedIndexes.Contains(name))
            {
                messages.Add($"Index '{name}' is not allowed. Allowed indexes: security, app, infra.");
            }
        }

        // 3. Enforce only read-only pipeline commands.
        // Split on '|' to approximate commands in the pipeline.
        var segments = normalized.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            if (string.IsNullOrWhiteSpace(segment))
            {
                continue;
            }

            // First segment can be an implicit search; allow it as long as it doesn't start
            // with an obviously blocked command.
            if (i == 0)
            {
                // If first token looks like a command, verify it's allowed.
                var firstToken = GetFirstToken(segment);
                if (!string.IsNullOrEmpty(firstToken) &&
                    !AllowedCommands.Contains(firstToken) &&
                    !LooksLikeImplicitSearch(firstToken))
                {
                    messages.Add($"First SPL segment uses unsupported command '{firstToken}'. Only read-only search is allowed.");
                }

                continue;
            }

            // Subsequent segments should start with a pipeline command.
            var command = GetFirstToken(segment);
            if (string.IsNullOrEmpty(command))
            {
                continue;
            }

            if (!AllowedCommands.Contains(command))
            {
                messages.Add($"Command '{command}' is not allowed in the SPL pipeline.");
            }
        }

        var isValid = messages.Count == 0;
        return new GuardrailValidationResult(isValid, messages);
    }

    private static string GetFirstToken(string segment)
    {
        var trimmed = segment.TrimStart();
        var spaceIdx = trimmed.IndexOf(' ');
        return spaceIdx < 0 ? trimmed : trimmed[..spaceIdx];
    }

    private static bool LooksLikeImplicitSearch(string token)
    {
        // If the first token looks like a field comparison or search term, treat it as
        // an implicit search rather than a command (e.g., "error", "host=web01").
        return token.Contains("=") || !Regex.IsMatch(token, "^[a-zA-Z_]+$");
    }
}

