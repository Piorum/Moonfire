using Moonfire.Interfaces;
using Moonfire.Types.Discord;

namespace Moonfire;

public abstract class BotBase{
    public const string helpCmd = $"help";
    protected readonly DiscordSocketClient _client;
    public readonly List<MoonfireCommand> commands;
    protected ulong ownerId;
    protected ulong ownerServerId;
    private readonly string _token;

    public BotBase(string t,DiscordSocketConfig? c = null, List<MoonfireCommand>? _commands = null){
        ownerId = ulong.Parse(Environment.GetEnvironmentVariable("BOT_OWNER_ID") ?? "0");
        ownerServerId = ulong.Parse(Environment.GetEnvironmentVariable("BOT_OWNER_SERVER_ID") ?? "0");

        //Client Creation
        _token = t;
        if(c!=null){
            _client = new DiscordSocketClient(c);
        } else { 
            _client = new DiscordSocketClient();
        }

        //Define Handlers
        _client.Ready += ClientReadyHandler;

        _client.SlashCommandExecuted += SlashCommandHandler;
        _client.ModalSubmitted += ModalSubmissionHandler;
        _client.ButtonExecuted += ComponentHandler;
        _client.SelectMenuExecuted += ComponentHandler;

        _client.Log += message => {
            Console.WriteLine(message);
            return Task.CompletedTask;
        };

        //Ensure commands is not null
        commands = _commands ?? [];
    }

    public async Task StartBotAsync(){
        await PreStartupTasks();

        //Starting Bot
        await _client.LoginAsync(TokenType.Bot, _token);
        await _client.StartAsync();
        await _client.SetActivityAsync(new Discord.Game($"/{helpCmd}", ActivityType.Watching));

        //Block this task until program is closed
        await Task.Delay(-1);
    }

    protected virtual Task PreStartupTasks(){return Task.CompletedTask;}

    protected virtual Task ClientReadyHandler(){return Task.CompletedTask;}

    protected virtual Task SlashCommandHandler(SocketSlashCommand command){return Task.CompletedTask;}

    protected virtual Task ModalSubmissionHandler(SocketModal modal){return Task.CompletedTask;}

    protected virtual Task ComponentHandler(SocketMessageComponent component){return Task.CompletedTask;}

    protected async Task PopulateCommandsAsync(ulong ownerServer){
        // Register normal commands globally
        foreach (var command in commands.Where(p => p.Rank == MoonfireCommandRank.User || p.Rank == MoonfireCommandRank.Admin).ToList())
            await PopulateGlobalCommandAsync(command);

        // Register owner commands for a specified server
        foreach (var command in commands.Where(p => p.Rank == MoonfireCommandRank.Owner).ToList())
            await PopulateGuildCommandAsync(command, _client.GetGuild(ownerServer));

        Console.WriteLine("All Commands Registered");
    }

    private static Task<SlashCommandProperties> BuildCommand(MoonfireCommand _command){
        var command = new SlashCommandBuilder().WithName(_command.Name.ToLower()).WithDescription(_command.Description);
        
        foreach(var option in _command.Options){
            var optionBuilder = new SlashCommandOptionBuilder();
            optionBuilder.WithName(option.Name.ToLower());
            optionBuilder.WithDescription(option.Description);
            optionBuilder.WithType(option.Type);
            optionBuilder.WithRequired(option.IsRequired);
            int i = 1;
            foreach(var choice in option.Choices){
                optionBuilder.AddChoice(choice, i.ToString());
                i++;
            }
            command.AddOption(optionBuilder);
        }

        return Task.FromResult(command.Build());
    }

    private static async Task PopulateGuildCommandAsync(MoonfireCommand _command, SocketGuild guild){
        await guild.CreateApplicationCommandAsync(await BuildCommand(_command));
        Console.WriteLine($"Command {_command.Name} registered at {guild.Id}");
    }

    private async Task PopulateGlobalCommandAsync(MoonfireCommand _command){
        await _client.CreateGlobalApplicationCommandAsync(await BuildCommand(_command));
        Console.WriteLine($"Command {_command.Name} registered globally");
    }

    protected async Task UnregisterCommandsAsync(){
        //deletes every command from every guild
        foreach(var guild in _client.Guilds){
            await guild.DeleteApplicationCommandsAsync();
        }
        Console.WriteLine("Commands Unregistered");
    }

    public static async Task PrintHelpTaskAsync(SocketSlashCommand command, BotBase bot){
        //empty string in case no commands
        string help = "";
        //formats and get information for every command
        foreach(var cmd in bot.commands.Where(p => p.Rank == MoonfireCommandRank.User || p.Rank == MoonfireCommandRank.Admin).ToList())
            help += $"[{cmd.Name} - {cmd.Description}]\n";
        //to reduce code this removes the first/last bracket and last newline to match expected formatting
        await DI.ModifyResponseAsync(help[(help.IndexOf('[')+1)..help.LastIndexOf(']')],command);
    }

    public static async Task RepopulateTaskAsync(SocketSlashCommand command, BotBase bot){
        //ensure initial reply is sent first
        await DI.ModifyResponseAsync("Repopulating Commands",command);
        await bot.UnregisterCommandsAsync();
        await bot.PopulateCommandsAsync(bot.ownerServerId);
        _ = DI.ModifyResponseAsync("Commands Repopulated",command);
    }
    
}