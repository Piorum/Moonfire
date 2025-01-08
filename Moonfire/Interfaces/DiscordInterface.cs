namespace Moonfire.Interfaces;

public static class DI
{
    public static Task<EmbedBuilder> EmbedMessage(string input, SocketSlashCommand command){
        EmbedBuilder embed = new();
        embed.AddField($"**[{command.Data.Name.ToUpper()}]**",$"**```[{input}]```**");
        return Task.FromResult(embed);
    }

    public static async Task SendInitialSlashReplyAsync(string input, SocketSlashCommand command)=>
        await command.RespondAsync(" ", embed: (await EmbedMessage(input,command)).Build(), ephemeral: true);

    public static async Task SendSlashReplyAsync (string a, SocketSlashCommand command) =>
        await command.ModifyOriginalResponseAsync(async msg => msg.Embed = (await EmbedMessage(a,command)).Build());
}
