using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace wolle.Services;
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
        public TimeSpan ServiceUptime { get; set; }
        public int TotalFilesProcessed { get; set; }
        public int SuccessfulOperations { get; set; }
        public int FailedOperations { get; set; }
        public double SuccessRate { get; set; }
        public long TotalBytesProcessed { get; set; }
        public long AverageFileSizeBytes { get; set; }
        public double AverageProcessingTimeMs { get; set; }
        public double ThroughputBytesPerSecond { get; set; }
        public List<PerformanceMetric> RecentMetrics { get; set; } = new();
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
        public string status { get; set; } = string.Empty;
        public string? digest { get; set; }
        public long total { get; set; }
        public long completed { get; set; }
        public int percent { get; set; }
    }