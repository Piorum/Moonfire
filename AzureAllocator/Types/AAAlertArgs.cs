namespace AzureAllocator.Types;

public class AAAlertArgs(string alertMessage, Exception? exception = null) : EventArgs
{
    public string AlertMessage { get; } = alertMessage;
    public Exception Exception { get; } = exception ?? new("Exception Arg Null");
}
