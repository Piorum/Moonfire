using System.Collections.Immutable;
using SCDisc.Utility;

namespace SCDisc;

public class Bot(string t, DiscordSocketConfig c) : BotBase(t,c)
{
    private static readonly ImmutableList<ulong> adminIds = [208430103384948737, 739176967064256542]; //Discord UserIds, @piorum, @crownsofstars
    private readonly SCPProcessInterface _server = new();

    protected override async Task MessageRecievedHandler(SocketMessage message){
        if (message.Author.IsBot) return;
        if (message.Content[0] == prefix[0]){
            if (await MessageCommandHandler(message)) return; // Ends if handled
        }else{
            // Do something else with the message
            return;
        }
    }

    private async Task<bool> MessageCommandHandler(SocketMessage message){
        async Task<IUserMessage> sendMessage   (string a)                 => await SendMessage   (message.Channel, $"**[{a}]**", true);
        async Task               modifyMessage (IUserMessage a, string b) => await ModifyMessage (a, $"**[{b}]**");
        string[] args = message.Content.Split(' ');
        
        return args[0] switch{
            helpCmd =>
                await RunTask(() => 
                    PrintHelp(sendMessage)),
            $"{prefix}start" => 
                await RequireAdmin(message.Author.Id, sendMessage, () => 
                    FuncExt.Time(
                        sendMessage,
                        modifyMessage,
                        _server.StartServer,
                        "Starting",
                        () => $"Started @{_server.PublicIp}",
                        "Unusually fast, server likely already started or error occurred.",
                        elapsed => elapsed.Seconds < 1)),
            $"{prefix}stop" => 
                await RequireAdmin(message.Author.Id, sendMessage, () => 
                    FuncExt.Time(
                        sendMessage,
                        modifyMessage,
                        _server.StopServer,
                        "Stopping",
                        "Stopped",
                        "Unusually fast, server likely already stopped or error occured.",
                        elapsed => elapsed.Milliseconds < 10)),
            $"{prefix}console" =>
                await RequireAdmin(message.Author.Id, sendMessage, () =>
                    FuncExt.Time(
                        sendMessage,
                        modifyMessage,
                        () => _server.SendConsoleInput(message.Content[(message.Content.IndexOf(' ') + 1)..]),
                        "Sending",
                        $"Sent \"{message.Content[(message.Content.IndexOf(' ') + 1)..]}\"")),
            _ => false,
        };
    }

    private async static Task<bool> RequireAdmin(ulong id, Func<string, Task> _SendMessage, Func<Task> function){
        var task = adminIds.Contains(id) ? function() : _SendMessage("**[You are not an admin.]**");
        await task;
        return true;
    }

    private async static Task<bool> RunTask(Func<Task> function){
        await function();
        return true;
    }

    private static async Task PrintHelp(Func<string, Task> _SendMessage){
        static string _f(string content) => $"[{prefix}{content}]\n";
        string help = string.Join(
            _f("start   - starts the server              #Admin"),
            _f("stop    - stops the server               #Admin"),
            _f("console - sends remaining args to server #Admin")
        );
        await _SendMessage($"**{help}**");
    }
    
}