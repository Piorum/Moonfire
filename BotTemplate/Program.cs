namespace SCDisc;

public class Program{
    
    //entry point
    public static async Task Main(){
        var token = File.ReadAllText("token.txt");
        var config = new DiscordSocketConfig{
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
        };

        var _application = new Bot(token,config);
        await _application.StartBotAsync();
    }
}