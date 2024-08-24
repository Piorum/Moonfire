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
        if(await CheckAdmin(message.Author.Id)){
            switch(parts[0]){
                case $"{prefix}start":
                    await StartSCPServer(message);
                    return true;
                case $"{prefix}stop":
                    await StopSCPServer(message);
                    return true;
                case $"{prefix}console":
                    //safe because all inputs are caught by the scp server!
                    await _server.SendConsoleInput(message.Content[(message.Content.IndexOf(' ') + 1)..]);
                    return true;
            }
        } else {
            await SendMessage(message.Channel, "**[You are not an admin.]**");
        }
        switch(parts[0]){
            case helpCmd:
                await PrintHelp(message);
                return true;
        }
        return false;
    }

    private static Task<bool> CheckAdmin(ulong id){
        if(adminIds.Contains(id)) return Task.FromResult(true);
        return Task.FromResult(false);
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
        _help += $"[{prefix}start - starts the server                  #Admin]\n";
        _help += $"[{prefix}stop  - stops the server                   #Admin]\n";
        _help += $"[{prefix}console  - sends remaining args to server  #Admin]\n";
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
}
