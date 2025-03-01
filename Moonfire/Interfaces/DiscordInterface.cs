using Moonfire.Types.Discord;

namespace Moonfire.Interfaces;

public static class DI
{
    public static Task<EmbedBuilder> EmbedMessage(string input, SocketSlashCommand command){
        EmbedBuilder embed = new();
        embed.AddField($"**[{command.Data.Name.ToUpper()}]**",$"**```[{input}]```**");
        return Task.FromResult(embed);
    }

    public static Task<EmbedBuilder> EmbedMessage(string input, string title){
        EmbedBuilder embed = new();
        embed.AddField($"**[{title}]**",$"**```[{input}]```**");
        return Task.FromResult(embed);
    }

    //response to slash command
    public static async Task SendResponseAsync(string input, SocketSlashCommand command, bool ephemeral = true)=>
        await command.RespondAsync(" ", embed: (await EmbedMessage(input,command)).Build(), ephemeral: ephemeral);

    public static async Task ModifyResponseAsync (string input, SocketSlashCommand command) =>
        await command.ModifyOriginalResponseAsync(async msg => msg.Embed = (await EmbedMessage(input,command)).Build());

    public static async Task SendFollowUpResponseAsync (string input, SocketSlashCommand command)=>
        await command.Channel.SendMessageAsync(" ", embed: (await EmbedMessage(input,command)).Build());

    //reponse to component interaction
    //title
    public static async Task SendResponseAsync (string input, string title, SocketMessageComponent component) =>
        await component.RespondAsync(" ", embed: (await EmbedMessage(input,title)).Build(), ephemeral:true);

    public static async Task ModifyResponseAsync (string input, string title, SocketMessageComponent component) =>
        await component.ModifyOriginalResponseAsync(async msg => msg.Embed = (await EmbedMessage(input,title)).Build());

    //no title
    public static async Task SendResponseAsync (string input, SocketMessageComponent component) =>
        await component.RespondAsync(" ", embed: (await EmbedMessage(input,"Component Response")).Build(), ephemeral:true);

    public static async Task ModifyResponseAsync (string input, SocketMessageComponent component) =>
        await component.ModifyOriginalResponseAsync(async msg => msg.Embed = (await EmbedMessage(input,"Component Response")).Build());

    //response to modal submit
    //title
    public static async Task SendResponseAsync(string input, string title, SocketModal modal) =>
        await modal.RespondAsync(" ", embed: (await EmbedMessage(input,title)).Build(), ephemeral:true);

    public static async Task ModifyResponseAsync(string input, string title, SocketModal modal) =>
        await modal.ModifyOriginalResponseAsync(async msg => msg.Embed = (await EmbedMessage(input,title)).Build());

    //no title
    public static async Task SendResponseAsync(string input, SocketModal modal) =>
        await modal.RespondAsync(" ", embed: (await EmbedMessage(input,"Modal Response")).Build(), ephemeral:true);

    public static async Task ModifyResponseAsync(string input, SocketModal modal) =>
        await modal.ModifyOriginalResponseAsync(async msg => msg.Embed = (await EmbedMessage(input,"Modal Response")).Build());

    //modal
    public static async Task SendModalAsync (MoonfireModal modal, SocketSlashCommand command) =>
        await command.RespondWithModalAsync(await BuildModal(modal));
    public static async Task SendModalAsync (MoonfireModal modal, SocketMessageComponent component) =>
        await component.RespondWithModalAsync(await BuildModal(modal));

    private static Task<Modal> BuildModal(MoonfireModal modal){
        var modalBuilder = new ModalBuilder();

        modalBuilder
            .WithTitle(modal.Title)
            .WithCustomId(modal.CustomId);

        foreach(var textInput in modal.TextInputs){
            var textInputBuilder = new TextInputBuilder();

            textInputBuilder
                .WithLabel(textInput.Label)
                .WithCustomId(textInput.CustomId)
                .WithStyle(textInput.Style)
                .WithRequired(textInput.Required);

            if(textInput.Placeholder is not null)
                textInputBuilder.WithPlaceholder(textInput.Placeholder);

            if(textInput.MinLength is not null)
                textInputBuilder.WithMinLength((int)textInput.MinLength);

            if(textInput.MaxLength is not null)
                textInputBuilder.WithMaxLength((int)textInput.MaxLength);

            modalBuilder.AddTextInput(textInputBuilder);
        }

        return Task.FromResult(modalBuilder.Build());
    }

    //component
    public static async Task SendComponentsAsync(MoonfireComponent components, SocketSlashCommand command) =>
        await command.RespondAsync(" ", components: await BuildComponent(components), ephemeral: true);

    public static async Task SendComponentsAsync(MoonfireComponent components, SocketModal modal) =>
        await modal.RespondAsync(" ", components: await BuildComponent(components), ephemeral: true);

    public static async Task SendComponentsAsync(MoonfireComponent components, SocketMessageComponent component) =>
        await component.RespondAsync(" ", components: await BuildComponent(components), ephemeral: true);

    public static Task<MessageComponent> BuildComponent(MoonfireComponent components){
        var builder = new ComponentBuilder();

        foreach(var selectMenu in components.SelectMenus){
            var menuBuilder = new SelectMenuBuilder();

            menuBuilder
                .WithCustomId(selectMenu.CustomId)
                .WithDisabled(selectMenu.Disabled)
                .WithType(selectMenu.Type);

            foreach(var option in selectMenu.Options){
                var optionBuilder = new SelectMenuOptionBuilder();

                optionBuilder
                    .WithLabel(option.Label)
                    .WithValue(option.Value)
                    .WithDefault(option.IsDefault);

                if(option.Description is not null)
                    optionBuilder.WithDescription(option.Description);

                if(option.Emote is not null)
                    optionBuilder.WithEmote(option.Emote);
    
                menuBuilder.AddOption(optionBuilder);
            }

            if(selectMenu.Placeholder is not null)
                menuBuilder.WithPlaceholder(selectMenu.Placeholder);

            if(selectMenu.ChannelTypes is not [])
                menuBuilder.WithChannelTypes(selectMenu.ChannelTypes);

            builder.WithSelectMenu(menuBuilder);
        }
        foreach(var button in components.Buttons){
            var buttonBuilder = new ButtonBuilder();

            buttonBuilder
                .WithStyle(button.Style)
                .WithDisabled(button.Disabled);

            if(button.Label is not null)
                buttonBuilder.WithLabel(button.Label);

            if(button.CustomId is not null)
                buttonBuilder.WithCustomId(button.CustomId);

            if(button.Emote is not null)
                buttonBuilder.WithEmote(button.Emote);

            if(button.Url is not null && button.Style is ButtonStyle.Link && button.CustomId is null)
                buttonBuilder.WithUrl(button.Url);

            if(button.SkuId is not null && button.Style is ButtonStyle.Premium && button.CustomId is null && button.Url is null && button.Label is null)
                buttonBuilder.WithSkuId(button.SkuId);
            
            builder.WithButton(buttonBuilder);
        }

        return Task.FromResult(builder.Build());
    }

    //generic configure responders
    public static async Task GenericConfigUpdateResponse(string message, string gameName, SocketMessageComponent component) =>
        await SendResponseAsync($"{message}]\n[Server Restart Needed",$"{gameName} Configure",component);

    public static async Task GenericConfigUpdateResponse(string message, string gameName, SocketModal modal) =>
        await SendResponseAsync($"{message}]\n[Server Restart Needed",$"{gameName} Configure",modal);

}
