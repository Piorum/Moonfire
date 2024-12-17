
namespace Sunfire;

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
        _client.JoinedGuild += JoinedGuildHandler;
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
        await _client.SetActivityAsync(new Discord.Game($"/{helpCmd}", ActivityType.Watching));

        //Block this task until program is closed
        await Task.Delay(-1);
    }

    protected virtual Task ClientReadyHandler(){return Task.CompletedTask;}

    protected virtual Task SlashCommandHandler(SocketSlashCommand command){return Task.CompletedTask;}

    private Task JoinedGuildHandler(SocketGuild guild){
        //add each user/admin command in commands list to server
        foreach (var command in commands.Where(p => p.Rank == Rank.User || p.Rank == Rank.Admin).ToList())
            _ = PopulateCommandAsync(command, guild);
        return Task.CompletedTask;
    }

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

public class Command(string _name, string _description, Rank _rank, List<CommandOption>? _options = null)
{
    public readonly string Name = _name;
    public readonly string Description = _description;
    public readonly Rank Rank = _rank;
    public readonly List<CommandOption> Options = _options ?? [];
}

public class CommandOption(string name = "", ApplicationCommandOptionType type = ApplicationCommandOptionType.String, string description = "", bool isRequired = false, List<string>? choices = null)
{
    public string Name{ get; set; } = name;
    public ApplicationCommandOptionType Type { get; set; } = type;
    public string Description { get; set; } = description;
    public bool IsRequired { get; set; } = isRequired;

    public List<string> Choices { get; set; } = choices ?? [];

}

public enum Rank{
        User,
        Admin,
        Owner
}