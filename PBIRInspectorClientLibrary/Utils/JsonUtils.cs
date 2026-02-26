using PBIRInspectorLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PBIRInspectorClientLibrary.Utils
{
    public class JsonUtils
    {
        public static T? DeserialiseFromPath<T>(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException(string.Format("Path \"{0}\" was not found", path));

            string jsonString = File.ReadAllText(path);

            return Deserialise<T>(jsonString);
        }

        public static T? DeserialiseFromPath<T>(string path, IFileSystem? fileSystem)
        {
            var fs = fileSystem ?? new PhysicalFileSystem();
            if (!fs.FileExists(path)) throw new FileNotFoundException(string.Format("Path \"{0}\" was not found", path));

            string jsonString = fs.ReadAllText(path);

            return Deserialise<T>(jsonString);
        }

        public static T? Deserialise<T>(string jsonString)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            return JsonSerializer.Deserialize<T>(jsonString, options);
        }

        public static T? Deserialise<T>(Stream jsonStream)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            return JsonSerializer.Deserialize<T>(jsonStream, options);
        }

    }
}
