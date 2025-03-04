using Moonfire.Interfaces;
using Moonfire.Types.Discord;
using AzureAllocator.Managers;

namespace Moonfire;

public abstract class BotBase{
    public const string helpCmd = $"help";
    protected readonly DiscordSocketClient _client;
    internal readonly List<MoonfireCommand> commands;
    //protected ulong ownerId;
    protected ulong ownerServerId;
    protected ulong alertsChannelId;
    private readonly string _token;

    public BotBase(string t,DiscordSocketConfig? c = null, List<MoonfireCommand>? _commands = null){
        //ownerId = ulong.Parse(Environment.GetEnvironmentVariable("BOT_OWNER_ID") ?? "0");
        ownerServerId = ulong.Parse(Environment.GetEnvironmentVariable("BOT_OWNER_SERVER_ID") ?? "0");
        alertsChannelId = ulong.Parse(Environment.GetEnvironmentVariable("BOT_ALERTS_CHANNEL_ID") ?? "0");

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

        _client.Log += async msg => {
            await Console.Out.WriteLineAsync($"[{msg.Severity}] {msg.Source}: {msg.Message}");
            if (msg.Exception != null)
                Console.WriteLine(msg.Exception);
            await Task.CompletedTask;
        };

        AzureManager.ErrorAlert += (sender, args) => SendAlert(args.AlertMessage, args.Exception);

        //Ensure commands is not null
        commands = _commands ?? [];
    }

    public async void SendAlert(string alertMessage, Exception? e){
        var guild = _client.GetGuild(ownerServerId);
        if(guild is not null){
            var channel = guild.GetTextChannel(alertsChannelId);
            if(channel is not null){
                var sendAlertTask = channel.SendMessageAsync(alertMessage);
                var logAlertTask = Console.Out.WriteLineAsync(alertMessage);
                var logExceptionTask = Console.Out.WriteLineAsync($"{e}");
                await Task.WhenAll(logAlertTask, sendAlertTask, logExceptionTask);
            }
        }
    }

    public async Task StartBotAsync(){
        await PreStartupTasks();

        //Starting Bot
        await _client.LoginAsync(TokenType.Bot, _token);
        await _client.StartAsync();
        await _client.SetActivityAsync(new Discord.Game($"/{helpCmd}", ActivityType.Watching));
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
        List<Task> deletions = [];
        foreach(var guild in _client.Guilds){
            deletions.Add(guild.DeleteApplicationCommandsAsync());
        }

        //deletes all global commands
        var globalCommands = await _client.GetGlobalApplicationCommandsAsync();
        foreach(var command in globalCommands){
            deletions.Add(command.DeleteAsync());
        }

        await Task.WhenAll(deletions);
        
        Console.WriteLine("Commands Unregistered");
    }

    internal static async Task PrintHelpTaskAsync(SocketSlashCommand command, BotBase bot){
        //empty string in case no commands
        string help = "";
        //formats and get information for every command
        foreach(var cmd in bot.commands.Where(p => p.Rank == MoonfireCommandRank.User || p.Rank == MoonfireCommandRank.Admin).ToList())
            help += $"[{cmd.Name} - {cmd.Description}]\n";
        //to reduce code this removes the first/last bracket and last newline to match expected formatting
        await DI.ModifyResponseAsync(help[(help.IndexOf('[')+1)..help.LastIndexOf(']')],command);
    }

    internal static async Task RepopulateTaskAsync(SocketSlashCommand command, BotBase bot){
        //ensure initial reply is sent first
        await DI.ModifyResponseAsync("Repopulating Commands",command);
        await bot.UnregisterCommandsAsync();
        await bot.PopulateCommandsAsync(bot.ownerServerId);
        _ = DI.ModifyResponseAsync("Commands Repopulated",command);
    }
    
}