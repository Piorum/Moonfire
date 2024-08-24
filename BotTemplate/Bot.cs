namespace SCDisc;

public class Bot(string t, DiscordSocketConfig c) : BotBase(t,c)
{
    private readonly SCPProcessInterface _server = new();

    protected override async Task MessageRecievedHandler(SocketMessage message){  
        if (message.Author.IsBot) return;
        if (message.Content[0] == prefix[0]){
            if (await CommandHandler(message)) return;
        } else {
        //do something else with the message
            return;
        }
    }

    private async Task<bool> CommandHandler(SocketMessage message){
        switch(message.Content)
        {
            case helpCmd:
                string _help = "";
                _help += $"[{prefix}start - starts the server ]\n";
                _help += $"[{prefix}stop  - stops the server  ]\n";
                await SendMessage(message.Channel, $"{_help}");
                break;
            case $"{prefix}start":
                await SendMessage(message.Channel, "[Starting]");
                await _server.StartServer();
                await SendMessage(message.Channel, "[Started]");
                break;
            case $"{prefix}stop":
                await SendMessage(message.Channel, "[Stopping]");
                await _server.StopServer();
                await SendMessage(message.Channel, "[Stopped]");
                break;
            default:
                return false;
        }
        return true;
    }

}
