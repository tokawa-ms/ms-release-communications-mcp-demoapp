using System.Net;
using System.Net.Http.Headers;
using ReleaseCommunications.Core.Abstractions;
using ReleaseCommunications.Core.Models;

namespace ReleaseCommunications.Infrastructure.Articles;

public sealed class MicrosoftArticleContentClient(IHttpClientFactory httpClientFactory)
    : IArticleContentClient
{
    private const int MaximumRedirects = 5;
    private const int MaximumResponseBytes = 2_000_000;

    public async Task<ArticlePageContent> FetchAsync(
        Uri uri,
        CancellationToken cancellationToken)
    {
        if (!MicrosoftArticleUrlPolicy.IsAllowed(uri))
        {
            return Failure(uri, $"Blocked untrusted or non-HTTPS article URL: {uri}");
        }

        var client = httpClientFactory.CreateClient("MicrosoftArticle");
        var current = uri;
        for (var redirect = 0; redirect <= MaximumRedirects; redirect++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Failure(uri, $"Could not fetch {current.Host}: {ex.Message}");
            }

            using (response)
            {
                if (IsRedirect(response.StatusCode))
                {
                    if (redirect == MaximumRedirects)
                    {
                        return Failure(uri, "The linked Microsoft page exceeded the redirect limit.");
                    }

                    var location = response.Headers.Location;
                    if (location is null)
                    {
                        return Failure(uri, "The linked Microsoft page returned a redirect without a location.");
                    }

                    var next = location.IsAbsoluteUri ? location : new Uri(current, location);
                    if (!MicrosoftArticleUrlPolicy.IsAllowed(next))
                    {
                        return Failure(
                            uri,
                            $"Blocked redirect from {current.Host} to untrusted URL {next}.");
                    }

                    current = next;
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    return Failure(
                        uri,
                        $"The linked Microsoft page returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).");
                }

                var mediaType = response.Content.Headers.ContentType?.MediaType;
                if (!string.Equals(mediaType, "text/html", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(mediaType, "application/xhtml+xml", StringComparison.OrdinalIgnoreCase))
                {
                    return Failure(
                        uri,
                        $"The linked Microsoft URL returned unsupported content type '{mediaType ?? "unknown"}'.");
                }

                if (response.Content.Headers.ContentLength is > MaximumResponseBytes)
                {
                    return Failure(
                        uri,
                        $"The linked Microsoft page exceeded the {MaximumResponseBytes:N0}-byte download limit.");
                }

                try
                {
                    var html = await ReadBoundedAsync(
                        await response.Content.ReadAsStreamAsync(cancellationToken),
                        cancellationToken);
                    var text = await HtmlArticleTextExtractor.ExtractAsync(html, cancellationToken);
                    return new ArticlePageContent(
                        true,
                        uri,
                        current,
                        text,
                        $"Read linked Microsoft article content from {current.Host}.");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    return Failure(uri, $"Could not extract article content from {current.Host}: {ex.Message}");
                }
            }
        }

        return Failure(uri, "The linked Microsoft page could not be fetched.");
    }

    private static async Task<string> ReadBoundedAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[16_384];
        while (true)
        {
            var read = await stream.ReadAsync(chunk, cancellationToken);
            if (read == 0)
            {
                break;
            }

            if (buffer.Length + read > MaximumResponseBytes)
            {
                throw new InvalidDataException(
                    $"The linked Microsoft page exceeded the {MaximumResponseBytes:N0}-byte download limit.");
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        buffer.Position = 0;
        using var reader = new StreamReader(
            buffer,
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: false);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static bool IsRedirect(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.Moved or
            HttpStatusCode.Redirect or
            HttpStatusCode.RedirectMethod or
            HttpStatusCode.TemporaryRedirect or
            HttpStatusCode.PermanentRedirect;

    private static ArticlePageContent Failure(Uri uri, string diagnostic) =>
        new(false, uri, null, null, diagnostic);
}
