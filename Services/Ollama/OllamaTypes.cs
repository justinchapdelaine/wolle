using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace wolle.Services.Ollama;
/// <summary>
/// Represents a performance metric for monitoring.
/// </summary>
public record PerformanceMetric
{
    public DateTime Timestamp { get; init; }
    public required string OperationType { get; init; }
    public required string FileName { get; init; }
    public long FileSizeBytes { get; init; }
    public TimeSpan ProcessingTime { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = new();
}

/// <summary>
/// Represents performance statistics summary.
/// </summary>
public class PerformanceStats
{
    public required TimeSpan ServiceUptime { get; init; }
    public required int TotalFilesProcessed { get; init; }
    public required int SuccessfulOperations { get; init; }
    public required int FailedOperations { get; init; }
    public required double SuccessRate { get; init; }
    public required long TotalBytesProcessed { get; init; }
    public required long AverageFileSizeBytes { get; init; }
    public required double AverageProcessingTimeMs { get; init; }
    public required double ThroughputBytesPerSecond { get; init; }
    public required List<PerformanceMetric> RecentMetrics { get; init; } = new();
}

/// <summary>
/// Represents Ollama API request.
/// </summary>
public record OllamaApiRequest
{
    public required string Model { get; init; }
    public required string Prompt { get; init; }
    public bool Stream { get; init; }
    public OllamaOptions? Options { get; init; }
    public List<string>? Images { get; init; }
}

/// <summary>
/// Represents Ollama API options.
/// </summary>
public record OllamaOptions
{
    [JsonPropertyName("num_ctx")]
    public int? NumCtx { get; init; }

    [JsonPropertyName("temperature")]
    public double? Temperature { get; init; }

    [JsonPropertyName("top_p")]
    public double? TopP { get; init; }

    [JsonPropertyName("top_k")]
    public int? TopK { get; init; }
}

/// <summary>
/// Represents Ollama progress information.
/// </summary>
public class OllamaProgress
{
    public required string Status { get; set; } = string.Empty;
    public string? Digest { get; set; }
    public long Total { get; set; }
    public long Completed { get; set; }
    public int Percent { get; set; }
}