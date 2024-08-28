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

    //Registered command with a guild, extra values for option
    //Replace option params with a single list of addoption function pointers, iterate through them
    protected static async Task PopulateCommand(string name, string description, SocketGuild guild, 
    string? optionName = null, ApplicationCommandOptionType optionType = ApplicationCommandOptionType.String, string? optionDescription = null, bool optionRequirment = false){
        var command = new SlashCommandBuilder();
        command.WithName(name.ToLower());
        command.WithDescription(description);
        if(optionName != null && optionDescription != null) 
            command.AddOption(optionName.ToLower(), ApplicationCommandOptionType.String, optionDescription, isRequired: optionRequirment);

        await guild.CreateApplicationCommandAsync(command.Build());

        Console.WriteLine($"Command {name} registered at {guild.Id}");
    }

    protected async Task UnregisterCommands(){
        //deletes every command from every guild
        foreach(var guild in _client.Guilds){
            await guild.DeleteApplicationCommandsAsync();
        }
        Console.WriteLine("Commands Unregistered");
    }
    
}