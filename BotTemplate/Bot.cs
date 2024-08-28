using System.Collections.Immutable;
using SCDisc.Utility;

namespace SCDisc;

public class Bot(string t, DiscordSocketConfig? c = null) : BotBase(t,c)
{
    private static readonly ImmutableList<ulong> adminIds = [208430103384948737, 739176967064256542]; //<Discord UserIds>, @piorum, @crownsofstars
    private readonly SCPProcessInterface _server = new();

    protected override Task SlashCommandHandler(SocketSlashCommand command){
        async Task sendMessage (string a) => await command.RespondAsync($"**[{a}]**", ephemeral: true);
        async Task followUpMessage (string a) => await command.FollowupAsync($"**[{a}]**", ephemeral: true);
        Task run(Task a){
            return Task.Run(async () => await a);}
        Task runT(Func<Task> a, object b, object c, object? d = default, Func<TimeSpan, bool>? e = default){
            return run(FuncExt.Time(sendMessage, followUpMessage, a, b, c, d, e));}


        //user commands
        switch (command.Data.Name){
            case helpCmd:
                run(
                    PrintHelp(sendMessage));
                break;
            default:
                break;
        }
        
        //admin commands
        if(!adminIds.Contains(command.User.Id)){
            run(
                sendMessage("You are not an admin"));
            return Task.CompletedTask;
        }
        switch(command.Data.Name){
            case "start":
                runT(
                    _server.StartServer,
                    "Starting",
                    () => $"Started @{_server.PublicIp}",
                    "Unusually fast, server likely already started or error occurred.",
                    elapsed => elapsed.Seconds < 1);
                break;
            case "stop":
                runT(
                    _server.StopServer,
                    "Stopping",
                    "Stopped",
                    "Unusually fast, server likely already stopped or error occured.",
                    elapsed => elapsed.Milliseconds < 10);
                break;
            case "console":
                runT(
                    () => _server.SendConsoleInput((string)command.Data.Options.First().Value),
                    "Sending",
                    $"Sent \"{(string)command.Data.Options.First().Value}\"");
                break;
            case "repopulate":
                runT(
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
             "start   - starts the server              #Admin]\n" +
            "[stop    - stops the server               #Admin]\n" +
            "[console - sends remaining args to server #Admin"    ;
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
                [new(name: "input", description: "Input sent to the console", isRequired: true)]);
        }
        Console.WriteLine("All Commands Registered");
    }
    
}