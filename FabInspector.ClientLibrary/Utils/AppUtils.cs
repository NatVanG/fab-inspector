using FabInspector.Core;
using FabInspector.Core.Exceptions;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FabInspector.ClientLibrary.Utils
{
    public class AppUtils
    {
        public static void OpenUrl(string url)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url))
                {
                    throw new PBIRInspectorException("Path or URL is null or empty.");
                }

                var isWebUrl = Uri.TryCreate(url, UriKind.Absolute, out var uri)
                    && (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                        || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));
                var isLocalPath = File.Exists(url) || Directory.Exists(url);

                if (!isWebUrl && !isLocalPath)
                {
                    throw new PBIRInspectorException($"Path or URL does not exist or is unsupported: {url}");
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    ProcessStartInfo ps = new()
                    {
                        FileName = url,
                        UseShellExecute = true
                    };
                    Process.Start(ps);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
                else
                {
                    throw new PBIRInspectorException($"Unsupported OS platform for opening: {url}");
                }
            }
            catch (PBIRInspectorException)
            {
                throw;
            }
            catch
            {
                throw new PBIRInspectorException(string.Format("Could not launch browser or file explorer for location \"{0}\".", url));
            }
        }

        public static string About()
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            var about = string.Format("Fab Inspector v{0}", version);
            return about;
        }

        public static string GetTempRootFolderPath()
        {
            return Path.Combine(Path.GetTempPath(), Constants.FabInspectorTemp);
        }
    }
}
