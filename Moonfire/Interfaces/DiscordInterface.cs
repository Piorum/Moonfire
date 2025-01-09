using Moonfire.Types;

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

    public static async Task SendModalResponseAsync (MoonfireModal modal, SocketSlashCommand command){
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
                textInputBuilder.WithMinLength((int)textInput.MaxLength);

            modalBuilder.AddTextInput(textInputBuilder);
        }

        await command.RespondWithModalAsync(modalBuilder.Build());
    }

    public static async Task SendComponentResponseAsync (MoonfireComponent components, SocketSlashCommand command){
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
                .WithLabel(button.Label)
                .WithCustomId(button.CustomId)
                .WithStyle(button.Style)
                .WithDisabled(button.Disabled);

            if(button.Emote is not null)
                buttonBuilder.WithEmote(button.Emote);

            if(button.Url is not null && button.Style is ButtonStyle.Link)
                buttonBuilder.WithUrl(button.Url);
            
            builder.WithButton(buttonBuilder);
        }

        await command.RespondAsync(" ", components: builder.Build(), ephemeral: true);
    }
}
