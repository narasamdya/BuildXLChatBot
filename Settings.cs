using System.Diagnostics.CodeAnalysis;

namespace BuildXLChatBot;

public record Settings(
    bool UseOpenAI,
    string Model,
    string EndPoint,
    string ApiKey,
    string OrgId)
{
    
    private const string EnvSettingsName = "OPENAI_SETTINGS";

    public static bool TryReadFromEnvironment([NotNullWhen(true)] out Settings? settings, out string? error)
    {
        settings = null;
        error = null;

        string? envVarSettings = Environment.GetEnvironmentVariable(EnvSettingsName);
        if (envVarSettings == null)
        {
            error = $"Environment variable {EnvSettingsName} not found";
            return false;
        }

        string[] parts = envVarSettings.Split(';');
        if (parts.Length != 5)
        {
            error = @$"The value of environment variable {EnvSettingsName} does not contain complete information.
Format: 'UseOpenAI;Model;EndPoint;ApiKey;OrgId'
Examples:
- 'true;gpt-3.5-turbo;;sk-12345;' - for OpenAI, with 'gpt-3.5-turbo' model and API key 'sk-12345'
- 'false;gpt-35-turbo-a;https://1es.openai.azure.com/;5e1ec7ed;' - for Azure OpenAI, with 'gpt-35-turbo-a' model, 'https://1es.openai.azure.com' endpoint, and API key '5e1ec7ed'";
            return false;
        }

        bool useOpenAI = bool.TryParse(parts[0].Trim(), out bool parsedUseOpenAI) && parsedUseOpenAI;
        string model = parts[1].Trim();
        string endPoint = parts[2].Trim();
        string apiKey = parts[3].Trim();
        string orgId = parts[4].Trim();

        settings = new Settings(useOpenAI, model, endPoint, apiKey, orgId);
        return true;
    }
}