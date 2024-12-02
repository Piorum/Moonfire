
namespace SCDisc;

public abstract class BotBase{
    protected const string helpCmd = $"help";
    protected readonly DiscordSocketClient _client;
    protected readonly List<Command> commands;
    protected ulong ownerId;
    protected ulong ownerServerId;
    private readonly string _token;

    public BotBase(string t,DiscordSocketConfig? c = null, List<Command>? _commands = null){
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
        _client.Log += message => {
            Console.WriteLine(message);
            return Task.CompletedTask;
        };

        //Ensure commands is not null
        commands = _commands ?? [];
    }

    public async Task StartBotAsync(){
        //Starting Bot
        await _client.LoginAsync(TokenType.Bot, _token);
        await _client.StartAsync();
        await _client.SetActivityAsync(new Game($"/{helpCmd}", ActivityType.Watching));

        //Block this task until program is closed
        await Task.Delay(-1);
    }

    protected virtual Task ClientReadyHandler(){return Task.CompletedTask;}

    protected virtual Task SlashCommandHandler(SocketSlashCommand command){return Task.CompletedTask;}

    // Tried refactoring the PopulateCommandsAsync to have less redundancy
    // Let me know if this does not end up working - Jarod 8/30/24 7:17PM
    // It didn't really work but it gave me some good ideas - Piorum 8/31/24 02:05:44AM
    protected async Task PopulateCommandsAsync(ulong ownerServer){
        foreach (var command in commands.Where(p => p.Rank == Rank.User || p.Rank == Rank.Admin).ToList())
            foreach (var guild in _client.Guilds)
                await PopulateCommandAsync(command, guild);

        // Register owner commands for a specified server
        foreach (var command in commands.Where(p => p.Rank == Rank.Owner).ToList())
            await PopulateCommandAsync(command, _client.GetGuild(ownerServer));

        Console.WriteLine("All Commands Registered");
    }

    private static async Task PopulateCommandAsync(Command _command, SocketGuild guild){
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


        await guild.CreateApplicationCommandAsync(command.Build());

        Console.WriteLine($"Command {_command.Name} registered at {guild.Id}");
    }

    protected async Task UnregisterCommandsAsync(){
        //deletes every command from every guild
        foreach(var guild in _client.Guilds){
            await guild.DeleteApplicationCommandsAsync();
        }
        Console.WriteLine("Commands Unregistered");
    }
    
}