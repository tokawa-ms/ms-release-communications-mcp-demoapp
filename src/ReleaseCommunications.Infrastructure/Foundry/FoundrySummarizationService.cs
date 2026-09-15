using Azure;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using ReleaseCommunications.Core.Abstractions;
using ReleaseCommunications.Core.Models;
using ReleaseCommunications.Core.UseCases;
using ReleaseCommunications.Core.Validation;
using System.ClientModel;
using System.ClientModel.Primitives;

namespace ReleaseCommunications.Infrastructure.Foundry;

#pragma warning disable OPENAI001
public sealed class FoundrySummarizationService(
    AzureCliCredential credential,
    ISettingsStore settingsStore,
    ISummaryCache cache,
    ILogger<FoundrySummarizationService> logger) : ISummarizationService
{
    private static readonly Action<ILogger, int, TimeSpan, Exception?> RetryWarning =
        LoggerMessage.Define<int, TimeSpan>(
            LogLevel.Warning,
            new EventId(2001, nameof(RetryWarning)),
            "Foundry request attempt {Attempt} was throttled or unavailable; retrying in {Delay}.");

    public async Task<ArticleSummary> SummarizeAsync(
        EnrichedReleaseArticle article,
        SummaryLanguage language,
        CancellationToken cancellationToken)
    {
        var settings = await settingsStore.LoadAsync(cancellationToken);
        EnsureValid(settings);
        var key = SummaryCacheKey.Create(article, language, settings.FoundryDeploymentName);
        var cached = await cache.TryGetAsync(key, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var prompt = SummaryPromptBuilder.Build(article, language);
        var responseText = await ExecuteWithRetryAsync(settings, prompt, cancellationToken);
        var summary = SummaryResponseParser.Parse(responseText, language, DateTimeOffset.UtcNow) with
        {
            ContentStatus = article.ContentStatus,
            ContentDiagnostics = article.Diagnostics
        };
        await cache.SetAsync(key, summary, cancellationToken);
        return summary;
    }

    public async Task<ConnectionDiagnostic> CheckConnectionAsync(
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        var errors = SettingsValidator.Validate(settings)
            .Concat(SettingsValidator.ValidateFoundry(settings))
            .ToArray();
        if (errors.Length > 0)
        {
            return new ConnectionDiagnostic("Foundry", false, string.Join(" ", errors));
        }

        try
        {
            await credential.GetTokenAsync(
                new TokenRequestContext(["https://ai.azure.com/.default"]),
                cancellationToken);

            var client = CreateChatClient(settings);
            var response = await client.CompleteChatAsync(
                [new UserChatMessage("Reply with the single word OK.")],
                CreateOptions(),
                cancellationToken);
            var output = GetOutputText(response.Value);
            return string.IsNullOrWhiteSpace(output)
                ? new ConnectionDiagnostic("Foundry", false, "Foundry returned an empty response.")
                : new ConnectionDiagnostic("Foundry", true, "Azure CLI authentication and model deployment succeeded.");
        }
        catch (CredentialUnavailableException ex)
        {
            return new ConnectionDiagnostic("Azure CLI", false, $"Azure CLI is unavailable: {ex.Message}");
        }
        catch (AuthenticationFailedException ex)
        {
            return new ConnectionDiagnostic("Azure CLI", false, $"Run 'az login' for the intended tenant: {ex.Message}");
        }
        catch (RequestFailedException ex) when (ex.Status == 403)
        {
            return new ConnectionDiagnostic("Foundry", false, "Authenticated, but RBAC does not permit model inference.");
        }
        catch (ClientResultException ex) when (ex.Status == 403)
        {
            return new ConnectionDiagnostic("Foundry", false, "Authenticated, but RBAC does not permit model inference.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ConnectionDiagnostic("Foundry", false, ex.Message);
        }
    }

    private async Task<string> ExecuteWithRetryAsync(
        AppSettings settings,
        string prompt,
        CancellationToken cancellationToken)
    {
        var client = CreateChatClient(settings);

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(90));

            try
            {
                var response = await client.CompleteChatAsync(
                    [new UserChatMessage(prompt)],
                    CreateOptions(),
                    timeout.Token);
                var output = GetOutputText(response.Value);
                if (string.IsNullOrWhiteSpace(output))
                {
                    throw new FormatException("Foundry returned an empty summary.");
                }

                return output;
            }
            catch (RequestFailedException ex) when (
                attempt < 3 &&
                (ex.Status == 429 || ex.Status >= 500))
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                RetryWarning(logger, attempt, delay, ex);
                await Task.Delay(delay, cancellationToken);
            }
            catch (ClientResultException ex) when (
                attempt < 3 &&
                (ex.Status == 429 || ex.Status >= 500))
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                RetryWarning(logger, attempt, delay, ex);
                await Task.Delay(delay, cancellationToken);
            }
        }

        throw new InvalidOperationException("Foundry summary retry policy ended unexpectedly.");
    }

    private static void EnsureValid(AppSettings settings)
    {
        var errors = SettingsValidator.Validate(settings)
            .Concat(SettingsValidator.ValidateFoundry(settings))
            .ToArray();
        if (errors.Length > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }
    }

    private ChatClient CreateChatClient(AppSettings settings)
    {
        BearerTokenPolicy tokenPolicy = new(credential, "https://ai.azure.com/.default");
        return new ChatClient(
            model: settings.FoundryDeploymentName,
            authenticationPolicy: tokenPolicy,
            options: new OpenAIClientOptions
            {
                Endpoint = NormalizeEndpoint(settings.FoundryProjectEndpoint)
            });
    }

    private static Uri NormalizeEndpoint(string endpoint) =>
        new(endpoint.EndsWith('/') ? endpoint : $"{endpoint}/");

    private static ChatCompletionOptions CreateOptions() =>
        new()
        {
            MaxOutputTokenCount = 1200
        };

    private static string GetOutputText(ChatCompletion completion) =>
        string.Concat(completion.Content.Select(part => part.Text));
}
#pragma warning restore OPENAI001
