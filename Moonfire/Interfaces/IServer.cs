namespace Moonfire.Interfaces;

public interface IServer<TSelf> 
    where TSelf : IServer<TSelf>
{
    // Static abstract method to “create” an instance of TSelf
    static abstract Task<TSelf?> CreateInterfaceAsync(string name, CancellationToken cancellationToken = default);
    Task<bool> StartServerAsync(Func<string, Task> messageSenderCallback, CancellationToken cancellationToken = default);
    Task<bool> StopServerAsync(Func<string, Task> messageSenderCallback, CancellationToken cancellationToken = default);
    string PublicIp { get; }
}

public interface IServerBase
{
    Task<bool> StartServerAsync(Func<string, Task> messageSenderCallback, CancellationToken cancellationToken = default);
    Task<bool> StopServerAsync(Func<string, Task> messageSenderCallback, CancellationToken cancellationToken = default);
    string PublicIp { get; }
}