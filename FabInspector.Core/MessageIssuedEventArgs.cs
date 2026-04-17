namespace FabInspector.Core
{
    public class MessageIssuedEventArgs : EventArgs
    {
        public MessageIssuedEventArgs(string message, MessageTypeEnum messageType)
        {
            Message = message;
            MessageType = messageType;
        }

        public MessageIssuedEventArgs(string itemPath, string message, MessageTypeEnum messageType)
        {
            ItemPath = itemPath;
            Message = message;
            MessageType = messageType;
        }
        
        public string? ItemPath { get; private set; }
        public string Message { get; private set; }
        public MessageTypeEnum MessageType { get; private set; }
        public bool DialogOKResponse { get; set; }
    }

    public enum MessageTypeEnum
    {
        Error,
        Warning,
        Information,
        Dialog,
        Complete
    }
}