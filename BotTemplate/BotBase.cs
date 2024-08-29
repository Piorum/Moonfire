namespace SCDisc;

public abstract class BotBase{
    protected const string helpCmd = $"help";
    protected readonly DiscordSocketClient _client;
    private readonly string _token;

    public BotBase(string t,DiscordSocketConfig? c = null){
        //Client Creation
        _token = t;
        if(c!=null){
            _client = new DiscordSocketClient(c);
        } else { 
            _client = new DiscordSocketClient();
        }

        //Define Handlers
        _client.SlashCommandExecuted += SlashCommandHandler;
        _client.Log += message => {
            Console.WriteLine(message);
            return Task.CompletedTask;
        };
    }

    public async Task StartBotAsync(){
        //Starting Bot
        await _client.LoginAsync(TokenType.Bot, _token);
        await _client.StartAsync();
        await _client.SetActivityAsync(new Game($"/{helpCmd}", ActivityType.Watching));

        //Block this task until program is closed
        await Task.Delay(-1);
    }

    protected virtual Task SlashCommandHandler(SocketSlashCommand command){return Task.CompletedTask;}

    //Registered command with a guild, optional options arg
    protected static async Task PopulateCommand(Command _command, CommandOption[]? _options = null){
        var command = new SlashCommandBuilder().WithName(_command.Name.ToLower()).WithDescription(_command.Description);
        
        if(_options != null)
            foreach(var option in _options)
                command.AddOption(option.Name.ToLower(), option.Type, option.Description, isRequired: option.IsRequired);

        await _command.Guild.CreateApplicationCommandAsync(command.Build());

        Console.WriteLine($"Command {_command.Name} registered at {_command.Guild.Id}");
    }

    protected async Task UnregisterCommands(){
        //deletes every command from every guild
        foreach(var guild in _client.Guilds){
            await guild.DeleteApplicationCommandsAsync();
        }
        Console.WriteLine("Commands Unregistered");
    }
    
}