namespace PBIRInspectorLibrary
{
    public class InspectorBase
    {
        private readonly IEnumerable<JsonLogicOperatorRegistry> _registries;
        protected readonly IFabricFileSystem _fileSystem;

        public InspectorBase(InspectionRules inspectionRules, IEnumerable<JsonLogicOperatorRegistry> registries, IFabricFileSystem fileSystem)
        {
            if (fileSystem == null) throw new ArgumentNullException(nameof(fileSystem));
            _fileSystem = fileSystem;
            _fileSystem.ScopedItemTypes = inspectionRules.Rules.SelectMany(_ => _.ItemType.Split("|")).Distinct();
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
