using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FabInspector.Core
{
    public static class Utils
    {
        /// <summary>
        /// Creates a normalized column-separated path string that can be used for regex matching across different OS.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }
            const string windowsDriveSep = ":";
            const string empty = "";

            const char windowsSep = '\\';
            const char linuxSep = '/';
            const char normalizedSep = ':';
            var normalizedPath = path.Replace(windowsDriveSep, empty)
                                       .Replace(windowsSep, normalizedSep)
                                        .Replace(linuxSep, normalizedSep);
            return normalizedPath;
        }
    }
}
