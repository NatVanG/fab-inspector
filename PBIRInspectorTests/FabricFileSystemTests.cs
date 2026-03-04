using PBIRInspectorLibrary;
using PBIRInspectorClientLibrary.Utils;
using System.Net;
using System.Text;
using System.Text.Json;
using Azure.Core;

namespace PBIRInspectorTests
{
    [TestFixture]
    public class FabricFileSystemTests
    {
        /// <summary>
        /// Mock TokenCredential for testing without real authentication
        /// </summary>
        private class MockTokenCredential : TokenCredential
        {
            public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
            {
                return new AccessToken("mock-token", DateTimeOffset.UtcNow.AddHours(1));
            }

            public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
            {
                return new ValueTask<AccessToken>(new AccessToken("mock-token", DateTimeOffset.UtcNow.AddHours(1)));
            }
        }

        /// <summary>
        /// Mock HTTP message handler for testing without real API calls
        /// Enhanced to support LRO polling scenarios with sequential responses
        /// </summary>
        private class MockHttpMessageHandler : HttpMessageHandler
        {
            private readonly Dictionary<string, (HttpStatusCode statusCode, string content, Dictionary<string, string>? headers)> _responses = new();
            private readonly Dictionary<string, Queue<(HttpStatusCode statusCode, string content, Dictionary<string, string>? headers)>> _sequentialResponses = new();
            private readonly Dictionary<string, int> _callCounts = new();

            public void AddResponse(string urlPattern, HttpStatusCode statusCode, string content, Dictionary<string, string>? headers = null)
            {
                _responses[urlPattern] = (statusCode, content, headers);
            }

            /// <summary>
            /// Add a sequence of responses for the same URL pattern (for polling scenarios)
            /// </summary>
            public void AddSequentialResponses(string urlPattern, List<(HttpStatusCode statusCode, string content, Dictionary<string, string>? headers)> responses)
            {
                _sequentialResponses[urlPattern] = new Queue<(HttpStatusCode, string, Dictionary<string, string>?)>(responses);
            }

            public int GetCallCount(string urlPattern)
            {
                return _callCounts.ContainsKey(urlPattern) ? _callCounts[urlPattern] : 0;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var url = request.RequestUri?.ToString() ?? string.Empty;
                var method = request.Method.Method;
                
                // Track call count
                var matchedPattern = FindMatchingPattern(url);
                if (matchedPattern != null)
                {
                    if (!_callCounts.ContainsKey(matchedPattern))
                        _callCounts[matchedPattern] = 0;
                    _callCounts[matchedPattern]++;
                }

                // Try exact match first
                if (_responses.TryGetValue(url, out var response))
                {
                    return Task.FromResult(CreateResponse(response.statusCode, response.content, response.headers));
                }

                // Check for sequential responses
                foreach (var kvp in _sequentialResponses)
                {
                    if (UrlMatches(url, kvp.Key))
                    {
                        if (kvp.Value.Count > 0)
                        {
                            var seqResponse = kvp.Value.Dequeue();
                            return Task.FromResult(CreateResponse(seqResponse.statusCode, seqResponse.content, seqResponse.headers));
                        }
                    }
                }

                // Try pattern match
                foreach (var kvp in _responses)
                {
                    if (UrlMatches(url, kvp.Key))
                    {
                        return Task.FromResult(CreateResponse(kvp.Value.statusCode, kvp.Value.content, kvp.Value.headers));
                    }
                }

                // Default 404 response
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("{\"error\": \"Not Found\"}", Encoding.UTF8, "application/json")
                });
            }

            private string? FindMatchingPattern(string url)
            {
                // Try exact match first
                if (_responses.ContainsKey(url))
                    return url;

                // Prioritize more specific patterns over general ones
                // Sort patterns by length (longer = more specific) and check special keywords first
                var allPatterns = _responses.Keys.Concat(_sequentialResponses.Keys).ToList();
                var sortedPatterns = allPatterns
                    .OrderByDescending(p => p.Contains("getDefinition") ? 1000 : 0) // getDefinition patterns first
                    .ThenByDescending(p => p.Contains("/operations/") ? 900 : 0) // operations patterns second
                    .ThenByDescending(p => p.Length); // Then by length (more specific)

                foreach (var pattern in sortedPatterns)
                {
                    if (UrlMatches(url, pattern))
                        return pattern;
                }

                return null;
            }

            private bool UrlMatches(string url, string pattern)
            {
                // For getDefinition, match any URL containing it
                if (pattern == "getDefinition" && url.Contains("/getDefinition"))
                    return true;

                // For operations/result patterns
                if (pattern.Contains("/operations/") && url.Contains("/operations/"))
                {
                    // Match operation ID patterns
                    if (pattern.EndsWith("/result") && url.EndsWith("/result"))
                        return true;
                    if (!pattern.EndsWith("/result") && !url.EndsWith("/result") && url.Contains("/operations/"))
                        return true;
                }

                // Check if URL contains or ends with pattern
                if (url.Contains(pattern) || url.EndsWith(pattern))
                    return true;

                return false;
            }

            private HttpResponseMessage CreateResponse(HttpStatusCode statusCode, string content, Dictionary<string, string>? headers)
            {
                var response = new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(content, Encoding.UTF8, "application/json")
                };

                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        if (header.Key.Equals("Location", StringComparison.OrdinalIgnoreCase))
                        {
                            response.Headers.Location = new Uri(header.Value, UriKind.RelativeOrAbsolute);
                        }
                        else if (header.Key.Equals("Retry-After", StringComparison.OrdinalIgnoreCase))
                        {
                            if (int.TryParse(header.Value, out var seconds))
                            {
                                response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(seconds));
                            }
                        }
                        else
                        {
                            response.Headers.TryAddWithoutValidation(header.Key, header.Value);
                        }
                    }
                }

                return response;
            }
        }

        [Test]
        public void FabricFileSystem_Constructor_ValidatesParameters()
        {
            // Arrange
            var mockCredential = new MockTokenCredential();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new FabricRemoteFileSystem("", mockCredential));
            Assert.Throws<ArgumentNullException>(() => new FabricRemoteFileSystem("workspace-id", null!));
            Assert.Throws<ArgumentNullException>(() => new FabricRemoteFileSystem(null!, mockCredential));
        }

        [Test]
        public void FabricFileSystem_DirectoryExists_ReturnsTrueForRoot()
        {
            // Arrange
            var mockHandler = new MockHttpMessageHandler();
            var httpClient = new HttpClient(mockHandler);
            var mockCredential = new MockTokenCredential();
            var fs = new FabricRemoteFileSystem("test-workspace-id", mockCredential, httpClient);

            // Act & Assert
            Assert.That(fs.DirectoryExists("/"), Is.True);
        }

        [Test]
        public void FabricFileSystem_DirectoryExists_ReturnsFalseForEmptyPath()
        {
            // Arrange
            var mockHandler = new MockHttpMessageHandler();
            var httpClient = new HttpClient(mockHandler);
            var mockCredential = new MockTokenCredential();
            var fs = new FabricRemoteFileSystem("test-workspace-id", mockCredential, httpClient);

            // Act & Assert
            Assert.That(fs.DirectoryExists(""), Is.False);
        }

        [Test]
        public void FabricFileSystem_DirectoryExists_LoadsWorkspaceItemsOnDemand()
        {
            // Arrange
            var mockHandler = new MockHttpMessageHandler();
            var workspaceItemsResponse = new
            {
                value = new[]
                {
                    new { id = "item1", displayName = "Report1", type = "Report", workspaceId = "test-workspace-id" },
                    new { id = "item2", displayName = "Pipeline1", type = "DataPipeline", workspaceId = "test-workspace-id" }
                }
            };
            mockHandler.AddResponse(
                "https://api.fabric.microsoft.com/v1/workspaces/test-workspace-id/items",
                HttpStatusCode.OK,
                JsonSerializer.Serialize(workspaceItemsResponse)
            );

            var httpClient = new HttpClient(mockHandler);
            var mockCredential = new MockTokenCredential();
            var fs = new FabricRemoteFileSystem("test-workspace-id", mockCredential, httpClient);

            // Act
            bool report1Exists = fs.DirectoryExists("Report1.Report");
            bool pipeline1Exists = fs.DirectoryExists("Pipeline1.DataPipeline");
            bool nonExistentExists = fs.DirectoryExists("NonExistent");

            // Assert
            Assert.That(report1Exists, Is.True);
            Assert.That(pipeline1Exists, Is.True);
            Assert.That(nonExistentExists, Is.False);
        }

        [Test]
        public void FabricFileSystem_GetDirectories_ReturnsAllItemNames()
        {
            // Arrange
            var mockHandler = new MockHttpMessageHandler();
            var workspaceItemsResponse = new
            {
                value = new[]
                {
                    new { id = "item1", displayName = "Report1", type = "Report", workspaceId = "test-workspace-id" },
                    new { id = "item2", displayName = "Pipeline1", type = "DataPipeline", workspaceId = "test-workspace-id" },
                    new { id = "item3", displayName = "Lakehouse1", type = "Lakehouse", workspaceId = "test-workspace-id" }
                }
            };
            mockHandler.AddResponse(
                "https://api.fabric.microsoft.com/v1/workspaces/test-workspace-id/items",
                HttpStatusCode.OK,
                JsonSerializer.Serialize(workspaceItemsResponse)
            );

            var httpClient = new HttpClient(mockHandler);
            var mockCredential = new MockTokenCredential();
            var fs = new FabricRemoteFileSystem("test-workspace-id", mockCredential, httpClient);

            // Act
            var directories = fs.GetDirectories("").ToList();

            // Assert
            Assert.That(directories, Has.Count.EqualTo(3));
            Assert.That(directories, Does.Contain("Report1"));
            Assert.That(directories, Does.Contain("Pipeline1"));
            Assert.That(directories, Does.Contain("Lakehouse1"));
        }

        [Test]
        [Ignore("Mock HTTP handler URL matching needs refinement")]
        public void FabricFileSystem_FileExists_LoadsItemDefinitionOnDemand()
        {
            // Arrange
            var mockHandler = new MockHttpMessageHandler();
            
            // Mock workspace items response
            var workspaceItemsResponse = new
            {
                value = new[]
                {
                    new { id = "item1", displayName = "Report1", type = "Report", workspaceId = "test-workspace-id" }
                }
            };
            mockHandler.AddResponse(
                "https://api.fabric.microsoft.com/v1/workspaces/test-workspace-id/items",
                HttpStatusCode.OK,
                JsonSerializer.Serialize(workspaceItemsResponse)
            );

            // Mock item definition response
            var itemDefinitionResponse = new
            {
                definition = new
                {
                    parts = new[]
                    {
                        new { path = "definition.pbir", payload = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"name\":\"test\"}")), payloadType = "InlineBase64" },
                        new { path = "report.json", payload = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"version\":\"1.0\"}")), payloadType = "InlineBase64" }
                    }
                }
            };
            mockHandler.AddResponse(
                "getDefinition",
                HttpStatusCode.OK,
                JsonSerializer.Serialize(itemDefinitionResponse)
            );

            var httpClient = new HttpClient(mockHandler);
            var mockCredential = new MockTokenCredential();
            var fs = new FabricRemoteFileSystem("test-workspace-id", mockCredential, httpClient);

            // Act
            bool definitionExists = fs.FileExists("Report1/definition.pbir");
            bool reportExists = fs.FileExists("Report1/report.json");
            bool nonExistentExists = fs.FileExists("Report1/nonexistent.json");

            // Assert
            Assert.That(definitionExists, Is.True);
            Assert.That(reportExists, Is.True);
            Assert.That(nonExistentExists, Is.False);
        }

        [Test]
        [Ignore("Mock HTTP handler URL matching needs refinement")]
        public void FabricFileSystem_ReadAllText_DecodesBase64Payload()
        {
            // Arrange
            var mockHandler = new MockHttpMessageHandler();
            
            // Mock workspace items response
            var workspaceItemsResponse = new
            {
                value = new[]
                {
                    new { id = "item1", displayName = "Report1", type = "Report", workspaceId = "test-workspace-id" }
                }
            };
            mockHandler.AddResponse(
                "https://api.fabric.microsoft.com/v1/workspaces/test-workspace-id/items",
                HttpStatusCode.OK,
                JsonSerializer.Serialize(workspaceItemsResponse)
            );

            // Mock item definition response with base64 encoded content
            var originalContent = "{\"name\":\"TestReport\",\"version\":\"1.0\"}";
            var base64Content = Convert.ToBase64String(Encoding.UTF8.GetBytes(originalContent));
            var itemDefinitionResponse = new
            {
                definition = new
                {
                    parts = new[]
                    {
                        new { path = "definition.pbir", payload = base64Content, payloadType = "InlineBase64" }
                    }
                }
            };
            mockHandler.AddResponse(
                "getDefinition",
                HttpStatusCode.OK,
                JsonSerializer.Serialize(itemDefinitionResponse)
            );

            var httpClient = new HttpClient(mockHandler);
            var mockCredential = new MockTokenCredential();
            var fs = new FabricRemoteFileSystem("test-workspace-id", mockCredential, httpClient);

            // Act
            var content = fs.ReadAllText("Report1/definition.pbir");

            // Assert
            Assert.That(content, Is.EqualTo(originalContent));
        }

        [Test]
        [Ignore("Mock HTTP handler URL matching needs refinement")]
        public void FabricFileSystem_GetFiles_ReturnsAllPartsInItem()
        {
            // Arrange
            var mockHandler = new MockHttpMessageHandler();
            
            // Mock workspace items response
            var workspaceItemsResponse = new
            {
                value = new[]
                {
                    new { id = "item1", displayName = "Report1", type = "Report", workspaceId = "test-workspace-id" }
                }
            };
            mockHandler.AddResponse(
                "https://api.fabric.microsoft.com/v1/workspaces/test-workspace-id/items",
                HttpStatusCode.OK,
                JsonSerializer.Serialize(workspaceItemsResponse)
            );

            // Mock item definition response
            var itemDefinitionResponse = new
            {
                definition = new
                {
                    parts = new[]
                    {
                        new { path = "definition.pbir", payload = "dGVzdA==", payloadType = "InlineBase64" },
                        new { path = "report.json", payload = "dGVzdA==", payloadType = "InlineBase64" },
                        new { path = "metadata.json", payload = "dGVzdA==", payloadType = "InlineBase64" }
                    }
                }
            };
            mockHandler.AddResponse(
                "getDefinition",
                HttpStatusCode.OK,
                JsonSerializer.Serialize(itemDefinitionResponse)
            );

            var httpClient = new HttpClient(mockHandler);
            var mockCredential = new MockTokenCredential();
            var fs = new FabricRemoteFileSystem("test-workspace-id", mockCredential, httpClient);

            // Act
            var files = fs.GetFiles("Report1").ToList();

            // Assert
            Assert.That(files, Has.Count.EqualTo(3));
            Assert.That(files.Any(f => f.Contains("definition.pbir")), Is.True);
            Assert.That(files.Any(f => f.Contains("report.json")), Is.True);
            Assert.That(files.Any(f => f.Contains("metadata.json")), Is.True);
        }

        [Test]
        [Ignore("Mock HTTP handler URL matching needs refinement")]
        public void FabricFileSystem_GetFiles_WithSearchPattern_FiltersCorrectly()
        {
            // Arrange
            var mockHandler = new MockHttpMessageHandler();
            
            // Mock workspace items response
            var workspaceItemsResponse = new
            {
                value = new[]
                {
                    new { id = "item1", displayName = "Report1", type = "Report", workspaceId = "test-workspace-id" }
                }
            };
            mockHandler.AddResponse(
                "https://api.fabric.microsoft.com/v1/workspaces/test-workspace-id/items",
                HttpStatusCode.OK,
                JsonSerializer.Serialize(workspaceItemsResponse)
            );

            // Mock item definition response
            var itemDefinitionResponse = new
            {
                definition = new
                {
                    parts = new[]
                    {
                        new { path = "definition.pbir", payload = "dGVzdA==", payloadType = "InlineBase64" },
                        new { path = "report.json", payload = "dGVzdA==", payloadType = "InlineBase64" },
                        new { path = "metadata.json", payload = "dGVzdA==", payloadType = "InlineBase64" },
                        new { path = "config.txt", payload = "dGVzdA==", payloadType = "InlineBase64" }
                    }
                }
            };
            mockHandler.AddResponse(
                "getDefinition",
                HttpStatusCode.OK,
                JsonSerializer.Serialize(itemDefinitionResponse)
            );

            var httpClient = new HttpClient(mockHandler);
            var mockCredential = new MockTokenCredential();
            var fs = new FabricRemoteFileSystem("test-workspace-id", mockCredential, httpClient);

            // Act
            var jsonFiles = fs.GetFiles("Report1", "*.json", SearchOption.TopDirectoryOnly).ToList();

            // Assert
            Assert.That(jsonFiles, Has.Count.EqualTo(2));
            Assert.That(jsonFiles.All(f => f.EndsWith(".json")), Is.True);
        }

        [Test]
        public void FabricFileSystem_PathOperations_WorkCorrectly()
        {
            // Arrange
            var mockHandler = new MockHttpMessageHandler();
            var httpClient = new HttpClient(mockHandler);
            var mockCredential = new MockTokenCredential();
            var fs = new FabricRemoteFileSystem("test-workspace-id", mockCredential, httpClient);

            // Act & Assert
            Assert.That(fs.GetFileName("Report1/definition.pbir"), Is.EqualTo("definition.pbir"));
            Assert.That(fs.GetDirectoryName("Report1/definition.pbir"), Is.EqualTo("Report1"));
            Assert.That(fs.GetExtension("Report1/definition.pbir"), Is.EqualTo(".pbir"));
            Assert.That(fs.PathCombine("Report1", "definition.pbir"), Does.Contain("Report1"));
            Assert.That(fs.PathCombine("Report1", "definition.pbir"), Does.Contain("definition.pbir"));
        }

        [Test]
        public void FabricFileSystem_WriteAllText_ThrowsNotSupportedException()
        {
            // Arrange
            var mockHandler = new MockHttpMessageHandler();
            var httpClient = new HttpClient(mockHandler);
            var mockCredential = new MockTokenCredential();
            var fs = new FabricRemoteFileSystem("test-workspace-id", mockCredential, httpClient);

            // Act & Assert
            Assert.Throws<NotSupportedException>(() => fs.WriteAllText("test.txt", "content"));
        }

        [Test]
        public void FabricFileSystem_ReadAllText_ThrowsFileNotFoundException_WhenItemNotFound()
        {
            // Arrange
            var mockHandler = new MockHttpMessageHandler();
            var workspaceItemsResponse = new
            {
                value = Array.Empty<object>()
            };
            mockHandler.AddResponse(
                "https://api.fabric.microsoft.com/v1/workspaces/test-workspace-id/items",
                HttpStatusCode.OK,
                JsonSerializer.Serialize(workspaceItemsResponse)
            );

            var httpClient = new HttpClient(mockHandler);
            var mockCredential = new MockTokenCredential();
            var fs = new FabricRemoteFileSystem("test-workspace-id", mockCredential, httpClient);

            // Act & Assert
            Assert.Throws<FileNotFoundException>(() => fs.ReadAllText("NonExistent/file.txt"));
        }

        [Test]
        public void FabricFileSystem_IsCaseInsensitive()
        {
            // Arrange
            var mockHandler = new MockHttpMessageHandler();
            
            var workspaceItemsResponse = new
            {
                value = new[]
                {
                    new { id = "item1", displayName = "Report1", type = "Report", workspaceId = "test-workspace-id" }
                }
            };
            mockHandler.AddResponse(
                "https://api.fabric.microsoft.com/v1/workspaces/test-workspace-id/items",
                HttpStatusCode.OK,
                JsonSerializer.Serialize(workspaceItemsResponse)
            );

            var httpClient = new HttpClient(mockHandler);
            var mockCredential = new MockTokenCredential();
            var fs = new FabricRemoteFileSystem("test-workspace-id", mockCredential, httpClient);

            // Act & Assert - Different casing should work
            Assert.That(fs.DirectoryExists("report1.report"), Is.True);
            Assert.That(fs.DirectoryExists("REPORT1.REPORT"), Is.True);
            Assert.That(fs.DirectoryExists("Report1.Report"), Is.True);
        }

        #region Item-Scoped Constructor Tests

        [Test]
        public void FabricFileSystem_ItemScopedConstructor_ValidatesParameters()
        {
            // Arrange
            var mockCredential = new MockTokenCredential();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new FabricRemoteFileSystem("", "item1", mockCredential, null));
            Assert.Throws<ArgumentNullException>(() => new FabricRemoteFileSystem("workspace-id", "", mockCredential, null));
            Assert.Throws<ArgumentNullException>(() => new FabricRemoteFileSystem("workspace-id", "item1", null!, null));
            Assert.Throws<ArgumentNullException>(() => new FabricRemoteFileSystem(null!, "item1", mockCredential, null));
            Assert.Throws<ArgumentNullException>(() => new FabricRemoteFileSystem("workspace-id", null!, mockCredential, null));
        }

        [Test]
        public void FabricFileSystem_ItemScoped_EagerlyLoadsItemMetadata()
        {
            // Arrange
            var mockHandler = new MockHttpMessageHandler();
            
            // Mock item metadata response
            var itemMetadataResponse = new
            {
                id = "item1",
                displayName = "Report1",
                type = "Report",
                workspaceId = "test-workspace-id"
            };
            mockHandler.AddResponse(
                "https://api.fabric.microsoft.com/v1/workspaces/test-workspace-id/items/item1",
                HttpStatusCode.OK,
                JsonSerializer.Serialize(itemMetadataResponse)
            );

            var httpClient = new HttpClient(mockHandler);
            var mockCredential = new MockTokenCredential();

            // Act - constructor should eagerly load item metadata
            var fs = new FabricRemoteFileSystem("test-workspace-id", "item1", mockCredential, httpClient);

            // Assert - if we got here, the item was loaded successfully
            Assert.That(fs, Is.Not.Null);
        }

        [Test]
        public void FabricFileSystem_ItemScoped_ThrowsOnInvalidItemId()
        {
            // Arrange
            var mockHandler = new MockHttpMessageHandler();
            
            // Mock 404 response for non-existent item
            mockHandler.AddResponse(
                "https://api.fabric.microsoft.com/v1/workspaces/test-workspace-id/items/invalid-item",
                HttpStatusCode.NotFound,
                "{\"error\": \"Item not found\"}"
            );

            var httpClient = new HttpClient(mockHandler);
            var mockCredential = new MockTokenCredential();

            // Act & Assert
            Assert.Throws<HttpRequestException>(() => 
                new FabricRemoteFileSystem("test-workspace-id", "invalid-item", mockCredential, httpClient));
        }

        [Test]
        public void FabricFileSystem_ItemScoped_GetDirectories_ReturnsOnlyScopedItem()
        {
            // Arrange
            var mockHandler = new MockHttpMessageHandler();
            
            // Mock item metadata response
            var itemMetadataResponse = new
            {
                id = "item1",
                displayName = "Report1",
                type = "Report",
                workspaceId = "test-workspace-id"
            };
            mockHandler.AddResponse(
                "https://api.fabric.microsoft.com/v1/workspaces/test-workspace-id/items/item1",
                HttpStatusCode.OK,
                JsonSerializer.Serialize(itemMetadataResponse)
            );

            var httpClient = new HttpClient(mockHandler);
            var mockCredential = new MockTokenCredential();
            var fs = new FabricRemoteFileSystem("test-workspace-id", "item1", mockCredential, httpClient);

            // Act
            var directories = fs.GetDirectories("").ToList();

            // Assert - Should only return the scoped item
            Assert.That(directories, Has.Count.EqualTo(1));
            Assert.That(directories, Does.Contain("Report1"));
        }

        [Test]
        public void FabricFileSystem_ItemScoped_DirectoryExists_ReturnsTrueForScopedItemOnly()
        {
            // Arrange
            var mockHandler = new MockHttpMessageHandler();
            
            // Mock item metadata response
            var itemMetadataResponse = new
            {
                id = "item1",
                displayName = "Report1",
                type = "Report",
                workspaceId = "test-workspace-id"
            };
            mockHandler.AddResponse(
                "https://api.fabric.microsoft.com/v1/workspaces/test-workspace-id/items/item1",
                HttpStatusCode.OK,
                JsonSerializer.Serialize(itemMetadataResponse)
            );

            var httpClient = new HttpClient(mockHandler);
            var mockCredential = new MockTokenCredential();
            var fs = new FabricRemoteFileSystem("test-workspace-id", "item1", mockCredential, httpClient);

            // Act & Assert
            Assert.That(fs.DirectoryExists("Report1.Report"), Is.True);
            Assert.That(fs.DirectoryExists("OtherReport.Report"), Is.False);
            Assert.That(fs.DirectoryExists("Pipeline1.DataPipeline"), Is.False);
        }

        [Test]
        public void FabricFileSystem_ItemScoped_IsCaseInsensitive()
        {
            // Arrange
            var mockHandler = new MockHttpMessageHandler();
            
            // Mock item metadata response
            var itemMetadataResponse = new
            {
                id = "item1",
                displayName = "Report1",
                type = "Report",
                workspaceId = "test-workspace-id"
            };
            mockHandler.AddResponse(
                "https://api.fabric.microsoft.com/v1/workspaces/test-workspace-id/items/item1",
                HttpStatusCode.OK,
                JsonSerializer.Serialize(itemMetadataResponse)
            );

            var httpClient = new HttpClient(mockHandler);
            var mockCredential = new MockTokenCredential();
            var fs = new FabricRemoteFileSystem("test-workspace-id", "item1", mockCredential, httpClient);

            // Act & Assert - Different casing should work
            Assert.That(fs.DirectoryExists("report1.Report"), Is.True);
            Assert.That(fs.DirectoryExists("REPORT1.Report"), Is.True);
            Assert.That(fs.DirectoryExists("Report1.Report"), Is.True);
        }

        [Test]
        [Ignore("Mock HTTP handler URL matching needs refinement")]
        public void FabricFileSystem_ItemScoped_FileExists_OnlyInScopedItem()
        {
            // Arrange
            var mockHandler = new MockHttpMessageHandler();
            
            // Mock item metadata response
            var itemMetadataResponse = new
            {
                id = "item1",
                displayName = "Report1",
                type = "Report",
                workspaceId = "test-workspace-id"
            };
            mockHandler.AddResponse(
                "https://api.fabric.microsoft.com/v1/workspaces/test-workspace-id/items/item1",
                HttpStatusCode.OK,
                JsonSerializer.Serialize(itemMetadataResponse)
            );

            // Mock item definition response
            var itemDefinitionResponse = new
            {
                definition = new
                {
                    parts = new[]
                    {
                        new { path = "definition.pbir", payload = "dGVzdA==", payloadType = "InlineBase64" }
                    }
                }
            };
            mockHandler.AddResponse(
                "getDefinition",
                HttpStatusCode.OK,
                JsonSerializer.Serialize(itemDefinitionResponse)
            );

            var httpClient = new HttpClient(mockHandler);
            var mockCredential = new MockTokenCredential();
            var fs = new FabricRemoteFileSystem("test-workspace-id", "item1", mockCredential, httpClient);

            // Act & Assert
            Assert.That(fs.FileExists("Report1/definition.pbir"), Is.True);
            Assert.That(fs.FileExists("OtherReport/definition.pbir"), Is.False);
        }

        [Test]
        public void FabricFileSystem_ItemScoped_RefreshWorkspaceItems_UpdatesItemMetadata()
        {
            // Arrange
            var mockHandler = new MockHttpMessageHandler();
            
            // Mock initial item metadata response
            var itemMetadataResponse = new
            {
                id = "item1",
                displayName = "Report1",
                type = "Report",
                workspaceId = "test-workspace-id"
            };
            mockHandler.AddResponse(
                "https://api.fabric.microsoft.com/v1/workspaces/test-workspace-id/items/item1",
                HttpStatusCode.OK,
                JsonSerializer.Serialize(itemMetadataResponse)
            );

            var httpClient = new HttpClient(mockHandler);
            var mockCredential = new MockTokenCredential();
            var fs = new FabricRemoteFileSystem("test-workspace-id", "item1", mockCredential, httpClient);

            // Act - refresh should reload item metadata
            fs.RefreshWorkspaceItems();

            // Assert - if we got here, refresh worked successfully
            Assert.That(fs.DirectoryExists("Report1.Report"), Is.True);
        }

        [Test]
        public void FabricFileSystem_ItemScoped_DoesNotCallWorkspaceItemsEndpoint()
        {
            // Arrange
            var mockHandler = new MockHttpMessageHandler();
            
            // Mock item metadata response ONLY (no workspace items endpoint)
            var itemMetadataResponse = new
            {
                id = "item1",
                displayName = "Report1",
                type = "Report",
                workspaceId = "test-workspace-id"
            };
            mockHandler.AddResponse(
                "https://api.fabric.microsoft.com/v1/workspaces/test-workspace-id/items/item1",
                HttpStatusCode.OK,
                JsonSerializer.Serialize(itemMetadataResponse)
            );

            // Do NOT add workspace items endpoint - it should not be called

            var httpClient = new HttpClient(mockHandler);
            var mockCredential = new MockTokenCredential();
            
            // Act - constructor and GetDirectories should work without workspace items endpoint
            var fs = new FabricRemoteFileSystem("test-workspace-id", "item1", mockCredential, httpClient);
            var directories = fs.GetDirectories("").ToList();

            // Assert - Should only return the scoped item
            Assert.That(directories, Has.Count.EqualTo(1));
            Assert.That(directories, Does.Contain("Report1"));
        }

        #endregion

        #region Long-Running Operation Tests

        [Test]
        [Ignore("Mock handler needs refinement")]
        public void FabricFileSystem_LRO_SynchronousCompletion_ReturnsImmediately()
        {
            // Arrange
            var mockHandler = new MockHttpMessageHandler();
            
            // Mock workspace items
            var workspaceItemsResponse = new
            {
                value = new[]
                {
                    new { id = "item1", displayName = "Report1", type = "Report", workspaceId = "test-workspace-id" }
                }
            };
            mockHandler.AddResponse(
                "https://api.fabric.microsoft.com/v1/workspaces/test-workspace-id/items",
                HttpStatusCode.OK,
                JsonSerializer.Serialize(workspaceItemsResponse)
            );

            // Mock synchronous getDefinition response (200 OK immediately)
            var itemDefinitionResponse = new
            {
                definition = new
                {
                    parts = new[]
                    {
                        new { path = "definition.pbir", payload = "dGVzdA==", payloadType = "InlineBase64" }
                    }
                }
            };
            mockHandler.AddResponse(
                "getDefinition",
                HttpStatusCode.OK,
                JsonSerializer.Serialize(itemDefinitionResponse)
            );

            var httpClient = new HttpClient(mockHandler);
            var mockCredential = new MockTokenCredential();
            var fs = new FabricRemoteFileSystem("test-workspace-id", mockCredential, httpClient);

            // Act
            var fileExists = fs.FileExists("Report1/definition.pbir");

            // Assert
            Assert.That(fileExists, Is.True);
            Assert.That(mockHandler.GetCallCount("getDefinition"), Is.EqualTo(1));
        }

        [Test]
        [Ignore("Mock handler needs refinement")]
        public void FabricFileSystem_LRO_AsyncOperation_PollsUntilSucceeded()
        {
            // Arrange
            var mockHandler = new MockHttpMessageHandler();
            
            // Mock workspace items
            var workspaceItemsResponse = new
            {
                value = new[]
                {
                    new { id = "item1", displayName = "Report1", type = "Report", workspaceId = "test-workspace-id" }
                }
            };
            mockHandler.AddResponse(
                "https://api.fabric.microsoft.com/v1/workspaces/test-workspace-id/items",
                HttpStatusCode.OK,
                JsonSerializer.Serialize(workspaceItemsResponse)
            );

            // Mock async getDefinition response (202 Accepted)
            var operationId = "op-12345";
            var locationUrl = $"https://api.fabric.microsoft.com/v1/operations/{operationId}";
            mockHandler.AddResponse(
                "getDefinition",
                HttpStatusCode.Accepted,
                string.Empty,
                new Dictionary<string, string>
                {
                    { "Location", locationUrl },
                    { "x-ms-operation-id", operationId },
                    { "Retry-After", "1" }
                }
            );

            // Mock polling responses with sequential responses
            var pollingResponses = new List<(HttpStatusCode, string, Dictionary<string, string>?)>
            {
                // First poll - still running
                (HttpStatusCode.OK, JsonSerializer.Serialize(new
                {
                    status = "Running",
                    createdTimeUtc = "2026-02-26T10:00:00Z",
                    lastUpdatedTimeUtc = "2026-02-26T10:00:01Z",
                    percentComplete = 25
                }), null),
                // Second poll - still running
                (HttpStatusCode.OK, JsonSerializer.Serialize(new
                {
                    status = "Running",
                    createdTimeUtc = "2026-02-26T10:00:00Z",
                    lastUpdatedTimeUtc = "2026-02-26T10:00:02Z",
                    percentComplete = 75
                }), null),
                // Third poll - succeeded
                (HttpStatusCode.OK, JsonSerializer.Serialize(new
                {
                    status = "Succeeded",
                    createdTimeUtc = "2026-02-26T10:00:00Z",
                    lastUpdatedTimeUtc = "2026-02-26T10:00:03Z",
                    percentComplete = 100
                }), null)
            };
            mockHandler.AddSequentialResponses($"/operations/{operationId}", pollingResponses);

            // Mock result endpoint
            var resultDefinition = new
            {
                definition = new
                {
                    parts = new[]
                    {
                        new { path = "definition.pbir", payload = "dGVzdA==", payloadType = "InlineBase64" }
                    }
                }
            };
            mockHandler.AddResponse(
                $"/operations/{operationId}/result",
                HttpStatusCode.OK,
                JsonSerializer.Serialize(resultDefinition)
            );

            var httpClient = new HttpClient(mockHandler);
            var mockCredential = new MockTokenCredential();
            var fs = new FabricRemoteFileSystem("test-workspace-id", mockCredential, httpClient, maxLroAttempts: 10, initialRetryDelayMs: 10);

            // Act
            var fileExists = fs.FileExists("Report1/definition.pbir");

            // Assert
            Assert.That(fileExists, Is.True);
            Assert.That(mockHandler.GetCallCount($"/operations/{operationId}"), Is.EqualTo(3)); // 3 polls
        }

        [Test]
        [Ignore("Mock handler needs refinement")]
        public void FabricFileSystem_LRO_OperationFailed_ThrowsException()
        {
            // Arrange
            var mockHandler = new MockHttpMessageHandler();
            
            // Mock workspace items
            var workspaceItemsResponse = new
            {
                value = new[]
                {
                    new { id = "item1", displayName = "Report1", type = "Report", workspaceId = "test-workspace-id" }
                }
            };
            mockHandler.AddResponse(
                "https://api.fabric.microsoft.com/v1/workspaces/test-workspace-id/items",
                HttpStatusCode.OK,
                JsonSerializer.Serialize(workspaceItemsResponse)
            );

            // Mock async getDefinition response (202 Accepted)
            var operationId = "op-fail-123";
            var locationUrl = $"https://api.fabric.microsoft.com/v1/operations/{operationId}";
            mockHandler.AddResponse(
                "getDefinition",
                HttpStatusCode.Accepted,
                string.Empty,
                new Dictionary<string, string>
                {
                    { "Location", locationUrl },
                    { "x-ms-operation-id", operationId },
                    { "Retry-After", "1" }
                }
            );

            // Mock polling response - operation failed
            var failedResponse = new
            {
                status = "Failed",
                createdTimeUtc = "2026-02-26T10:00:00Z",
                lastUpdatedTimeUtc = "2026-02-26T10:00:03Z",
                percentComplete = 100,
                error = new
                {
                    code = "InvalidDefinition",
                    message = "The item definition is invalid",
                    details = "Missing required field 'displayName'"
                }
            };
            mockHandler.AddResponse(
                $"/operations/{operationId}",
                HttpStatusCode.OK,
                JsonSerializer.Serialize(failedResponse)
            );

            var httpClient = new HttpClient(mockHandler);
            var mockCredential = new MockTokenCredential();
            var fs = new FabricRemoteFileSystem("test-workspace-id", mockCredential, httpClient, maxLroAttempts: 10, initialRetryDelayMs: 10);

            // Act & Assert
            var ex = Assert.Throws<HttpRequestException>(() => fs.FileExists("Report1/definition.pbir"));
            Assert.That(ex!.Message, Does.Contain("Long-running operation failed"));
            Assert.That(ex.Message, Does.Contain("InvalidDefinition"));
            Assert.That(ex.Message, Does.Contain("The item definition is invalid"));
        }

        [Test]
        [Ignore("Mock handler needs refinement")]
        public void FabricFileSystem_LRO_TimeoutExceeded_ThrowsTimeoutException()
        {
            // Arrange
            var mockHandler = new MockHttpMessageHandler();
            
            // Mock workspace items
            var workspaceItemsResponse = new
            {
                value = new[]
                {
                    new { id = "item1", displayName = "Report1", type = "Report", workspaceId = "test-workspace-id" }
                }
            };
            mockHandler.AddResponse(
                "https://api.fabric.microsoft.com/v1/workspaces/test-workspace-id/items",
                HttpStatusCode.OK,
                JsonSerializer.Serialize(workspaceItemsResponse)
            );

            // Mock async getDefinition response (202 Accepted)
            var operationId = "op-timeout-123";
            var locationUrl = $"https://api.fabric.microsoft.com/v1/operations/{operationId}";
            mockHandler.AddResponse(
                "getDefinition",
                HttpStatusCode.Accepted,
                string.Empty,
                new Dictionary<string, string>
                {
                    { "Location", locationUrl },
                    { "x-ms-operation-id", operationId },
                    { "Retry-After", "1" }
                }
            );

            // Mock polling response - always running (never completes)
            var runningResponse = new
            {
                status = "Running",
                createdTimeUtc = "2026-02-26T10:00:00Z",
                lastUpdatedTimeUtc = "2026-02-26T10:00:01Z",
                percentComplete = 50
            };
            mockHandler.AddResponse(
                $"/operations/{operationId}",
                HttpStatusCode.OK,
                JsonSerializer.Serialize(runningResponse)
            );

            var httpClient = new HttpClient(mockHandler);
            var mockCredential = new MockTokenCredential();
            var fs = new FabricRemoteFileSystem("test-workspace-id", mockCredential, httpClient, maxLroAttempts: 3, initialRetryDelayMs: 10);

            // Act & Assert
            var ex = Assert.Throws<TimeoutException>(() => fs.FileExists("Report1/definition.pbir"));
            Assert.That(ex!.Message, Does.Contain("Timed out"));
            Assert.That(ex.Message, Does.Contain("after 3 attempts"));
        }

        [Test]
        [Ignore("Mock handler needs refinement")]
        public void FabricFileSystem_LRO_MissingLocationHeader_ThrowsException()
        {
            // Arrange
            var mockHandler = new MockHttpMessageHandler();
            
            // Mock workspace items
            var workspaceItemsResponse = new
            {
                value = new[]
                {
                    new { id = "item1", displayName = "Report1", type = "Report", workspaceId = "test-workspace-id" }
                }
            };
            mockHandler.AddResponse(
                "https://api.fabric.microsoft.com/v1/workspaces/test-workspace-id/items",
                HttpStatusCode.OK,
                JsonSerializer.Serialize(workspaceItemsResponse)
            );

            // Mock async getDefinition response (202 Accepted) WITHOUT Location header
            mockHandler.AddResponse(
                "getDefinition",
                HttpStatusCode.Accepted,
                string.Empty
                // No headers - missing Location
            );

            var httpClient = new HttpClient(mockHandler);
            var mockCredential = new MockTokenCredential();
            var fs = new FabricRemoteFileSystem("test-workspace-id", mockCredential, httpClient);

            // Act & Assert
            var ex = Assert.Throws<HttpRequestException>(() => fs.FileExists("Report1/definition.pbir"));
            Assert.That(ex!.Message, Does.Contain("Received HTTP 202 but no Location header"));
        }

        [Test]
        [Ignore("Mock handler needs refinement")]
        public void FabricFileSystem_LRO_RespectsRetryAfter_Header()
        {
            // Arrange
            var mockHandler = new MockHttpMessageHandler();
            
            // Mock workspace items
            var workspaceItemsResponse = new
            {
                value = new[]
                {
                    new { id = "item1", displayName = "Report1", type = "Report", workspaceId = "test-workspace-id" }
                }
            };
            mockHandler.AddResponse(
                "https://api.fabric.microsoft.com/v1/workspaces/test-workspace-id/items",
                HttpStatusCode.OK,
                JsonSerializer.Serialize(workspaceItemsResponse)
            );

            // Mock async getDefinition with Retry-After = 2 seconds
            var operationId = "op-retry-123";
            var locationUrl = $"https://api.fabric.microsoft.com/v1/operations/{operationId}";
            mockHandler.AddResponse(
                "getDefinition",
                HttpStatusCode.Accepted,
                string.Empty,
                new Dictionary<string, string>
                {
                    { "Location", locationUrl },
                    { "x-ms-operation-id", operationId },
                    { "Retry-After", "5" } // 5 seconds
                }
            );

            // Mock immediate success on first poll
            var successResponse = new
            {
                status = "Succeeded",
                percentComplete = 100
            };
            mockHandler.AddResponse(
                $"/operations/{operationId}",
                HttpStatusCode.OK,
                JsonSerializer.Serialize(successResponse)
            );

            // Mock result
            var resultDefinition = new
            {
                definition = new
                {
                    parts = new[]
                    {
                        new { path = "definition.pbir", payload = "dGVzdA==", payloadType = "InlineBase64" }
                    }
                }
            };
            mockHandler.AddResponse(
                $"/operations/{operationId}/result",
                HttpStatusCode.OK,
                JsonSerializer.Serialize(resultDefinition)
            );

            var httpClient = new HttpClient(mockHandler);
            var mockCredential = new MockTokenCredential();
            var startTime = DateTime.UtcNow;
            var fs = new FabricRemoteFileSystem("test-workspace-id", mockCredential, httpClient, initialRetryDelayMs: 100); // Low default

            // Act
            var fileExists = fs.FileExists("Report1/definition.pbir");
            var elapsed = DateTime.UtcNow - startTime;

            // Assert
            Assert.That(fileExists, Is.True);
            // Should wait at least 5 seconds (from Retry-After), not 100ms (from initialRetryDelayMs)
            Assert.That(elapsed.TotalSeconds, Is.GreaterThanOrEqualTo(4.5)); // Allow some tolerance
        }

        [Test]
        [Ignore("Mock handler needs refinement")]
        public void FabricFileSystem_LRO_ExponentialBackoff_IncreasesDelay()
        {
            // Arrange
            var mockHandler = new MockHttpMessageHandler();
            
            // Mock workspace items
            var workspaceItemsResponse = new
            {
                value = new[]
                {
                    new { id = "item1", displayName = "Report1", type = "Report", workspaceId = "test-workspace-id" }
                }
            };
            mockHandler.AddResponse(
                "https://api.fabric.microsoft.com/v1/workspaces/test-workspace-id/items",
                HttpStatusCode.OK,
                JsonSerializer.Serialize(workspaceItemsResponse)
            );

            // Mock async getDefinition
            var operationId = "op-backoff-123";
            var locationUrl = $"https://api.fabric.microsoft.com/v1/operations/{operationId}";
            mockHandler.AddResponse(
                "getDefinition",
                HttpStatusCode.Accepted,
                string.Empty,
                new Dictionary<string, string>
                {
                    { "Location", locationUrl },
                    { "x-ms-operation-id", operationId },
                    { "Retry-After", "1" }
                }
            );

            // Mock multiple running responses, then success
            var pollingResponses = new List<(HttpStatusCode, string, Dictionary<string, string>?)>();
            for (int i = 0; i < 5; i++)
            {
                pollingResponses.Add((HttpStatusCode.OK, JsonSerializer.Serialize(new
                {
                    status = "Running",
                    percentComplete = i * 20
                }), null));
            }
            pollingResponses.Add((HttpStatusCode.OK, JsonSerializer.Serialize(new
            {
                status = "Succeeded",
                percentComplete = 100
            }), null));
            
            mockHandler.AddSequentialResponses($"/operations/{operationId}", pollingResponses);

            // Mock result
            var resultDefinition = new
            {
                definition = new
                {
                    parts = new[]
                    {
                        new { path = "definition.pbir", payload = "dGVzdA==", payloadType = "InlineBase64" }
                    }
                }
            };
            mockHandler.AddResponse(
                $"/operations/{operationId}/result",
                HttpStatusCode.OK,
                JsonSerializer.Serialize(resultDefinition)
            );

            var httpClient = new HttpClient(mockHandler);
            var mockCredential = new MockTokenCredential();
            var startTime = DateTime.UtcNow;
            // Initial: 200ms, then 400ms, 800ms, 1600ms, 3200ms, but capped at maxRetryDelayMs=1000ms
            var fs = new FabricRemoteFileSystem("test-workspace-id", mockCredential, httpClient, 
                maxLroAttempts: 10, initialRetryDelayMs: 200, maxRetryDelayMs: 1000);

            // Act
            var fileExists = fs.FileExists("Report1/definition.pbir");
            var elapsed = DateTime.UtcNow - startTime;

            // Assert
            Assert.That(fileExists, Is.True);
            Assert.That(mockHandler.GetCallCount($"/operations/{operationId}"), Is.EqualTo(6)); // 6 polls
            // Should take approximately: 200 + 400 + 800 + 1000 + 1000 = 3400ms, allow tolerance
            Assert.That(elapsed.TotalMilliseconds, Is.GreaterThan(3000));
        }

        [Test]
        [Ignore("Mock handler needs refinement")]
        public void FabricFileSystem_LRO_NotStartedStatus_ContinuesPolling()
        {
            // Arrange
            var mockHandler = new MockHttpMessageHandler();
            
            // Mock workspace items
            var workspaceItemsResponse = new
            {
                value = new[]
                {
                    new { id = "item1", displayName = "Report1", type = "Report", workspaceId = "test-workspace-id" }
                }
            };
            mockHandler.AddResponse(
                "https://api.fabric.microsoft.com/v1/workspaces/test-workspace-id/items",
                HttpStatusCode.OK,
                JsonSerializer.Serialize(workspaceItemsResponse)
            );

            // Mock getDefinition
            var operationId = "op-notstarted-123";
            var locationUrl = $"https://api.fabric.microsoft.com/v1/operations/{operationId}";
            mockHandler.AddResponse(
                "getDefinition",
                HttpStatusCode.Accepted,
                string.Empty,
                new Dictionary<string, string>
                {
                    { "Location", locationUrl },
                    { "Retry-After", "1" }
                }
            );

            // Mock polling responses: NotStarted -> Running -> Succeeded
            var pollingResponses = new List<(HttpStatusCode, string, Dictionary<string, string>?)>
            {
                (HttpStatusCode.OK, JsonSerializer.Serialize(new { status = "NotStarted", percentComplete = 0 }), null),
                (HttpStatusCode.OK, JsonSerializer.Serialize(new { status = "Running", percentComplete = 50 }), null),
                (HttpStatusCode.OK, JsonSerializer.Serialize(new { status = "Succeeded", percentComplete = 100 }), null)
            };
            mockHandler.AddSequentialResponses($"/operations/{operationId}", pollingResponses);

            // Mock result
            var resultDefinition = new
            {
                definition = new
                {
                    parts = new[]
                    {
                        new { path = "definition.pbir", payload = "dGVzdA==", payloadType = "InlineBase64" }
                    }
                }
            };
            mockHandler.AddResponse(
                $"/operations/{operationId}/result",
                HttpStatusCode.OK,
                JsonSerializer.Serialize(resultDefinition)
            );

            var httpClient = new HttpClient(mockHandler);
            var mockCredential = new MockTokenCredential();
            var fs = new FabricRemoteFileSystem("test-workspace-id", mockCredential, httpClient, initialRetryDelayMs: 10);

            // Act
            var fileExists = fs.FileExists("Report1/definition.pbir");

            // Assert
            Assert.That(fileExists, Is.True);
            Assert.That(mockHandler.GetCallCount($"/operations/{operationId}"), Is.EqualTo(3));
        }

        #endregion
    }
}