using System.Text.Json;

namespace PBIRInspectorLibrary
{
    public class InspectorBase
    {
        //private readonly IEnumerable<IJsonLogicOperator> _customOperators;
        private readonly IEnumerable<JsonLogicOperatorRegistry> _registries;
        protected readonly IFileSystem _fileSystem;

        public InspectorBase(InspectionRules inspectionRules, IEnumerable<JsonLogicOperatorRegistry> registries, IFileSystem fileSystem)
        {
            if (fileSystem == null) throw new ArgumentNullException(nameof(fileSystem));
            _fileSystem = fileSystem;
            _registries = registries;
            UseRegistries();
        }

        public InspectorBase(string rulesPath, IEnumerable<JsonLogicOperatorRegistry> registries, IFileSystem fileSystem)
        {
            if (fileSystem == null) throw new ArgumentNullException(nameof(fileSystem));
            _fileSystem = fileSystem;
            _registries = registries;
            UseRegistries();
        }

        private void UseRegistries()
        {
            foreach (var registry in _registries)
            {
                registry.RegisterAll();
                // Use registry.SerializerContext and registry.Operators as needed
            }
        }

        public static T? DeserialiseRulesFromPath<T>(string rulesPath)
        {
            if (!File.Exists(rulesPath)) throw new FileNotFoundException(string.Format("Rules with path \"{0}\" was not found", rulesPath));

            string jsonString = File.ReadAllText(rulesPath);

            return DeserialiseRules<T>(jsonString);
        }

        public static T? DeserialiseRulesFromPath<T>(string rulesPath, IFileSystem? fileSystem)
        {
            var fs = fileSystem ?? new PhysicalFileSystem();
            if (!fs.FileExists(rulesPath)) throw new FileNotFoundException(string.Format("Rules with path \"{0}\" was not found", rulesPath));

            string jsonString = fs.ReadAllText(rulesPath);

            return DeserialiseRules<T>(jsonString);
        }

        public T? DeserialiseRulesFromPathInstance<T>(string rulesPath)
        {
            return DeserialiseRulesFromPath<T>(rulesPath, _fileSystem);
        }

        public static T? DeserialiseRules<T>(string jsonString)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            return JsonSerializer.Deserialize<T>(jsonString, options);
        }

        public static T? DeserialiseRules<T>(Stream jsonStream)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            return JsonSerializer.Deserialize<T>(jsonStream, options);
        }
    }
}
