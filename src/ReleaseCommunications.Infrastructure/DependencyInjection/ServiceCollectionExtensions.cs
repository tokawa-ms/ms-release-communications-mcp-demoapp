using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using ReleaseCommunications.Core.Abstractions;
using ReleaseCommunications.Core.UseCases;
using ReleaseCommunications.Infrastructure.Caching;
using ReleaseCommunications.Infrastructure.Articles;
using ReleaseCommunications.Infrastructure.Configuration;
using ReleaseCommunications.Infrastructure.Foundry;
using ReleaseCommunications.Infrastructure.Mrc;

namespace ReleaseCommunications.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddReleaseCommunications(this IServiceCollection services)
    {
        services.AddHttpClient("Mrc", client =>
        {
            client.Timeout = TimeSpan.FromMinutes(2);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ReleaseCommunications/1.0");
        });
        services.AddHttpClient("MicrosoftArticle", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ReleaseCommunications/1.0");
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = System.Net.DecompressionMethods.All
        });
        services.AddSingleton<AzureCliCredential>();
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<ISummaryCache, JsonSummaryCache>();
        services.AddSingleton<IMrcReleaseClient, MrcReleaseClient>();
        services.AddSingleton<IArticleContentClient, MicrosoftArticleContentClient>();
        services.AddSingleton<IArticleEnrichmentService, ArticleEnrichmentService>();
        services.AddSingleton<IReleaseAggregationService, ReleaseAggregationService>();
        services.AddSingleton<ISummarizationService, FoundrySummarizationService>();
        return services;
    }
}
