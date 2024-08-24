using System.Diagnostics;

namespace SCDisc;

public class Bot(string t, DiscordSocketConfig c) : BotBase(t,c)
{
    private static readonly List<ulong> adminIds = [208430103384948737, 739176967064256542]; //Discord UserIds, @piorum, @crownsofstars
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
        string[] parts = message.Content.Split(' ');
        switch(parts[0]){
            case helpCmd:
                await PrintHelp(message);
                break;
            case $"{prefix}start":
                await RequireAdmin(
                    message,
                    () => StartSCPServer(message)
                );
                break;
            case $"{prefix}stop":
                await RequireAdmin(
                    message,
                    () => StopSCPServer(message)
                );
                break;
            case $"{prefix}console":
                await RequireAdmin(
                    message,
                    () => SendConsoleInput(message)
                );
                break;
            default:
                return false;
        }
        return true;
    }

    private async static Task RequireAdmin(SocketMessage message, Func<Task> function){
        //if id is in admins list runs the function, if not sends error
        var task = adminIds.Contains(message.Author.Id) ? function() : SendMessage(message.Channel, "**[You are not an admin.]**");
        await task;
    }

    private static async Task TimeFunction(SocketMessage message, string start, Func<Task> function, string end, string warning, Func<TimeSpan, bool> warningCondition){
        //Store message and send start message
        //Start timer
        //Run function
        //Stop timer
        //Update message
        var _tmsg = await SendMessage(message.Channel, $"**[{start}]**", true);
        var stopwatch = Stopwatch.StartNew();
        await function();
        stopwatch.Stop();
        await ModifyMessage(_tmsg, $"**[{end} - {stopwatch.ElapsedMilliseconds:D3}ms]**");

        //Time based error check
        //elapsed => elapsed.Unit(seconds/milliseconds etc...) < x(int value)
        if(warningCondition(stopwatch.Elapsed)){
            await ModifyMessage(_tmsg, $"**[{warning}]**");
        }
    }

    private static async Task PrintHelp(SocketMessage message){
        string _help = "";
        _help += $"[{prefix}start   - starts the server              #Admin]\n";
        _help += $"[{prefix}stop    - stops the server               #Admin]\n";
        _help += $"[{prefix}console - sends remaining args to server #Admin]\n";
        await SendMessage(message.Channel, $"**{_help}**");
    }

    private async Task StartSCPServer(SocketMessage message){
        await TimeFunction(
            message,
            "Starting",
            _server.StartServer,
            "Started",
            "Unusually fast, server likley already started or error occured.",
            elapsed => elapsed.Seconds < 1
        );
    }

    private async Task StopSCPServer(SocketMessage message){
        await TimeFunction(
            message,
            "Stopping",
            _server.StopServer,
            "Stopped",
            "Unusually fast, server likely already stopped or error occured.",
            elapsed => elapsed.Milliseconds < 10
        );
    }

    private async Task SendConsoleInput(SocketMessage message){
        string input = message.Content[(message.Content.IndexOf(' ') + 1)..];
        await TimeFunction(
            message,
            "Sending",
            () => _server.SendConsoleInput(input),
            $"Sent \"{input}\"",
            "Unusually fast, server likely stopped or error occured.",
            elapsed => elapsed.Milliseconds < 0
        );
    }
}
