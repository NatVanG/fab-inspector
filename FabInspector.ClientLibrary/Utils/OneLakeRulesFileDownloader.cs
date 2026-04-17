using Azure;
using Azure.Core;
using Azure.Storage.Files.DataLake;

namespace FabInspector.ClientLibrary.Utils
{
    internal static class OneLakeRulesFileDownloader
    {
        private const string RequiredUrlPrefix = "https://onelake.dfs.fabric.microsoft.com";

        public static bool IsOneLakeDfsUrl(string? path)
        {
            return !string.IsNullOrWhiteSpace(path)
                && path.StartsWith(RequiredUrlPrefix, StringComparison.OrdinalIgnoreCase);
        }

        public static async Task<MemoryStream> DownloadFileToMemoryStreamAsync(
            string oneLakeUrl,
            TokenCredential credential,
            Action<string>? onProgress = null,
            CancellationToken cancellationToken = default)
        {
            if (!IsOneLakeDfsUrl(oneLakeUrl))
            {
                throw new InvalidOperationException(
                    $"Rules path must be a full OneLake DFS URL starting with '{RequiredUrlPrefix}'.");
            }

            var (fileSystemName, oneLakePath) = ParseAbsoluteUrl(oneLakeUrl);
            var serviceClient = new DataLakeServiceClient(new Uri(RequiredUrlPrefix), credential);
            var fileSystemClient = serviceClient.GetFileSystemClient(fileSystemName);
            var fileClient = fileSystemClient.GetFileClient(oneLakePath);

            onProgress?.Invoke($"Checking OneLake rules file at \"{oneLakeUrl}\".");
            Response<bool> exists = await fileClient.ExistsAsync(cancellationToken: cancellationToken);
            if (!exists.Value)
            {
                throw new FileNotFoundException("Remote OneLake rules file was not found.", oneLakePath);
            }

            onProgress?.Invoke($"Downloading rules file from OneLake at \"{oneLakeUrl}\".");
            var memoryStream = new MemoryStream();
            await using var remote = await fileClient.OpenReadAsync(cancellationToken: cancellationToken);
            await remote.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;
            onProgress?.Invoke("Rules file downloaded from OneLake.");
            return memoryStream;
        }

        private static (string FileSystem, string Path) ParseAbsoluteUrl(string url)
        {
            var afterPrefix = url[RequiredUrlPrefix.Length..].TrimStart('/');
            var decoded = Uri.UnescapeDataString(afterPrefix);
            var slashIndex = decoded.IndexOf('/');

            if (slashIndex <= 0 || slashIndex == decoded.Length - 1)
            {
                throw new InvalidOperationException(
                    "OneLake URL must include both file system and file path segments.");
            }

            return (decoded[..slashIndex], decoded[(slashIndex + 1)..]);
        }
    }
}
