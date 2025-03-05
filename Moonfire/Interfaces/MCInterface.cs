namespace Moonfire.Interfaces;

public class MCInterface : IServer<MCInterface>, IServerBase
{
    public string PublicIp => throw new NotImplementedException();

    public static Task<MCInterface?> CreateInterfaceAsync(string name, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<bool> StartServerAsync(Func<string, Task> messageSenderCallback, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<bool> StopServerAsync(Func<string, Task> messageSenderCallback, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public static Task<bool> Updating(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
