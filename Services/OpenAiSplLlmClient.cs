using System.Text.Json;
using System.Text.Json.Serialization;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using SplWhisperer.Api.Prompts;

namespace SplWhisperer.Api.Services;

/// <summary>
/// Production-ready implementation of <see cref="ISplLlmClient"/> that calls OpenAI Chat models.
/// </summary>
public sealed class OpenAiSplLlmClient : ISplLlmClient
{
    private readonly ILogger<OpenAiSplLlmClient> _logger;
    private readonly ChatClient _chatClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public OpenAiSplLlmClient(
        ILogger<OpenAiSplLlmClient> logger,
        IConfiguration configuration)
    {
        _logger = logger;

        var endpoint = configuration["AzureOpenAI:Endpoint"];
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException(
                "Configuration value 'AzureOpenAI:Endpoint' is missing or empty. " +
                "Set a valid Azure OpenAI endpoint URL in configuration.");
        }

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri))
        {
            throw new InvalidOperationException(
                $"Configuration value 'AzureOpenAI:Endpoint' is not a valid absolute URI: '{endpoint}'.");
        }

        var apiKey = configuration["AzureOpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Configuration value 'AzureOpenAI:ApiKey' is missing or empty. " +
                "Set a valid Azure OpenAI API key in configuration.");
        }

        var deploymentName = configuration["AzureOpenAI:DeploymentName"];
        if (string.IsNullOrWhiteSpace(deploymentName))
        {
            throw new InvalidOperationException(
                "Configuration value 'AzureOpenAI:DeploymentName' is missing or empty. " +
                "Set the Azure OpenAI deployment name to use for chat completions.");
        }

        var azureClient = new AzureOpenAIClient(endpointUri, new AzureKeyCredential(apiKey));
        _chatClient = azureClient.GetChatClient(deploymentName);

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Disallow,
            AllowTrailingCommas = false
        };
    }

    public async Task<SplLlmResult> GetSplAnswerAsync(string question, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            throw new ArgumentException("Question must be provided.", nameof(question));
        }

        _logger.LogInformation("Calling Azure OpenAI LLM for SPL generation.");

        ChatMessage[] messages =
        {
            new SystemChatMessage(SplWhispererPrompt.SystemPrompt),
            new UserChatMessage(question)
        };

        ChatCompletion completion;
        try
        {
            completion = await _chatClient.CompleteChatAsync(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while calling OpenAI ChatClient.");
            throw;
        }

        var content = completion.Content.Count > 0
            ? completion.Content[0].Text
            : null;

        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogError("OpenAI response content was empty.");
            throw new InvalidOperationException("OpenAI did not return any content.");
        }

        LlmJsonResult? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<LlmJsonResult>(content, _jsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse OpenAI response as strict JSON. Raw content: {Content}", content);
            throw new InvalidOperationException("OpenAI response was not valid JSON.", ex);
        }

        if (parsed is null || string.IsNullOrWhiteSpace(parsed.Spl))
        {
            _logger.LogError("Parsed OpenAI JSON response was null or missing required 'spl' field. Raw content: {Content}", content);
            throw new InvalidOperationException("OpenAI response JSON was missing required fields.");
        }

        var result = new SplLlmResult(
            parsed.Spl,
            parsed.Explanation?.ToArray() ?? Array.Empty<string>(),
            parsed.Optimizations?.ToArray() ?? Array.Empty<string>(),
            parsed.Guardrails?.ToArray() ?? Array.Empty<string>()
        );

        // Basic sanity check to ensure we are returning real SPL in expected domains.
        if (!result.Spl.Contains("index=", StringComparison.OrdinalIgnoreCase) ||
            !result.Spl.Contains("stats", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Generated SPL may not meet expected patterns. SPL: {Spl}", result.Spl);
        }

        return result;
    }

    /// <summary>
    /// Internal DTO that mirrors the JSON schema described in <see cref="SplWhispererPrompt.SystemPrompt"/>.
    /// </summary>
    private sealed class LlmJsonResult
    {
        [JsonPropertyName("spl")]
        public string Spl { get; set; } = string.Empty;

        [JsonPropertyName("explanation")]
        public List<string>? Explanation { get; set; }

        [JsonPropertyName("optimizations")]
        public List<string>? Optimizations { get; set; }

        [JsonPropertyName("guardrails")]
        public List<string>? Guardrails { get; set; }
    }
}

