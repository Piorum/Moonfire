namespace SCDisc;

public class Bot(string t, DiscordSocketConfig c) : BotBase(t,c)
{
    protected override async Task MessageRecievedHandler(SocketMessage message){  
        if (message.Author.IsBot) return;
        if (message.Content[0] == prefix[0]){
            if (await CommandHandlerAsync(message)) return;
        } else {
        //do something else with the message
            return;
        }
    }

    private async Task<bool> CommandHandlerAsync(SocketMessage message){
        switch(message.Content)
        {
            case helpCmd:
                await SendMessage(message.Channel, "<Not Implemented>");
                break;
            case $"{prefix}example":
                await SendMessage(message.Channel, "Hello World!");
                break;
            default:
                return false;
        }
        return true;
    }

}
