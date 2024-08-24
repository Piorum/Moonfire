using System.Diagnostics;

namespace SCDisc;

public class Bot(string t, DiscordSocketConfig c) : BotBase(t,c)
{
    private readonly SCPProcessInterface _server = new();

    protected override async Task MessageRecievedHandler(SocketMessage message){  
        if (message.Author.IsBot) return;
        if (message.Content[0] == prefix[0]){
            if (await CommandHandler(message)) return;
        } else {
        //do something else with the message
            return;
        }
    }

    private async Task<bool> CommandHandler(SocketMessage message){
        var stopwatch = Stopwatch.StartNew();
        switch(message.Content)
        {
            case helpCmd:
                string _help = "";
                _help += $"[{prefix}start - starts the server ]\n";
                _help += $"[{prefix}stop  - stops the server  ]\n";
                await SendMessage(message.Channel, $"{_help}");
                break;
            case $"{prefix}start":
                await SendMessage(message.Channel, "**[Starting]**");
                stopwatch.Restart();
                await _server.StartServer();
                stopwatch.Stop();
                await SendMessage(message.Channel, $"**[Started - {stopwatch.Elapsed.Seconds}{stopwatch.Elapsed.Milliseconds:D3}ms]**");
                break;
            case $"{prefix}stop":
                await SendMessage(message.Channel, "**[Stopping]**");
                stopwatch.Restart();
                await _server.StopServer();
                stopwatch.Stop();
                await SendMessage(message.Channel, $"**[Stopped - {stopwatch.Elapsed.Seconds}{stopwatch.Elapsed.Milliseconds:D3}ms]**");
                break;
            default:
                return false;
        }
        return true;
    }
}
