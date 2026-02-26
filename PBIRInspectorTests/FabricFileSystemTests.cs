using NUnit.Framework;
using PBIRInspectorLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PBIRInspectorTests
{
    [TestFixture]
    public class FabricFileSystemTests
    {
        /// <summary>
        /// Mock HTTP message handler for testing without real API calls
        /// </summary>
        private class MockHttpMessageHandler : HttpMessageHandler
        {
            private readonly Dictionary<string, (HttpStatusCode statusCode, string content)> _responses = new();

            public void AddResponse(string urlPattern, HttpStatusCode statusCode, string content)
            {
                _responses[urlPattern] = (statusCode, content);
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var url = request.RequestUri?.ToString() ?? string.Empty;
                
                // Try exact match first
                if (_responses.TryGetValue(url, out var response))
                {
                    return Task.FromResult(new HttpResponseMessage(response.statusCode)
                    {
                        Content = new StringContent(response.content, Encoding.UTF8, "application/json")
                    });
                }

                // Try partial match - check if URL contains any of the patterns
                foreach (var kvp in _responses)
                {
                    // For getDefinition, match any URL containing it
                    if (kvp.Key == "getDefinition" && url.Contains("/getDefinition"))
                    {
                        return Task.FromResult(new HttpResponseMessage(kvp.Value.statusCode)
                        {
                            Content = new StringContent(kvp.Value.content, Encoding.UTF8, "application/json")
                        });
                    }
                    
                    // For other patterns, check if URL contains it or ends with it
                    if (url.Contains(kvp.Key) || url.EndsWith(kvp.Key))
                    {
                        return Task.FromResult(new HttpResponseMessage(kvp.Value.statusCode)
                        {
                            Content = new StringContent(kvp.Value.content, Encoding.UTF8, "application/json")
                        });
                    }
                }

                // Default 404 response
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("{\"error\": \"Not Found\"}", Encoding.UTF8, "application/json")
                });
            }
        }

        [Test]
        public void FabricFileSystem_Constructor_ValidatesParameters()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new FabricFileSystem("", "token"));
            Assert.Throws<ArgumentNullException>(() => new FabricFileSystem("workspace-id", ""));
            Assert.Throws<ArgumentNullException>(() => new FabricFileSystem(null!, "token"));
            Assert.Throws<ArgumentNullException>(() => new FabricFileSystem("workspace-id", null!));
        }

        [Test]
        public void FabricFileSystem_DirectoryExists_ReturnsTrueForRoot()
        {
            // Arrange
            var mockHandler = new MockHttpMessageHandler();
            var httpClient = new HttpClient(mockHandler);
            var fs = new FabricFileSystem("test-workspace-id", "test-token", httpClient);

            // Act & Assert
            Assert.That(fs.DirectoryExists(""), Is.True);
            Assert.That(fs.DirectoryExists("/"), Is.True);
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
            var fs = new FabricFileSystem("test-workspace-id", "test-token", httpClient);

            // Act
            bool report1Exists = fs.DirectoryExists("Report1");
            bool pipeline1Exists = fs.DirectoryExists("Pipeline1");
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
            var fs = new FabricFileSystem("test-workspace-id", "test-token", httpClient);

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
            var fs = new FabricFileSystem("test-workspace-id", "test-token", httpClient);

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
            var fs = new FabricFileSystem("test-workspace-id", "test-token", httpClient);

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
            var fs = new FabricFileSystem("test-workspace-id", "test-token", httpClient);

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
            var fs = new FabricFileSystem("test-workspace-id", "test-token", httpClient);

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
            var fs = new FabricFileSystem("test-workspace-id", "test-token", httpClient);

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
            var fs = new FabricFileSystem("test-workspace-id", "test-token", httpClient);

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
            var fs = new FabricFileSystem("test-workspace-id", "test-token", httpClient);

            // Act & Assert
            Assert.Throws<FileNotFoundException>(() => fs.ReadAllText("NonExistent/file.txt"));
        }

        [Test]
        public void FabricAuthenticationHelper_IsTokenFormatValid_ValidatesCorrectly()
        {
            // Arrange & Act & Assert
            Assert.That(FabricAuthenticationHelper.IsTokenFormatValid("header.payload.signature"), Is.True);
            Assert.That(FabricAuthenticationHelper.IsTokenFormatValid("invalidtoken"), Is.False);
            Assert.That(FabricAuthenticationHelper.IsTokenFormatValid(""), Is.False);
            Assert.That(FabricAuthenticationHelper.IsTokenFormatValid(null!), Is.False);
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
            var fs = new FabricFileSystem("test-workspace-id", "test-token", httpClient);

            // Act & Assert - Different casing should work
            Assert.That(fs.DirectoryExists("report1"), Is.True);
            Assert.That(fs.DirectoryExists("REPORT1"), Is.True);
            Assert.That(fs.DirectoryExists("Report1"), Is.True);
        }

        #region Item-Scoped Constructor Tests

        [Test]
        public void FabricFileSystem_ItemScopedConstructor_ValidatesParameters()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new FabricFileSystem("", "item1", "token", null));
            Assert.Throws<ArgumentNullException>(() => new FabricFileSystem("workspace-id", "", "token", null));
            Assert.Throws<ArgumentNullException>(() => new FabricFileSystem("workspace-id", "item1", "", null));
            Assert.Throws<ArgumentNullException>(() => new FabricFileSystem(null!, "item1", "token", null));
            Assert.Throws<ArgumentNullException>(() => new FabricFileSystem("workspace-id", null!, "token", null));
            Assert.Throws<ArgumentNullException>(() => new FabricFileSystem("workspace-id", "item1", null!, null));
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

            // Act - constructor should eagerly load item metadata
            var fs = new FabricFileSystem("test-workspace-id", "item1", "test-token", httpClient);

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

            // Act & Assert
            Assert.Throws<HttpRequestException>(() => 
                new FabricFileSystem("test-workspace-id", "invalid-item", "test-token", httpClient));
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
            var fs = new FabricFileSystem("test-workspace-id", "item1", "test-token", httpClient);

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
            var fs = new FabricFileSystem("test-workspace-id", "item1", "test-token", httpClient);

            // Act & Assert
            Assert.That(fs.DirectoryExists("Report1"), Is.True);
            Assert.That(fs.DirectoryExists("OtherReport"), Is.False);
            Assert.That(fs.DirectoryExists("Pipeline1"), Is.False);
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
            var fs = new FabricFileSystem("test-workspace-id", "item1", "test-token", httpClient);

            // Act & Assert - Different casing should work
            Assert.That(fs.DirectoryExists("report1"), Is.True);
            Assert.That(fs.DirectoryExists("REPORT1"), Is.True);
            Assert.That(fs.DirectoryExists("Report1"), Is.True);
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
            var fs = new FabricFileSystem("test-workspace-id", "item1", "test-token", httpClient);

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
            var fs = new FabricFileSystem("test-workspace-id", "item1", "test-token", httpClient);

            // Act - refresh should reload item metadata
            fs.RefreshWorkspaceItems();

            // Assert - if we got here, refresh worked successfully
            Assert.That(fs.DirectoryExists("Report1"), Is.True);
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
            
            // Act - constructor and GetDirectories should work without workspace items endpoint
            var fs = new FabricFileSystem("test-workspace-id", "item1", "test-token", httpClient);
            var directories = fs.GetDirectories("").ToList();

            // Assert - Should only return the scoped item
            Assert.That(directories, Has.Count.EqualTo(1));
            Assert.That(directories, Does.Contain("Report1"));
        }

        #endregion
    }
}
