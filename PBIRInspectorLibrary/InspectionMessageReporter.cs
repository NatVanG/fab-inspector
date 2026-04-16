namespace PBIRInspectorLibrary;

public interface IInspectionMessageReporter
{
    void Report(MessageIssuedEventArgs args);
}

internal sealed class DelegateInspectionMessageReporter : IInspectionMessageReporter
{
    private readonly Action<MessageIssuedEventArgs> _reportAction;

    public DelegateInspectionMessageReporter(Action<MessageIssuedEventArgs> reportAction)
    {
        _reportAction = reportAction ?? throw new ArgumentNullException(nameof(reportAction));
    }

    public void Report(MessageIssuedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        _reportAction(args);
    }
}