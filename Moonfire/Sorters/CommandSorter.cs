using Moonfire.Interfaces;
using Moonfire.Workers;
using Moonfire.Types.Discord;
using Moonfire.ComponentBuilders;
using Moonfire.Credit;

namespace Moonfire.Sorters;

public static class CommandSorter
{
    public static Task<(Task,ResponseType)> GetTask(SocketSlashCommand command, Bot bot){
        //finds first command that matches name of passed command
        //if a command was found gets the rank of that command
        //gets proper task to run as background task
        return bot.commands.FirstOrDefault(p => p.Name == command.Data.Name)?.Rank switch
        {
            MoonfireCommandRank.User => UserCommandHandler(command, bot),

            MoonfireCommandRank.Admin => AdminCommandHandler(command, bot),

            MoonfireCommandRank.Owner => OwnerCommandHandler(command, bot),

            _ => Task.FromResult((DI.ModifyResponseAsync("Failed to find command in commands list",command),ResponseType.BASIC))
        };
    }

    private static Task<(Task,ResponseType)> UserCommandHandler(SocketSlashCommand command, Bot bot){
        return command.Data.Name switch
        {
            BotBase.helpCmd => Task.FromResult((BotBase.PrintHelpTaskAsync(command, bot),ResponseType.BASIC)),

            _ => Task.FromResult((DI.ModifyResponseAsync($"Caught {command.Data.Name} by user handler but found no command", command),ResponseType.BASIC))
        };
    }



    private static async Task<(Task,ResponseType)> AdminCommandHandler(SocketSlashCommand command, Bot bot){
        if(!((SocketGuildUser)command.User).GuildPermissions.Administrator){
            return (DI.ModifyResponseAsync("You are not an admin",command),ResponseType.BASIC);
        }

        return command.Data.Name switch
        {
            "start" => await StartCommandHandler(command,bot),

            "stop" => await StopCommandHandler(command,bot),

            "configure" => await ConfigureCommandHandler(command),

            "region" => (DI.SendComponentsAsync(await BOTComponentBuilder.GetRegionSelectionComponents(),command),ResponseType.COMPONENT),

            "shop" => (DI.SendComponentsAsync(new(buttons:[
                new(label: "Shop", style: ButtonStyle.Link, url: "https://discord.com/discovery/applications/1077479824093888522/store"),
                new(style: ButtonStyle.Premium, skuId: 1339100750487355446),
                new(style: ButtonStyle.Premium, skuId: 1345145487824785490),
                new(style: ButtonStyle.Premium, skuId: 1345145596549529763) ]),command),ResponseType.COMPONENT),

            "checkcredit" => (Task.Run(async () => {
                var guildId = command.GuildId;
                if(guildId is null) {
                    await DI.ModifyResponseAsync("Error Getting GuildId",command);
                    return;
                }

                await DI.ModifyResponseAsync($"Guild has '{Math.Round(await CreditTableManager.GetCredit($"{guildId}"),3)}' credit",command);
            }),ResponseType.BASIC),

            _ => (DI.ModifyResponseAsync($"Caught {command.Data.Name} by admin handler but found no command",command),ResponseType.BASIC)
        };
    }

    private static Task<(Task,ResponseType)> OwnerCommandHandler(SocketSlashCommand command, Bot bot){
        //owner commands are only registered in owner server
        //allows admin in owner server to manage bot
        if(!((SocketGuildUser)command.User).GuildPermissions.Administrator){
            return Task.FromResult((DI.ModifyResponseAsync("You are not an admin",command),ResponseType.BASIC));
        }

        return command.Data.Name switch
        {
            "repopulate" => Task.FromResult((BotBase.RepopulateTaskAsync(command,bot),ResponseType.BASIC)),

            "console" => Task.FromResult((DI.ModifyResponseAsync("WIP",command),ResponseType.BASIC)),

            "addcredit" => Task.FromResult((Task.Run(async () => {
                var amountFound = ulong.TryParse(command.Data.Options.First().Value.ToString(), out var amount);
                if(!amountFound) {
                    await DI.ModifyResponseAsync("Amount Not Found",command);
                    return;
                }

                var guildIdFound = ulong.TryParse(command.Data.Options.ElementAt(1).Value.ToString(), out var guildId);

                if(!guildIdFound) {
                    await DI.ModifyResponseAsync("GuildId Not Found",command);
                    return;
                }

                await CreditTableManager.IncrementCredit($"{guildId}", amount, "Manual Increment");

                await DI.ModifyResponseAsync($"Added {amount} to {guildId}",command);
            }),ResponseType.BASIC)),

            "removecredit" => Task.FromResult((Task.Run(async () => {
                var amountFound = ulong.TryParse(command.Data.Options.First().Value.ToString(), out var amount);
                if(!amountFound) {
                    await DI.ModifyResponseAsync("Amount Not Found",command);
                    return;
                }

                var guildIdFound = ulong.TryParse(command.Data.Options.ElementAt(1).Value.ToString(), out var guildId);

                if(!guildIdFound) {
                    await DI.ModifyResponseAsync("GuildId Not Found",command);
                    return;
                }

                await CreditTableManager.DecrementCredit($"{guildId}", amount, "Manual Decrement");

                await DI.ModifyResponseAsync($"Removed {amount} from {guildId}",command);
            }),ResponseType.BASIC)),

            "checkcredit" => Task.FromResult((Task.Run(async () => {
                var guildIdFound = ulong.TryParse(command.Data.Options.First().Value.ToString(), out var guildId);
                if(!guildIdFound) {
                    await DI.ModifyResponseAsync("Amount Not Found",command);
                    return;
                }

                await DI.ModifyResponseAsync($"{guildId} has '{Math.Round(await CreditTableManager.GetCredit($"{guildId}"),3)}' credit",command);
            }),ResponseType.BASIC)),

            _ => Task.FromResult((DI.ModifyResponseAsync($"Caught {command.Data.Name} by owner handler but found no command",command),ResponseType.BASIC))
        };
    }

    private async static Task<(Task,ResponseType)> StartCommandHandler(SocketSlashCommand command, Bot bot){
        return await GetGame(command) switch
        {
            Game.SCP => (IServerWorker.StartTaskAsync(Bot.scpIPairs,command),ResponseType.BASIC),
            
            Game.MINECRAFT => (DI.ModifyResponseAsync("Minecraft not available",command),ResponseType.BASIC),

            _ => (DI.ModifyResponseAsync($"Caught {command.Data.Options.First().Value} by start command handler but found no game",command),ResponseType.BASIC)
        };
    }

    private async static Task<(Task,ResponseType)> StopCommandHandler(SocketSlashCommand command, Bot bot){
        return await GetGame(command) switch
        {
            Game.SCP => (IServerWorker.StopTaskAsync(Bot.scpIPairs,command),ResponseType.BASIC),
            
            Game.MINECRAFT => (DI.ModifyResponseAsync("Minecraft not available",command),ResponseType.BASIC),

            _ => (DI.ModifyResponseAsync($"Caught {command.Data.Options.First().Value} by stop command handler but found no game",command),ResponseType.BASIC)
        };
    }

    private async static Task<(Task,ResponseType)> ConfigureCommandHandler(SocketSlashCommand command){
        return await GetGame(command) switch{

            //create configuration prompt to change all settings associated with SCPInterface
            Game.SCP => (DI.SendComponentsAsync(await SCPComponentBuilder.GetConfigurationComponents(command.GuildId),command),ResponseType.COMPONENT),

            Game.MINECRAFT => (DI.ModifyResponseAsync("Minecraft not available",command),ResponseType.BASIC),

            _ => (DI.ModifyResponseAsync($"Caught {command.Data.Options.First().Value} by stop command handler but found no game",command),ResponseType.BASIC)
        
        };
    }

    private static Task<Game> GetGame(SocketSlashCommand command) =>
        Task.FromResult((Game)Convert.ToInt32(command.Data.Options.First().Value));

    public enum ResponseType{
        BASIC,
        COMPONENT,
        MODAL
    }
}
