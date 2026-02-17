using System.Text.Json;

namespace PBIRInspectorLibrary
{
    public class InspectorBase
    {
        //private readonly IEnumerable<IJsonLogicOperator> _customOperators;
        private readonly IEnumerable<JsonLogicOperatorRegistry> _registries;

        public InspectorBase(string fabricItemPath, InspectionRules inspectionRules, IEnumerable<JsonLogicOperatorRegistry> registries)
        {
            if (string.IsNullOrEmpty(fabricItemPath)) throw new ArgumentNullException(nameof(fabricItemPath));
            if (!File.Exists(fabricItemPath) && !Directory.Exists(fabricItemPath)) throw new FileNotFoundException();
            _registries = registries;
            UseRegistries();
        }

        public InspectorBase(string fabricItemPath, string rulesPath, IEnumerable<JsonLogicOperatorRegistry> registries)
        {
            if (string.IsNullOrEmpty(fabricItemPath)) throw new ArgumentNullException(nameof(fabricItemPath));
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
