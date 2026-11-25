using System.Net;
using FabInspector.Core.Exceptions;

namespace FabInspector.ClientLibrary.Utils
{
    internal static class PublicRulesFileDownloader
    {
        internal const int MaximumBytes = 10_000_000;
        private const int MaximumRedirects = 5;
        private static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(30);
        private static readonly HttpClient Client = new(CreateHandler()) { Timeout = Timeout.InfiniteTimeSpan };

        internal static SocketsHttpHandler CreateHandler() => new()
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            UseProxy = true,
            Credentials = null,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
        };

        internal static string DisplayPath(string? path)
        {
            if (string.IsNullOrEmpty(path))
                return path ?? string.Empty;
            if (Uri.TryCreate(path, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
            {
                var builder = new UriBuilder(uri) { UserName = "", Password = "", Query = "", Fragment = "" };
                return builder.Uri.GetLeftPart(UriPartial.Path);
            }
            return path;
        }

        private static Uri ValidateUrl(string value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps || string.IsNullOrEmpty(uri.Host)
                || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query))
                throw new PBIRInspectorException("Rules URL must be an absolute HTTPS URL without user information or query strings.");
            if (OneLakeRulesFileDownloader.IsOneLakeDfsUrl(uri.AbsoluteUri))
                throw new PBIRInspectorException("OneLake rules URLs must use the authenticated OneLake downloader.");
            if (!Uri.UnescapeDataString(uri.AbsolutePath).EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                throw new PBIRInspectorException("Rules URL path must end in .json.");
            return uri;
        }

        private static async Task<HttpResponseMessage> SendAsync(
            HttpClient client, Uri uri, CancellationToken cancellationToken)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            for (int redirects = 0; ; redirects++)
            {
                if (!visited.Add(uri.AbsoluteUri))
                    throw new PBIRInspectorException("Rules URL redirect loop detected.");

                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                var response = await client.SendAsync(
                    request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                if ((int)response.StatusCode is not (301 or 302 or 303 or 307 or 308))
                    return response;

                using (response)
                {
                    if (redirects >= MaximumRedirects)
                        throw new PBIRInspectorException("Rules URL exceeded the redirect limit.");
                    var location = response.Headers.Location;
                    if (location == null || !Uri.TryCreate(uri, location, out var redirected))
                        throw new PBIRInspectorException("Rules URL returned an invalid redirect location.");
                    uri = ValidateUrl(redirected.AbsoluteUri);
                }
            }
        }

        private static void ValidateResponse(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
                throw new PBIRInspectorException($"Rules URL returned HTTP {(int)response.StatusCode}. Origin authentication is not supported.");
            var encodings = response.Content.Headers.ContentEncoding;
            if (encodings.All(encoding => encoding.Equals("identity", StringComparison.OrdinalIgnoreCase))
                && response.Content.Headers.ContentLength >= MaximumBytes)
                throw SizeLimitExceeded();
        }

        private static PBIRInspectorException SizeLimitExceeded()
            => new("Rules URL JSON payload must be less than 10 MB (10,000,000 bytes).");

        internal static async Task<MemoryStream> DownloadAsync(string url, Action<string>? onProgress = null, HttpClient? client = null)
        {
            var uri = ValidateUrl(url);
            onProgress?.Invoke($"Downloading rules file from URL '{DisplayPath(uri.AbsoluteUri)}'.");
            using var timeout = new CancellationTokenSource(DownloadTimeout);
            var httpClient = client ?? Client;
            try
            {
                using var response = await SendAsync(httpClient, uri, timeout.Token).ConfigureAwait(false);
                ValidateResponse(response);
                if (response.Content.Headers.ContentEncoding.Any(
                    encoding => !encoding.Equals("identity", StringComparison.OrdinalIgnoreCase)))
                    throw new PBIRInspectorException("Rules URL response uses unsupported content encoding.");

                await using var input = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
                var output = new MemoryStream();
                try
                {
                    var buffer = new byte[81920];
                    int read;
                    while ((read = await input.ReadAsync(
                        buffer.AsMemory(0, (int)Math.Min(buffer.Length, MaximumBytes - output.Length)),
                        timeout.Token).ConfigureAwait(false)) != 0)
                    {
                        if (output.Length + read >= MaximumBytes)
                            throw SizeLimitExceeded();
                        await output.WriteAsync(buffer.AsMemory(0, read), timeout.Token).ConfigureAwait(false);
                    }
                    output.Position = 0;
                    return output;
                }
                catch
                {
                    output.Dispose();
                    throw;
                }
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                throw new PBIRInspectorException("Rules URL download timed out.");
            }
            catch (HttpRequestException)
            {
                throw new PBIRInspectorException("Could not download rules URL. Check the HTTPS connection and proxy configuration.");
            }
            catch (IOException)
            {
                throw new PBIRInspectorException("Could not read the rules URL response.");
            }
        }
    }
}
