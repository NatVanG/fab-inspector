using Azure;
using Azure.Core;
using Azure.Storage.Files.DataLake;

namespace PBIRInspectorClientLibrary.Utils
{
    internal static class OneLakeOutputUploader
    {
        private const string RequiredUrlPrefix = "https://onelake.dfs.fabric.microsoft.com";

        public static bool IsOneLakeDfsUrl(string? path)
        {
            return !string.IsNullOrWhiteSpace(path)
                && path.StartsWith(RequiredUrlPrefix, StringComparison.OrdinalIgnoreCase);
        }

        public static string CombineUrl(string baseUrl, string relativePath)
        {
            if (!IsOneLakeDfsUrl(baseUrl))
            {
                throw new InvalidOperationException(
                    $"Output path must be a full OneLake DFS URL starting with '{RequiredUrlPrefix}'.");
            }

            var encodedRelative = EncodePathSegments(relativePath.TrimStart('/', '\\'));
            return $"{baseUrl.TrimEnd('/')}/{encodedRelative}";
        }

        public static async Task<bool> FileExistsAsync(
            string oneLakeUrl,
            TokenCredential credential,
            CancellationToken cancellationToken = default)
        {
            var (fileSystemName, oneLakePath) = ParseAbsoluteUrl(oneLakeUrl);
            var fileClient = CreateServiceClient(credential)
                .GetFileSystemClient(fileSystemName)
                .GetFileClient(oneLakePath);

            Response<bool> exists = await fileClient.ExistsAsync(cancellationToken: cancellationToken);
            return exists.Value;
        }

        public static async Task UploadFileAsync(
            string localFilePath,
            string oneLakeUrl,
            bool overwrite,
            TokenCredential credential,
            CancellationToken cancellationToken = default)
        {
            if (!File.Exists(localFilePath))
            {
                throw new FileNotFoundException("Local output artifact was not found.", localFilePath);
            }

            await using var source = File.OpenRead(localFilePath);
            await UploadStreamAsync(source, oneLakeUrl, overwrite, credential, cancellationToken);
        }

        public static async Task UploadStreamAsync(
            Stream source,
            string oneLakeUrl,
            bool overwrite,
            TokenCredential credential,
            CancellationToken cancellationToken = default)
        {
            var (fileSystemName, oneLakePath) = ParseAbsoluteUrl(oneLakeUrl);
            var fileSystemClient = CreateServiceClient(credential).GetFileSystemClient(fileSystemName);
            var fileClient = fileSystemClient.GetFileClient(oneLakePath);

            var parentPath = Path.GetDirectoryName(oneLakePath.Replace('\\', '/'))?.Replace('\\', '/');
            if (!string.IsNullOrWhiteSpace(parentPath))
            {
                var parentDirectory = fileSystemClient.GetDirectoryClient(parentPath);
                await parentDirectory.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            }

            if (source.CanSeek)
            {
                source.Position = 0;
            }

            await fileClient.UploadAsync(source, overwrite: overwrite, cancellationToken: cancellationToken);
        }

        private static DataLakeServiceClient CreateServiceClient(TokenCredential credential)
        {
            return new DataLakeServiceClient(new Uri(RequiredUrlPrefix), credential);
        }

        private static (string FileSystem, string Path) ParseAbsoluteUrl(string url)
        {
            if (!IsOneLakeDfsUrl(url))
            {
                throw new InvalidOperationException(
                    $"Output path must be a full OneLake DFS URL starting with '{RequiredUrlPrefix}'.");
            }

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

        private static string EncodePathSegments(string path)
        {
            return string.Join('/', path.Replace('\\', '/').Split('/').Select(Uri.EscapeDataString));
        }
    }
}
