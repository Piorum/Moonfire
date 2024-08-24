namespace SCDisc;

public abstract class BotBase{

    protected const string prefix = ".";
    protected const string helpCmd = $"{prefix}help";
    private readonly DiscordSocketClient _client;
    private readonly string _token;

    public BotBase(string t,DiscordSocketConfig c){
        //Client Creation
        _token = t;
        _client = new DiscordSocketClient(c);

        //Define Handlers
        _client.MessageReceived += MessageRecievedHandler;
        _client.Log += LogHandler;
    }

    public async Task StartBotAsync(){
        //Starting Bot
        await _client.LoginAsync(TokenType.Bot, _token);
        await _client.StartAsync();
        await _client.SetActivityAsync(new Game(helpCmd, ActivityType.Watching));


        //Block this task until program is closed
        await Task.Delay(-1);
    }

    protected static async Task SendMessage(ISocketMessageChannel channel, string content){
        await channel.SendMessageAsync(content);
    }

    //Overloaded function, use to get IUserMessage return for messages that will need to be modified
    protected static async Task<IUserMessage> SendMessage(ISocketMessageChannel channel, string content, bool _){
        return await channel.SendMessageAsync(content);
    }

    protected static async Task ModifyMessage(IUserMessage message, string newContent){
        if(message!=null){
            await message.ModifyAsync(msg => msg.Content = newContent);
        } else {
            Console.WriteLine("No last message found to modify.");
        }
    }

    protected static Task LogHandler(LogMessage message){
        Console.WriteLine(message.ToString());
        return Task.CompletedTask;
    }

    protected abstract Task MessageRecievedHandler(SocketMessage message);
    
}