namespace SplWhisperer.Api.Models;

/// <summary>
/// Request payload for the /api/spl-whisperer endpoint.
/// </summary>
public sealed record SplWhispererRequest(string Question);

