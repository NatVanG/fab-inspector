using System.Net;
using System.Net.Sockets;
using System.IO.Compression;
using FabInspector.Core.Exceptions;

namespace FabInspector.ClientLibrary.Utils
{
    internal static class PublicRulesFileDownloader
    {
        internal const int MaximumBytes = 10 * 1024 * 1024;
        private const int MaximumRedirects = 5;
        private static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(30);
        private static readonly HttpClient Client = CreateClient();

        private static HttpClient CreateClient()
        {
            var handler = new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
                UseProxy = false,
                AutomaticDecompression = DecompressionMethods.None,
                ConnectCallback = ConnectAsync
            };
            return new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        }

        private static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
        {
            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken).ConfigureAwait(false);
            if (addresses.Length == 0 || addresses.Any(address => !IsPublicAddress(address)))
                throw new HttpRequestException("The rules URL must resolve exclusively to public IP addresses.");

            SocketException? lastError = null;
            foreach (var address in addresses)
            {
                var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken).ConfigureAwait(false);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch (SocketException ex)
                {
                    lastError = ex;
                    socket.Dispose();
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            }

            throw new HttpRequestException("Could not connect to the rules URL.", lastError);
        }

        internal static bool IsPublicAddress(IPAddress address)
        {
            if (address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                var b = address.GetAddressBytes();
                // Only global unicast; exclude transition and special-purpose prefixes.
                return (b[0] & 0xe0) == 0x20
                    && !(b[0] == 0x20 && b[1] == 0x02)
                    && !(b[0] == 0x20 && b[1] == 0x01 && (b[2] & 0xfe) == 0)
                    && !(b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x0d && b[3] == 0xb8);
            }

            if (address.AddressFamily != AddressFamily.InterNetwork)
                return false;

            var octets = address.GetAddressBytes();
            int a = octets[0], b2 = octets[1], c = octets[2];
            return a != 0 && a != 10 && a != 127 && a < 224
                && !(a == 100 && b2 >= 64 && b2 <= 127)
                && !(a == 169 && b2 == 254)
                && !(a == 172 && b2 >= 16 && b2 <= 31)
                && !(a == 192 && b2 == 168)
                && !(a == 192 && b2 == 0 && (c == 0 || c == 2))
                && !(a == 192 && b2 == 88 && c == 99)
                && !(a == 198 && (b2 == 18 || b2 == 19 || (b2 == 51 && c == 100)))
                && !(a == 203 && b2 == 0 && c == 113);
        }

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
                || !string.IsNullOrEmpty(uri.UserInfo))
                throw new PBIRInspectorException("Rules URL must be an absolute HTTPS URL without user information.");
            if (OneLakeRulesFileDownloader.IsOneLakeDfsUrl(uri.AbsoluteUri))
                throw new PBIRInspectorException("OneLake rules URLs must use the authenticated OneLake downloader.");
            if (IPAddress.TryParse(uri.Host.Trim('[', ']'), out var address) && !IsPublicAddress(address))
                throw new PBIRInspectorException("Rules URL must use a public IP address.");
            return uri;
        }

        internal static async Task<MemoryStream> DownloadAsync(string url, Action<string>? onProgress = null, HttpClient? client = null)
        {
            var uri = ValidateUrl(url);
            onProgress?.Invoke($"Downloading rules file from URL '{DisplayPath(uri.AbsoluteUri)}'.");
            using var timeout = new CancellationTokenSource(DownloadTimeout);
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                for (int redirects = 0; ; redirects++)
                {
                    if (!visited.Add(uri.AbsoluteUri))
                        throw new PBIRInspectorException("Rules URL redirect loop detected.");
                    using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                    using var response = await (client ?? Client).SendAsync(
                        request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
                    if ((int)response.StatusCode is 301 or 302 or 303 or 307 or 308)
                    {
                        if (redirects >= MaximumRedirects)
                            throw new PBIRInspectorException("Rules URL exceeded the redirect limit.");
                        var location = response.Headers.Location;
                        if (location == null || !Uri.TryCreate(uri, location, out var redirected))
                            throw new PBIRInspectorException("Rules URL returned an invalid redirect location.");
                        uri = ValidateUrl(redirected.AbsoluteUri);
                        continue;
                    }

                    if (!response.IsSuccessStatusCode)
                        throw new PBIRInspectorException($"Rules URL returned HTTP {(int)response.StatusCode}.");
                    if (response.Content.Headers.ContentLength > MaximumBytes)
                        throw new PBIRInspectorException("Rules URL response exceeds the 10 MiB limit.");

                    await using var input = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
                    await using var boundedInput = new LimitedReadStream(input);
                    var encodings = response.Content.Headers.ContentEncoding;
                    if (encodings.Count > 1)
                        throw new PBIRInspectorException("Rules URL response uses unsupported content encoding.");
                    await using Stream? decompressor = encodings.FirstOrDefault()?.ToLowerInvariant() switch
                    {
                        null or "identity" => null,
                        "gzip" => new GZipStream(boundedInput, CompressionMode.Decompress, leaveOpen: true),
                        "deflate" => new DeflateStream(boundedInput, CompressionMode.Decompress, leaveOpen: true),
                        "br" => new BrotliStream(boundedInput, CompressionMode.Decompress, leaveOpen: true),
                        _ => throw new PBIRInspectorException("Rules URL response uses unsupported content encoding.")
                    };
                    var payload = decompressor ?? boundedInput;
                    var output = new MemoryStream();
                    try
                    {
                        var buffer = new byte[81920];
                        int read;
                        while ((read = await payload.ReadAsync(buffer, timeout.Token).ConfigureAwait(false)) != 0)
                        {
                            if (output.Length + read > MaximumBytes)
                                throw new PBIRInspectorException("Rules URL response exceeds the 10 MiB limit.");
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

                    private sealed class LimitedReadStream(Stream inner) : Stream
                    {
                        private long _bytesRead;
                        public override bool CanRead => true;
                        public override bool CanSeek => false;
                        public override bool CanWrite => false;
                        public override long Length => throw new NotSupportedException();
                        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
                        public override void Flush() => throw new NotSupportedException();
                        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
                        public override void SetLength(long value) => throw new NotSupportedException();
                        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
                        public override int Read(byte[] buffer, int offset, int count)
                        {
                            var read = inner.Read(buffer, offset, count);
                            Count(read);
                            return read;
                        }
                        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
                        {
                            var read = await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                            Count(read);
                            return read;
                        }
                        private void Count(int read)
                        {
                            _bytesRead += read;
                            if (_bytesRead > MaximumBytes)
                                throw new PBIRInspectorException("Rules URL response exceeds the 10 MiB limit.");
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                throw new PBIRInspectorException("Rules URL download timed out.");
            }
            catch (HttpRequestException)
            {
                throw new PBIRInspectorException("Could not download rules URL. Check the HTTPS connection and public address restrictions.");
            }
            catch (IOException)
            {
                throw new PBIRInspectorException("Could not read the rules URL response.");
            }
        }
    }
}
