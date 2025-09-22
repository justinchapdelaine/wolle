using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using wolle.Services.Ollama;
using wolle.Services.Interfaces;

namespace wolle.Services.Ollama;
/// <summary>
/// Provides HTTP communication with Ollama API.
/// </summary>
public interface IOllamaHttpService
{
    /// <summary>
    /// Checks if specified Ollama model exists.
    /// </summary>
    /// <param name="modelName">The name of model to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if model exists, false otherwise.</returns>
    Task<bool> ModelExistsAsync(string modelName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pulls a model with progress tracking using Ollama API.
    /// </summary>
    /// <param name="modelName">The name of model to pull.</param>
    /// <param name="onProgress">Progress callback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task PullModelWithProgressApiAsync(string modelName, Action<OllamaProgress>? onProgress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs Ollama API asynchronously with a given prompt.
    /// </summary>
    /// <param name="request">The API request.</param>
    /// <param name="onOutput">Output callback.</param>
    /// <param name="onComplete">Completion callback.</param>
    /// <param name="onError">Error callback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RunOllamaApiAsync(OllamaApiRequest request, Action<string>? onOutput = null, Action? onComplete = null, Action<string>? onError = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a basic health check on Ollama service.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if Ollama is responsive, false otherwise.</returns>
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if model is ready for generation.
    /// </summary>
    /// <param name="modelName">The model name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if model is ready, false otherwise.</returns>
    Task<bool> IsModelReadyAsync(string modelName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implements HTTP communication with Ollama API.
/// </summary>
public class OllamaHttpService(IHttpClientFactory httpClientFactory, ILogger<OllamaHttpService> logger, IExceptionHandlingService exceptionHandlingService) : IOllamaHttpService, IDisposable
{
    private readonly SemaphoreSlim _apiLock = new(1, 1);
    private HttpClient _httpClient = httpClientFactory.CreateClient("OllamaClient");
    private bool _isDisposed = false;

    /// <summary>
    /// Checks if specified Ollama model exists.
    /// </summary>
    /// <param name="modelName">The name of model to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if model exists, false otherwise.</returns>
    public async Task<bool> ModelExistsAsync(string modelName, CancellationToken cancellationToken = default)
    {
        logger?.LogInformation("Checking if model exists: {ModelName}", modelName);

        try
        {
            if (_isDisposed)
            {
                logger?.LogWarning("OllamaHttpService is disposed, cannot check model existence");
                return false;
            }

            await _apiLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_isDisposed)
                {
                    logger?.LogWarning("OllamaHttpService is disposed after acquiring lock");
                    return false;
                }

                int maxRetries = 3;
                int retryCount = 0;
                bool success = false;

                while (!success && retryCount < maxRetries)
                {
                    try
                    {
                        logger?.LogInformation($"Sending list request to Ollama API (attempt {retryCount + 1})");

                        var response = await _httpClient.GetAsync("/api/tags", cancellationToken);
                        response.EnsureSuccessStatusCode();

                        logger?.LogInformation("List response received from Ollama API");
                        success = true;

                        var responseContent = await response.Content.ReadAsStringAsync();
                        var json = JsonDocument.Parse(responseContent);

                        if (json.RootElement.TryGetProperty("models", out var modelsElement))
                        {
                            var models = modelsElement.EnumerateArray();
                            foreach (var model in models)
                            {
                                if (model.TryGetProperty("name", out var nameElement))
                                {
                                    string name = nameElement.GetString() ?? "";
                                    if (name.Equals(modelName, StringComparison.OrdinalIgnoreCase) ||
                                        name.Equals($"{modelName}:latest", StringComparison.OrdinalIgnoreCase))
                                    {
                                        logger?.LogInformation($"Model {modelName} exists: {name}");
                                        return true;
                                    }
                                }
                            }
                        }

                        logger?.LogInformation($"Model {modelName} not found");
                        return false;
                    }
                    catch (HttpRequestException ex)
                    {
                        retryCount++;
                        logger?.LogError($"Network error (attempt {retryCount}): {ex.Message}");
                        await exceptionHandlingService.HandleExceptionAsync(ex, "OllamaHttpService.ModelExistsAsync",
                            $"Network connection issue while checking model (attempt {retryCount})", ExceptionSeverity.Warning);

                        if (retryCount < maxRetries)
                        {
                            await Task.Delay(1000 * retryCount, cancellationToken);
                        }
                        else
                        {
                            logger?.LogError("Max retries reached for Ollama API");
                            return false;
                        }
                    }
                    catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
                    {
                        retryCount++;
                        logger?.LogError($"Request timeout (attempt {retryCount}): {ex.Message}");
                        await exceptionHandlingService.HandleExceptionAsync(ex, "OllamaHttpService.ModelExistsAsync",
                            $"Request timeout while checking model (attempt {retryCount})", ExceptionSeverity.Warning);

                        if (retryCount < maxRetries)
                        {
                            await Task.Delay(1000 * retryCount, cancellationToken);
                        }
                        else
                        {
                            logger?.LogError("Max retries reached due to timeouts");
                            return false;
                        }
                    }
                    catch (JsonException ex)
                    {
                        logger?.LogError($"JSON parsing error: {ex.Message}");
                        await exceptionHandlingService.HandleExceptionAsync(ex, "OllamaHttpService.ModelExistsAsync",
                            "Invalid response format from Ollama API", ExceptionSeverity.Error);
                        return false;
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        logger?.LogInformation("Model existence check was cancelled");
                        throw; // Re-throw cancellation exceptions
                    }
                }

                return false;
            }
            finally
            {
                _apiLock.Release();
            }
        }
        catch (ObjectDisposedException disposedEx)
        {
            logger?.LogError($"Error checking model existence: Service is disposed - {disposedEx.Message}");
            await exceptionHandlingService.HandleExceptionAsync(disposedEx, "OllamaHttpService.ModelExistsAsync",
                "Service is no longer available. Please restart the application.", ExceptionSeverity.Error);
            return false;
        }
        catch (InvalidOperationException invalidEx)
        {
            logger?.LogError($"Invalid operation: {invalidEx.Message}");
            await exceptionHandlingService.HandleExceptionAsync(invalidEx, "OllamaHttpService.ModelExistsAsync",
                "Invalid operation performed. Please restart the application.", ExceptionSeverity.Error);
            return false;
        }
        catch (Exception ex)
        {
            logger?.LogError($"Unexpected error checking model existence: {ex.Message}");
            await exceptionHandlingService.HandleExceptionAsync(ex, "OllamaHttpService.ModelExistsAsync",
                "Failed to check model availability. Please check your network connection.", ExceptionSeverity.Error);
            return false;
        }
    }

    /// <summary>
    /// Pulls a model with progress tracking using Ollama API.
    /// </summary>
    /// <param name="modelName">The name of model to pull.</param>
    /// <param name="onProgress">Progress callback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task PullModelWithProgressApiAsync(string modelName, Action<OllamaProgress>? onProgress = null, CancellationToken cancellationToken = default)
    {
        logger?.LogInformation($"Pulling model with progress (API): {modelName}");

        try
        {
            var request = new
            {
                Model = modelName,
                Stream = true
            };

            var content = new StringContent(
                JsonSerializer.Serialize(request, options: new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                System.Text.Encoding.UTF8,
                "application/json");

            logger?.LogInformation("Sending pull request to Ollama API");

            var response = await _httpClient.PostAsync("/api/pull", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            logger?.LogInformation("Pull response received from Ollama API");

            using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken))
            using (var reader = new StreamReader(stream))
            {
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (!string.IsNullOrEmpty(line))
                    {
                        try
                        {
                            var json = JsonDocument.Parse(line);
                            if (json.RootElement.TryGetProperty("status", out var statusElement))
                            {
                                string status = statusElement.GetString() ?? "";

                                if (status.Contains("error") || status.Contains("failed") ||
                                    status.Contains("success") || status.Contains("manifest") ||
                                    status.Contains("verifying") || status.Contains("pulling manifest"))
                                {
                                    logger?.LogInformation($"Pull status: {status}");
                                }

                                var progress = ParseProgressFromApiResponse(json.RootElement);
                                if (progress != null)
                                {
                                    onProgress?.Invoke(progress);
                                }
                            }

                            if (json.RootElement.TryGetProperty("status", out var doneStatusElement) &&
                                doneStatusElement.GetString() == "success")
                            {
                                logger?.LogInformation("Ollama pull completed successfully");
                                break;
                            }
                        }
                        catch (JsonException ex)
                        {
                            logger?.LogError($"Error parsing JSON response: {ex.Message}");
                            await exceptionHandlingService.HandleExceptionAsync(ex, "OllamaHttpService.PullModelWithProgressApiAsync",
                                "Invalid response format from Ollama API during model pull", ExceptionSeverity.Warning);
                        }
                    }
                }
            }
        }
        catch (HttpRequestException ex)
        {
            logger?.LogError($"Ollama API pull HTTP error: {ex.Message}");
            await exceptionHandlingService.HandleExceptionAsync(ex, "OllamaHttpService.PullModelWithProgressApiAsync",
                "Network error occurred while pulling model. Please check your connection.", ExceptionSeverity.Error);
            throw;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger?.LogError($"Ollama API pull timeout: {ex.Message}");
            await exceptionHandlingService.HandleExceptionAsync(ex, "OllamaHttpService.PullModelWithProgressApiAsync",
                "Request timed out while pulling model. Please try again.", ExceptionSeverity.Error);
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger?.LogInformation("Ollama API pull was cancelled");
            throw; // Re-throw cancellation exceptions
        }
        catch (IOException ex)
        {
            logger?.LogError($"Ollama API pull IO error: {ex.Message}");
            await exceptionHandlingService.HandleExceptionAsync(ex, "OllamaHttpService.PullModelWithProgressApiAsync",
                "Network or file system error occurred while pulling model.", ExceptionSeverity.Error);
            throw;
        }
        catch (Exception ex)
        {
            logger?.LogError($"Ollama API pull unexpected error: {ex.Message}");
            await exceptionHandlingService.HandleExceptionAsync(ex, "OllamaHttpService.PullModelWithProgressApiAsync",
                "An unexpected error occurred while pulling the model.", ExceptionSeverity.Error);
            throw;
        }
    }

    /// <summary>
    /// Runs Ollama API asynchronously with a given prompt.
    /// </summary>
    /// <param name="request">The API request.</param>
    /// <param name="onOutput">Output callback.</param>
    /// <param name="onComplete">Completion callback.</param>
    /// <param name="onError">Error callback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RunOllamaApiAsync(OllamaApiRequest request, Action<string>? onOutput = null, Action? onComplete = null, Action<string>? onError = null, CancellationToken cancellationToken = default)
    {
        logger?.LogInformation($"RunOllamaApiAsync started with prompt: {request.Prompt}");

        try
        {
            if (_isDisposed)
            {
                logger?.LogWarning("OllamaHttpService is disposed, cannot make API call");
                onError?.Invoke("Service is shutting down");
                return;
            }

            await _apiLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_isDisposed)
                {
                    logger?.LogWarning("OllamaHttpService is disposed after acquiring lock");
                    onError?.Invoke("Service is shutting down");
                    return;
                }

                var content = new StringContent(
                    JsonSerializer.Serialize(request, options: new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                    }),
                    System.Text.Encoding.UTF8,
                    "application/json");

                logger?.LogInformation("Sending request to Ollama API");

                if (!await IsModelReadyAsync(request.Model, cancellationToken))
                {
                    logger?.LogError("Model is not ready for generation");
                    onError?.Invoke("Model is not ready for generation. Please try again.");
                    return;
                }

                var response = await _httpClient.PostAsync("/api/generate", content, cancellationToken);
                response.EnsureSuccessStatusCode();

                logger?.LogInformation("Response received from Ollama API");

                using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken))
                using (var reader = new StreamReader(stream))
                {
                    string? line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        if (!string.IsNullOrEmpty(line))
                        {
                            try
                            {
                                var json = JsonDocument.Parse(line);
                                if (json.RootElement.TryGetProperty("response", out var responseElement))
                                {
                                    if (_isDisposed)
                                    {
                                        logger?.LogWarning("OllamaHttpService is disposed, not sending output to UI");
                                        break;
                                    }

                                    string responseText = responseElement.GetString() ?? "";
                                    onOutput?.Invoke(responseText);
                                }

                                if (json.RootElement.TryGetProperty("done", out var doneElement) &&
                                    doneElement.GetBoolean())
                                {
                                    if (_isDisposed)
                                    {
                                        logger?.LogWarning("OllamaHttpService is disposed, not sending completion event to UI");
                                        break;
                                    }

                                    logger?.LogInformation("Ollama API processing completed");
                                    onComplete?.Invoke();
                                    break;
                                }
                            }
                            catch (JsonException ex)
                            {
                                logger?.LogError($"Error parsing JSON response: {ex.Message}");
                            }
                        }
                    }
                }
            }
            finally
            {
                _apiLock.Release();
            }
        }
        catch (Exception ex)
        {
            if (ex is ObjectDisposedException disposedEx)
            {
                logger?.LogError($"Ollama API error: Service is disposed - {disposedEx.Message}");
                onError?.Invoke("Service is shutting down");
            }
            else
            {
                logger?.LogError($"Ollama API error: {ex.Message}");
                onError?.Invoke($"Ollama API error: {ex.Message}");
            }
            onComplete?.Invoke();
        }

        logger?.LogInformation("RunOllamaApiAsync completed");
    }

    /// <summary>
    /// Performs a basic health check on Ollama service.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if Ollama is responsive, false otherwise.</returns>
    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            logger?.LogInformation("Performing Ollama health check");

            var response = await _httpClient.GetAsync("/api/tags", cancellationToken);
            response.EnsureSuccessStatusCode();

            logger?.LogInformation("Ollama health check passed");
            return true;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger?.LogError($"Ollama health check failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Checks if model is ready for generation.
    /// </summary>
    /// <param name="modelName">The model name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if model is ready, false otherwise.</returns>
    public async Task<bool> IsModelReadyAsync(string modelName, CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
        {
            logger?.LogWarning("OllamaHttpService is disposed, cannot check model readiness");
            return false;
        }

        try
        {
            logger?.LogInformation($"Checking if model {modelName} is ready...");

            var listResponse = await _httpClient.GetAsync("/api/tags", cancellationToken);
            listResponse.EnsureSuccessStatusCode();

            var listContent = await listResponse.Content.ReadAsStringAsync(cancellationToken);
            var listJson = JsonDocument.Parse(listContent);

            if (listJson.RootElement.TryGetProperty("models", out var modelsElement))
            {
                var models = modelsElement.EnumerateArray();
                foreach (var model in models)
                {
                    if (model.TryGetProperty("name", out var nameElement) &&
                        nameElement.GetString() == modelName)
                    {
                        logger?.LogInformation($"Model {modelName} found and ready");
                        return true;
                    }
                }
            }

            logger?.LogError($"Model {modelName} not found in model list");
            return false;
        }
        catch (Exception ex)
        {
            if (ex is ObjectDisposedException disposedEx)
            {
                logger?.LogError($"Error checking model readiness: Service is disposed - {disposedEx.Message}");
            }
            else
            {
                logger?.LogError($"Error checking model readiness: {ex.Message}");
            }
            return false;
        }
    }

    /// <summary>
    /// Parses progress information from API response.
    /// </summary>
    /// <param name="json">The JSON element to parse.</param>
    /// <returns>OllamaProgress object if successful, null otherwise.</returns>
    private OllamaProgress? ParseProgressFromApiResponse(JsonElement json)
    {
        try
        {
            var progress = new OllamaProgress
            {
                Status = json.TryGetProperty("status", out var statusElement)
                    ? statusElement.GetString() ?? ""
                    : ""
            };

            if (json.TryGetProperty("digest", out var digestElement))
            {
                progress.Digest = digestElement.GetString();
            }

            if (json.TryGetProperty("total", out var totalElement) &&
                json.TryGetProperty("completed", out var completedElement))
            {
                progress.Total = totalElement.GetInt64();
                progress.Completed = completedElement.GetInt64();

                if (progress.Total > 0)
                {
                    double rawPercentage = (progress.Completed * 100.0) / progress.Total;
                    progress.Percent = (int)Math.Round(rawPercentage);

                    if (progress.Percent == 0 || progress.Percent == 50 || progress.Percent == 100 ||
                        (progress.Status.Contains("error") || progress.Status.Contains("failed") ||
                         progress.Status.Contains("success") || progress.Status.Contains("manifest") ||
                         progress.Status.Contains("verifying")))
                    {
                        logger?.LogInformation($"Progress: {progress.Percent}% - {progress.Status}");
                    }
                }
                else
                {
                    logger?.LogInformation($"Progress calculation skipped - total is 0");
                }
            }

            if (progress.Total == 0)
            {
                if (progress.Status.Contains("pulling") || progress.Status.Contains("downloading"))
                {
                    progress.Percent = 0;
                }
                else if (progress.Status.Contains("verifying") || progress.Status.Contains("checking"))
                {
                    progress.Percent = 90;
                }
                else if (progress.Status.Contains("writing") || progress.Status.Contains("creating"))
                {
                    progress.Percent = 95;
                }
                else if (progress.Status.Contains("success"))
                {
                    progress.Percent = 100;
                }
            }

            return progress;
        }
        catch (Exception ex)
        {
            logger?.LogError($"Error parsing API progress data: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Disposes resources used by OllamaHttpService.
    /// </summary>
    public void Dispose()
    {
        logger?.LogInformation("OllamaHttpService Dispose called");

        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        try
        {
            if (_apiLock != null && _apiLock.CurrentCount == 1)
            {
                _apiLock.Dispose();
            }
            else if (_apiLock != null)
            {
                logger?.LogWarning("SemaphoreSlim not disposed - API calls may still be active");
            }
        }
        catch (Exception ex)
        {
            logger?.LogError($"Error disposing SemaphoreSlim: {ex.Message}");
        }

        logger?.LogInformation("OllamaHttpService Dispose completed");
    }
}