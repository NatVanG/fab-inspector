namespace PBIRInspectorLibrary
{
    public class InspectorBase
    {
        private readonly IEnumerable<JsonLogicOperatorRegistry> _registries;
        protected readonly IFileSystem _fileSystem;

        public InspectorBase(InspectionRules inspectionRules, IEnumerable<JsonLogicOperatorRegistry> registries, IFileSystem fileSystem)
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
            }
        }
    }
}
