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
                if (string.IsNullOrEmpty(url) || !File.Exists(url))
                {
                    throw new PBIRInspectorException($"Path is not a local file path: {url}");
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
