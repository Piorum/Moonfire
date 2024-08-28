using System.Collections.Immutable;
using SCDisc.Utility;

namespace SCDisc;

public class Bot(string t, DiscordSocketConfig c) : BotBase(t,c)
{
    private static readonly ImmutableList<ulong> adminIds = [208430103384948737, 739176967064256542]; //<Discord UserIds>, @piorum, @crownsofstars
    private readonly SCPProcessInterface _server = new();

    protected override Task SlashCommandHandler(SocketSlashCommand command){
        async Task sendMessage (string a) => await command.RespondAsync($"**[{a}]**", ephemeral: true);
        async Task followUpMessage (string a) => await command.FollowupAsync($"**[{a}]**", ephemeral: true);
        Task run(Func<string, Task> a, Func<string, Task> b, Func<Task> c, object d, object e, object? f = default, Func<TimeSpan, bool>? g = default){
            return Task.Run(async () => await FuncExt.Time(a, b, c, d, e, f, g));}


        //user commands
        switch (command.Data.Name){
            case helpCmd:
                Task.Run(async () => await PrintHelp(sendMessage));
                break;
            default:
                break;
        }
        
        //admin commands
        if(!adminIds.Contains(command.User.Id)){
            _ = sendMessage("You are not an admin");
            return Task.CompletedTask;
        }
        switch(command.Data.Name){
            case "start":
                run(
                    sendMessage,
                    followUpMessage,
                    _server.StartServer,
                    "Starting",
                    () => $"Started @{_server.PublicIp}",
                    "Unusually fast, server likely already started or error occurred.",
                    elapsed => elapsed.Seconds < 1);
                break;
            case "stop":
                run(
                    sendMessage,
                    followUpMessage,
                    _server.StopServer,
                    "Stopping",
                    "Stopped",
                    "Unusually fast, server likely already stopped or error occured.",
                    elapsed => elapsed.Milliseconds < 10);
                break;
            case "console":
                run(
                    sendMessage,
                    followUpMessage,
                    () => _server.SendConsoleInput((string)command.Data.Options.First().Value),
                    "Sending",
                    $"Sent \"{(string)command.Data.Options.First().Value}\"");
                break;
            case "repopulate":
                run(
                    sendMessage,
                    followUpMessage,
                    async () => {await UnregisterCommands(); await PopulateCommands();},
                    "Starting Task",
                    "Commands Repopulated");
                break;
            default:
                break;
        }
        return Task.CompletedTask;
    }

    private static async Task PrintHelp(Func<string, Task> _SendMessage){
        string help = //Leave out start/end bracket and end \n
            " start   - starts the server              #Admin]\n" +
            "[stop    - stops the server               #Admin]\n" +
            "[console - sends remaining args to server #Admin   " ;
        await _SendMessage(help);
    }
    
    //There is probably a better way to do this
    private async Task PopulateCommands(){
        //Add all commands to all guilds
        foreach(var guild in _client.Guilds){
            await PopulateCommand("help", "Prints help information", guild);
            await PopulateCommand("start", "#Admin - Starts the server", guild);
            await PopulateCommand("stop", "#Admin - Stops the server", guild);
            await PopulateCommand("repopulate", "#Admin - Refreshes bot commands", guild);
            await PopulateCommand("console", "#Admin - Sends input to the server process", guild,
                "input", default, "Input sent to the console", true);
        }
        Console.WriteLine("All Commands Registered");
    }
    
}