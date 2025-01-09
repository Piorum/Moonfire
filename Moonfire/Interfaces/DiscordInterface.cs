using Discord.Interactions;
using Discord;
using Discord.WebSocket;

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

    public static async Task SendSlashReplyAsync (string input, SocketSlashCommand command) =>
        await command.ModifyOriginalResponseAsync(async msg => msg.Embed = (await EmbedMessage(input,command)).Build());

    public static async Task SendModalResponseAsync (string input, SocketSlashCommand command){
        var mb = new ModalBuilder()
            .WithTitle("Fav Food")
            .WithCustomId("food_menu")
            .AddTextInput("What??", "food_name", placeholder:"Pizza")
            .AddTextInput("Why??", "food_reason", TextInputStyle.Paragraph,"Kus it's so tasty");

        await command.RespondWithModalAsync(mb.Build());
    }

    public static async Task SendComponentResponseAsync (string input, SocketSlashCommand command){
        var menuBuilder = new SelectMenuBuilder()
            .WithPlaceholder("Select an option")
            .WithCustomId("testmenu")
            .WithMinValues(1)
            .WithMaxValues(1)
            .AddOption("Option A", "opt1", "Option B is lying!")
            .AddOption("Option B", "opt2", "Option A is telling the truth!");

        var builder = new ComponentBuilder()
            .WithSelectMenu(menuBuilder);

        await command.RespondAsync(" ", components: builder.Build(), ephemeral: true);
    }
}
