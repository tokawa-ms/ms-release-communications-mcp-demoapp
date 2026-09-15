using ReleaseCommunications.Core.Models;

namespace ReleaseCommunications.Core.Validation;

public static class SettingsValidator
{
    public static IReadOnlyList<string> Validate(AppSettings settings)
    {
        var errors = new List<string>();

        if (settings.MaximumItems is < 1 or > 500)
        {
            errors.Add("Maximum items must be between 1 and 500.");
        }

        if (settings.SummaryConcurrency is < 1 or > 8)
        {
            errors.Add("Summary concurrency must be between 1 and 8.");
        }

        if (settings.CacheRetentionDays is < 1 or > 90)
        {
            errors.Add("Cache retention must be between 1 and 90 days.");
        }

        if (settings.MaximumCacheEntries is < 10 or > 5000)
        {
            errors.Add("Maximum cache entries must be between 10 and 5000.");
        }

        return errors;
    }

    public static IReadOnlyList<string> ValidateFoundry(AppSettings settings)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(settings.FoundryProjectEndpoint) ||
            !Uri.TryCreate(settings.FoundryProjectEndpoint, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme != Uri.UriSchemeHttps)
        {
            errors.Add("Foundry OpenAI v1 endpoint must be an absolute HTTPS URI.");
        }
        else if (!endpoint.AbsolutePath.TrimEnd('/').EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(
                "Foundry OpenAI v1 endpoint must end in /openai/v1.");
        }

        if (string.IsNullOrWhiteSpace(settings.FoundryDeploymentName))
        {
            errors.Add("Foundry deployment name is required.");
        }
        else if (IsKnownImageGenerationDeployment(settings.FoundryDeploymentName))
        {
            errors.Add(
                $"Deployment '{settings.FoundryDeploymentName}' appears to be an image-generation model and cannot summarize text. " +
                "Use a text-capable chat or responses deployment.");
        }

        return errors;
    }

    public static IReadOnlyList<string> ValidateForStorage(AppSettings settings)
    {
        var errors = Validate(settings).ToList();
        if (!string.IsNullOrWhiteSpace(settings.FoundryProjectEndpoint) ||
            !string.IsNullOrWhiteSpace(settings.FoundryDeploymentName))
        {
            errors.AddRange(ValidateFoundry(settings));
        }

        return errors;
    }

    private static bool IsKnownImageGenerationDeployment(string deploymentName) =>
        deploymentName.Contains("gpt-image", StringComparison.OrdinalIgnoreCase) ||
        deploymentName.Contains("dall-e", StringComparison.OrdinalIgnoreCase);
}
