using System;
using System.Collections.Generic;

namespace wolle.Services
{
    /// <summary>
    /// Represents a performance metric for monitoring.
    /// </summary>
    public class PerformanceMetric
    {
        public DateTime Timestamp { get; set; }
        public string OperationType { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<string, string> Metadata { get; } = new();
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
    public class OllamaApiRequest
    {
        public string Model { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;
        public bool Stream { get; set; }
        public OllamaOptions? Options { get; set; }
        public List<string>? Images { get; set; }
    }

    /// <summary>
    /// Represents Ollama API options.
    /// </summary>
    public class OllamaOptions
    {
        [System.Text.Json.Serialization.JsonPropertyName("num_ctx")]
        public int? NumCtx { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("temperature")]
        public double? Temperature { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("top_p")]
        public double? TopP { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("top_k")]
        public int? TopK { get; set; }
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
}