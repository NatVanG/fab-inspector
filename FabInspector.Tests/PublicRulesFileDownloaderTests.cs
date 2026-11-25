using System.IO.Compression;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using FabInspector.ClientLibrary;
using FabInspector.ClientLibrary.Utils;
using FabInspector.Core.Exceptions;

namespace FabInspector.Tests
{
    public class PublicRulesFileDownloaderTests
    {
        private const string RulesJson = """{"rules":[{"name":"Web rule","itemType":"none","test":[{"==":[1,1]},true]}]}""";
        private const string RulesUrl = "https://rules.example/rules.json";

        private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
        {
            public List<(HttpMethod Method, Uri? Uri)> Requests { get; } = [];

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add((request.Method, request.RequestUri));
                Assert.That(request.Headers.Authorization, Is.Null);
                Assert.That(request.Headers.Contains("Cookie"), Is.False);
                return Task.FromResult(respond(request));
            }
        }

        private static HttpResponseMessage Ok(string json = RulesJson)
            => new(HttpStatusCode.OK) { Content = new StringContent(json) };

        private static HttpResponseMessage Redirect(string? location)
        {
            var response = new HttpResponseMessage(HttpStatusCode.Found);
            if (location != null)
                response.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);
            return response;
        }

        [TestCase("http://rules.example/rules.json")]
        [TestCase("https://rules.example/rules.txt")]
        [TestCase("https://rules.example/rules")]
        [TestCase("https://user:secret@rules.example/rules.json")]
        [TestCase("https://rules.example/rules.json?token=secret")]
        [TestCase("https://onelake.dfs.fabric.microsoft.com/workspace/lakehouse/Files/rules.json")]
        public void DownloadAsync_RejectsInvalidUrlsBeforeRequests(string url)
        {
            using var handler = new StubHandler(_ => Ok());
            using var client = new HttpClient(handler);
            var ex = Assert.ThrowsAsync<PBIRInspectorException>(
                async () => { using var stream = await PublicRulesFileDownloader.DownloadAsync(url, client: client); });
            Assert.That(handler.Requests, Is.Empty);
            Assert.That(ex!.Message, Does.Not.Contain("secret"));
        }

        [Test]
        public async Task Loaders_UseSameAnonymousDownloaderForRulesAndCatalogUrlReferences()
        {
            var catalogPath = Path.Combine(Path.GetTempPath(), $"fab-catalog-{Guid.NewGuid():N}.json");
            using var handler = new StubHandler(_ => Ok());
            using var client = new HttpClient(handler);
            try
            {
                File.WriteAllText(catalogPath,
                    """{"ruleSets":[{"name":"Web","type":"url","path":"https://rules.example/rules.JSON"}]}""");
                var rules = await RulesFileLoader.DeserialiseRulesFromPathAsync(
                    "https://rules.example/rules.JSON", null, webClient: client);
                var catalog = await new RulesCatalogReader(null, webClient: client).ReadResolvedRuleSetsAsync(catalogPath);
                Assert.That(rules.Rules.Single().Name, Is.EqualTo("Web rule"));
                Assert.That(catalog.Single().Rules.Rules.Single().Name, Is.EqualTo("Web rule"));
                Assert.That(handler.Requests.Select(r => r.Method),
                    Is.EqualTo(new[] { HttpMethod.Get, HttpMethod.Get }));
            }
            finally
            {
                File.Delete(catalogPath);
            }
        }

        [Test]
        public void Catalog_RejectsCredentialBearingUrlWithSameGuardrails()
        {
            var catalogPath = Path.Combine(Path.GetTempPath(), $"fab-catalog-{Guid.NewGuid():N}.json");
            using var handler = new StubHandler(_ => Ok());
            using var client = new HttpClient(handler);
            try
            {
                File.WriteAllText(catalogPath,
                    """{"ruleSets":[{"name":"Web","type":"url","path":"https://rules.example/rules.json?token=secret"}]}""");
                var ex = Assert.ThrowsAsync<PBIRInspectorException>(async () =>
                    await new RulesCatalogReader(null, webClient: client).ReadResolvedRuleSetsAsync(catalogPath));
                Assert.That(handler.Requests, Is.Empty);
                Assert.That(ex!.Message, Does.Not.Contain("secret"));
            }
            finally
            {
                File.Delete(catalogPath);
            }
        }

        [Test]
        public async Task RulesLoader_PreservesLocalLoadingWithoutWebRequests()
        {
            var rulesPath = Path.Combine(Path.GetTempPath(), $"fab-rules-{Guid.NewGuid():N}.json");
            using var handler = new StubHandler(_ => Ok());
            using var client = new HttpClient(handler);
            try
            {
                File.WriteAllText(rulesPath, RulesJson);
                var rules = await RulesFileLoader.DeserialiseRulesFromPathAsync(rulesPath, null, webClient: client);
                Assert.That(rules.Rules.Single().Name, Is.EqualTo("Web rule"));
                Assert.That(handler.Requests, Is.Empty);
            }
            finally
            {
                File.Delete(rulesPath);
            }
        }

        [Test]
        public void RulesLoader_PreservesOneLakeAuthenticationRequirement()
        {
            using var handler = new StubHandler(_ => Ok());
            using var client = new HttpClient(handler);
            var ex = Assert.ThrowsAsync<PBIRInspectorException>(async () =>
                await RulesFileLoader.DeserialiseRulesFromPathAsync(
                    "https://onelake.dfs.fabric.microsoft.com/workspace/lakehouse/Files/rules.json", null, webClient: client));
            Assert.That(ex!.Message, Does.Contain("OneLake"));
            Assert.That(ex.InnerException!.Message, Does.Contain("requires authentication"));
            Assert.That(handler.Requests, Is.Empty);
        }

        [TestCase(HttpStatusCode.Unauthorized)]
        [TestCase(HttpStatusCode.Forbidden)]
        [TestCase(HttpStatusCode.NotFound)]
        [TestCase(HttpStatusCode.MethodNotAllowed)]
        [TestCase(HttpStatusCode.NotImplemented)]
        public void DownloadAsync_ReportsGetFailure(HttpStatusCode status)
        {
            using var handler = new StubHandler(_ => new HttpResponseMessage(status));
            using var client = new HttpClient(handler);
            var ex = Assert.ThrowsAsync<PBIRInspectorException>(
                async () => { using var stream = await PublicRulesFileDownloader.DownloadAsync(RulesUrl, client: client); });
            Assert.That(ex!.Message, Does.Contain($"HTTP {(int)status}"));
            Assert.That(handler.Requests.Count, Is.EqualTo(1));
        }

        [TestCase(10_000_000)]
        [TestCase(10_000_001)]
        public void DownloadAsync_RejectsDeclaredLimitOnGet(int bytes)
        {
            using var handler = new StubHandler(_ =>
            {
                var response = Ok();
                response.Content.Headers.ContentLength = bytes;
                return response;
            });
            using var client = new HttpClient(handler);
            var ex = Assert.ThrowsAsync<PBIRInspectorException>(
                async () => { using var stream = await PublicRulesFileDownloader.DownloadAsync(RulesUrl, client: client); });
            Assert.That(ex!.Message, Does.Contain("less than 10 MB"));
            Assert.That(handler.Requests.Count, Is.EqualTo(1));
        }

        [TestCase(9_999_999, true)]
        [TestCase(10_000_000, false)]
        public async Task DownloadAsync_EnforcesActualStrictBoundaryDespiteSmallHeaders(int bytes, bool accepted)
        {
            var json = RulesJson + new string(' ', bytes - Encoding.UTF8.GetByteCount(RulesJson));
            using var handler = new StubHandler(_ =>
            {
                var response = Ok(json);
                response.Content.Headers.ContentLength = 1;
                return response;
            });
            using var client = new HttpClient(handler);
            if (accepted)
            {
                var rules = await RulesFileLoader.DeserialiseRulesFromPathAsync(RulesUrl, null, webClient: client);
                Assert.That(rules.Rules.Single().Name, Is.EqualTo("Web rule"));
            }
            else
            {
                var ex = Assert.ThrowsAsync<PBIRInspectorException>(
                    async () => { using var stream = await PublicRulesFileDownloader.DownloadAsync(RulesUrl, client: client); });
                Assert.That(ex!.Message, Does.Contain("less than 10 MB"));
            }
        }

        [TestCase("http://rules.example/next.json")]
        [TestCase("https://rules.example/next.html")]
        [TestCase("https://rules.example/next.json?token=secret")]
        [TestCase("https://user:secret@rules.example/next.json")]
        [TestCase("https://onelake.dfs.fabric.microsoft.com/workspace/lakehouse/Files/rules.json")]
        [TestCase(null)]
        public void DownloadAsync_ValidatesRedirectBeforeFollowing(string? target)
        {
            using var handler = new StubHandler(_ => Redirect(target));
            using var client = new HttpClient(handler);
            var ex = Assert.ThrowsAsync<PBIRInspectorException>(
                async () => { using var stream = await PublicRulesFileDownloader.DownloadAsync(RulesUrl, client: client); });
            Assert.That(handler.Requests.Count, Is.EqualTo(1));
            Assert.That(ex!.Message, Does.Not.Contain("secret"));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void DownloadAsync_BoundsRedirectLoopsAndChains(bool loop)
        {
            var count = 0;
            using var handler = new StubHandler(_ => Redirect(loop ? RulesUrl : $"https://rules.example/{++count}.json"));
            using var client = new HttpClient(handler);
            var ex = Assert.ThrowsAsync<PBIRInspectorException>(
                async () => { using var stream = await PublicRulesFileDownloader.DownloadAsync(RulesUrl, client: client); });
            Assert.That(ex!.Message, Does.Contain(loop ? "loop" : "redirect limit"));
            Assert.That(handler.Requests.Count, Is.EqualTo(loop ? 1 : 6));
        }

        [Test]
        public async Task DownloadAsync_FollowsValidatedGetRedirect()
        {
            using var handler = new StubHandler(request =>
                request.RequestUri!.AbsolutePath == "/rules.json"
                    ? Redirect("/next.json") : Ok());
            using var client = new HttpClient(handler);
            using var stream = await PublicRulesFileDownloader.DownloadAsync(RulesUrl, client: client);
            Assert.That(handler.Requests.Last().Uri!.AbsolutePath, Is.EqualTo("/next.json"));
            Assert.That(handler.Requests.Count, Is.EqualTo(2));
            Assert.That(handler.Requests.All(r => r.Method == HttpMethod.Get), Is.True);
        }

        [Test]
        public void LoadRules_InvalidJsonReportsSourceWithoutFragment()
        {
            using var handler = new StubHandler(_ => Ok("not JSON"));
            using var client = new HttpClient(handler);
            var ex = Assert.ThrowsAsync<PBIRInspectorException>(async () =>
                await RulesFileLoader.DeserialiseRulesFromPathAsync(RulesUrl + "#secret", null, webClient: client));
            Assert.That(ex!.Message, Does.Contain("Could not deserialise"));
            Assert.That(ex.Message, Does.Not.Contain("secret"));
        }

        [TestCase(100)]
        [TestCase(10_000_000)]
        public async Task DownloadAsync_UsesHttpsProxyAndBoundsDecompressedBody(int payloadBytes)
        {
            using var key = RSA.Create(2048);
            var certificateRequest = new CertificateRequest("CN=rules.example", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var names = new SubjectAlternativeNameBuilder();
            names.AddDnsName("rules.example");
            certificateRequest.CertificateExtensions.Add(names.Build());
            using var generatedCertificate = certificateRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
            // Windows SslStream needs a non-ephemeral key; disposal removes the temporary key container.
            using var certificate = X509CertificateLoader.LoadPkcs12(generatedCertificate.Export(X509ContentType.Pfx), null);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            var payload = RulesJson + new string(' ', payloadBytes - Encoding.UTF8.GetByteCount(RulesJson));
            using var compressed = new MemoryStream();
            using (var gzip = new GZipStream(compressed, CompressionMode.Compress, leaveOpen: true))
                gzip.Write(Encoding.UTF8.GetBytes(payload));
            var body = compressed.ToArray();
            var proxyRequests = new List<string>();
            var server = ServeProxyAsync();
            using var handler = PublicRulesFileDownloader.CreateHandler();
            handler.Proxy = new WebProxy($"http://127.0.0.1:{endpoint.Port}");
            // Trust only this test origin's exact certificate, without changing system trust.
            handler.SslOptions.RemoteCertificateValidationCallback = (_, remote, _, errors) =>
                remote?.GetCertHashString() == certificate.GetCertHashString()
                && (errors & ~SslPolicyErrors.RemoteCertificateChainErrors) == SslPolicyErrors.None;
            using var client = new HttpClient(handler);
            try
            {
                if (payloadBytes < 10_000_000)
                {
                    using var stream = await PublicRulesFileDownloader.DownloadAsync(RulesUrl, client: client);
                    Assert.That(stream.Length, Is.EqualTo(payloadBytes));
                }
                else
                {
                    var ex = Assert.ThrowsAsync<PBIRInspectorException>(
                        async () => { using var stream = await PublicRulesFileDownloader.DownloadAsync(RulesUrl, client: client); });
                    Assert.That(ex!.Message, Does.Contain("less than 10 MB"));
                }
                await server;
                Assert.That(proxyRequests.Count, Is.EqualTo(1));
                Assert.That(proxyRequests.All(headers => headers.StartsWith("CONNECT rules.example:443 ")), Is.True);
            }
            finally
            {
                timeout.Cancel();
                listener.Stop();
                await server;
            }

            async Task ServeProxyAsync()
            {
                using var socket = await listener.AcceptTcpClientAsync(timeout.Token);
                using var network = socket.GetStream();
                proxyRequests.Add(await ReadHeadersAsync(network, timeout.Token));
                await network.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established\r\n\r\n"), timeout.Token);
                using var tls = new SslStream(network, leaveInnerStreamOpen: true);
                await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = certificate }, timeout.Token);
                var headers = await ReadHeadersAsync(tls, timeout.Token);
                Assert.That(headers, Does.StartWith("GET /rules.json "));
                Assert.That(headers, Does.Not.Contain("Authorization:").IgnoreCase);
                Assert.That(headers, Does.Not.Contain("Cookie:").IgnoreCase);
                var responseHeaders = "HTTP/1.1 200 OK\r\nContent-Encoding: gzip\r\nTransfer-Encoding: chunked\r\nConnection: close\r\n\r\n";
                await tls.WriteAsync(Encoding.ASCII.GetBytes(responseHeaders), timeout.Token);
                await tls.WriteAsync(Encoding.ASCII.GetBytes($"{body.Length:X}\r\n"), timeout.Token);
                await tls.WriteAsync(body, timeout.Token);
                await tls.WriteAsync(Encoding.ASCII.GetBytes("\r\n0\r\n\r\n"), timeout.Token);
                await tls.FlushAsync(timeout.Token);
            }
        }

        private static async Task<string> ReadHeadersAsync(Stream stream, CancellationToken cancellationToken)
        {
            var headers = new StringBuilder();
            var singleByte = new byte[1];
            while (headers.Length < 16_384)
            {
                if (await stream.ReadAsync(singleByte, cancellationToken) == 0)
                    throw new IOException("Connection closed before HTTP headers completed.");
                headers.Append((char)singleByte[0]);
                if (headers.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
                    return headers.ToString();
            }
            throw new IOException("Test HTTP headers exceeded the limit.");
        }
    }
}
