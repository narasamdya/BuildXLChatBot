using System.ComponentModel;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.SemanticKernel;

namespace BuildXLChatBot.Plugins;

public sealed class CBPlugin(ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<CBPlugin>();

    [KernelFunction]
    [Description("Create a JSON formatted CloudBuild (or CB) build request for a specific build queue, and optionally with a description, a specify engine or drop, and additional command-line options or arguments.")]
    public string CreateBuildRequest(
        [Description("The build queue in CloudBuild")] string buildQueue,
        [Description("An optional description for the build request")] string? description,
        [Description("An optional BuildXL engine or BuildXL drop")] string? engineOrDrop,
        [Description("Optional additional command-line options or arguments")] string? additionalCommandLineOptions)
    {
        _logger.LogInformation("Create a build request: {buildQueue}", buildQueue);

        if (string.IsNullOrWhiteSpace(buildQueue))
        {
            throw new ArgumentException("Build queue is required", nameof(buildQueue));
        }

        string requestor = Environment.GetEnvironmentVariable("USERNAME") ?? "unknown";

        var cbBuildRequest = new CBBuildRequest
        {
            BuildQueue = buildQueue,
            Requester = requestor,
            Description = string.IsNullOrWhiteSpace(description)
                ? $"BuildXL build requested by {requestor} - {DateTime.UtcNow}"
                : description,
        };

        if (!string.IsNullOrWhiteSpace(engineOrDrop))
        {
            engineOrDrop = !engineOrDrop.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                ? $"https://cloudbuild.artifacts.visualstudio.com/DefaultCollection/_apis/drop/drops/{engineOrDrop}"
                : engineOrDrop;

            engineOrDrop = !engineOrDrop.Contains("?root=", StringComparison.OrdinalIgnoreCase)
                ? $"{engineOrDrop}?root=release/win-x64"
                : engineOrDrop;

            cbBuildRequest.ToolPaths = new ToolPaths
            {
                DominoEngine = engineOrDrop
            };
        }

        if (!string.IsNullOrWhiteSpace(additionalCommandLineOptions))
        {
            cbBuildRequest.BuildEngineOptions = new BuildEngineOptions
            {
                Additionalcommandlineflags = additionalCommandLineOptions
            };
        }

        return JsonSerializer.Serialize(
            cbBuildRequest,
            new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
    }

    [KernelFunction]
    [Description("Submit a JSON formatted CloudBuild (or CB) build request to the CloudBuild service")]
    public async Task<string> SubmitBuildRequestAsync([Description("A CloudBuild (or CB) build request in JSON format")] string buildRequest)
    {
        _logger.LogInformation("Submitting build request: {buildRequest}", buildRequest);

        if (string.IsNullOrWhiteSpace(buildRequest))
        {
            throw new ArgumentException("Build request is required", nameof(buildRequest));
        }

        var app = PublicClientApplicationBuilder.Create("a5a5dbed-8a88-40d9-94f3-f62bad35ad07")
            .WithTenantId("microsoft.com")
            .WithRedirectUri("http://localhost")
            .Build();

        AuthenticationResult? authResult;

        try
        {
            authResult = await app.AcquireTokenInteractive(["https://cloudbuild.microsoft.com/.default"])
                .WithPrompt(Prompt.SelectAccount)
                .WithUseEmbeddedWebView(false).ExecuteAsync();
        }
        catch (MsalUiRequiredException)
        {
            return "Failed to submit build because interactive authentication is required";
        }
        catch (MsalException ex)
        {
            return $"Failed to submit build because of an error acquiring token: {ex.Message}";
        }

        using var client = new HttpClient
        {
            BaseAddress = new Uri("https://cloudbuild.microsoft.com/")
        };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authResult.AccessToken);
        var submitUri = $"ScheduleBuild/submit";

        using var jsonContent = new StringContent(buildRequest, Encoding.UTF8, "application/json");

        try
        {
            var response = await client.PostAsync(submitUri, jsonContent);
            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = await response.Content.ReadAsStringAsync();
                return $"Failed to submit buildRequest: '{submitUri}' ({response.StatusCode}): {errorMessage}.";
            }

            var resultjson = await response.Content.ReadAsStringAsync();
            using var jDoc = JsonDocument.Parse(resultjson);
            return JsonSerializer.Serialize(jDoc, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception)
        {
            throw;
        }
    }

    private class CBBuildRequest
    {
        public string BuildQueue { get; set; } = string.Empty;
        public string Requester { get; set; } = string.Empty;
        public string? Description { get; set; }
        public ToolPaths? ToolPaths { get; set; }
        public BuildEngineOptions? BuildEngineOptions { get; set; }
    }

    private class ToolPaths
    {
        public string? DominoEngine { get; set; }
    }

    private class BuildEngineOptions
    {
        public string? Additionalcommandlineflags { get; set; }
    }
}