namespace FabInspector.Core.Exceptions
{
    public class PBIRInspectorException : Exception
    {
        public PBIRInspectorException()
        {
        }

        public PBIRInspectorException(string message)
            : base(message)
        {
        }

        public PBIRInspectorException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
