using PBIRInspectorLibrary;
using PBIRInspectorLibrary.Exceptions;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PBIRInspectorClientLibrary.Utils
{
    public class AppUtils
    {
        public static void OpenUrl(string url)
        {
            try
            {
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
