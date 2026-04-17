namespace FabInspector.Core
{
    /// <summary>
    /// Extended event args for structured progress reporting from FabricRemoteFileSystem.
    /// Backward-compatible: existing subscribers see this as MessageIssuedEventArgs.
    /// Advanced clients can downcast for structured progress data (e.g., progress bars).
    /// </summary>
    public class FabricProgressEventArgs : MessageIssuedEventArgs
    {
        public FabricProgressEventArgs(string operationName, string message)
            : base(message, MessageTypeEnum.Information)
        {
            OperationName = operationName;
        }

        public FabricProgressEventArgs(string operationName, string message, int currentStep, int totalSteps)
            : base(message, MessageTypeEnum.Information)
        {
            OperationName = operationName;
            CurrentStep = currentStep;
            TotalSteps = totalSteps;
        }

        public FabricProgressEventArgs(string operationName, string message, string? itemId, string? itemName)
            : base(message, MessageTypeEnum.Information)
        {
            OperationName = operationName;
            ItemId = itemId;
            ItemDisplayName = itemName;
        }

        public FabricProgressEventArgs(string operationName, string message, string? itemId, string? itemName, int currentStep, int totalSteps)
            : base(message, MessageTypeEnum.Information)
        {
            OperationName = operationName;
            ItemId = itemId;
            ItemDisplayName = itemName;
            CurrentStep = currentStep;
            TotalSteps = totalSteps;
        }

        /// <summary>
        /// The name of the long-running operation (e.g., "LoadItemDefinition", "LoadWorkspaceItems").
        /// </summary>
        public string OperationName { get; }

        /// <summary>
        /// Current step number within the operation (e.g., polling attempt number).
        /// </summary>
        public int? CurrentStep { get; }

        /// <summary>
        /// Total expected steps for the operation (e.g., max LRO polling attempts).
        /// </summary>
        public int? TotalSteps { get; }

        /// <summary>
        /// The Fabric item ID being processed, if applicable.
        /// </summary>
        public string? ItemId { get; }

        /// <summary>
        /// The display name of the Fabric item being processed, if available.
        /// </summary>
        public string? ItemDisplayName { get; }
    }
}
