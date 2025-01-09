using Moonfire.Interfaces;
using Moonfire.Workers;
using Moonfire.Types.Discord;
using Moonfire.ComponentBuilders;

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

            _ => Task.FromResult((DI.SendSlashReplyAsync("Failed to find command in commands list",command),ResponseType.BASIC))
        };
    }

    private static Task<(Task,ResponseType)> UserCommandHandler(SocketSlashCommand command, Bot bot){
        return command.Data.Name switch
        {
            BotBase.helpCmd => Task.FromResult((BotBase.PrintHelpTaskAsync(command, bot),ResponseType.BASIC)),

            _ => Task.FromResult((DI.SendSlashReplyAsync($"Caught {command.Data.Name} by user handler but found no command", command),ResponseType.BASIC))
        };
    }



    private static Task<(Task,ResponseType)> AdminCommandHandler(SocketSlashCommand command, Bot bot){
        if(!((SocketGuildUser)command.User).GuildPermissions.Administrator){
            return Task.FromResult((DI.SendSlashReplyAsync("You are not an admin",command),ResponseType.BASIC));
        }

        return command.Data.Name switch
        {
            "start" => StartCommandHandler(command,bot),

            "stop" => StopCommandHandler(command,bot),

            "configure" => ConfigureCommandHandler(command),

            _ => Task.FromResult((DI.SendSlashReplyAsync($"Caught {command.Data.Name} by admin handler but found no command",command),ResponseType.BASIC))
        };
    }

    private static Task<(Task,ResponseType)> OwnerCommandHandler(SocketSlashCommand command, Bot bot){
        //owner commands are only registered in owner server
        //allows admin in owner server to manage bot
        if(!((SocketGuildUser)command.User).GuildPermissions.Administrator){
            return Task.FromResult((DI.SendSlashReplyAsync("You are not an admin",command),ResponseType.BASIC));
        }

        return command.Data.Name switch
        {
            "repopulate" => Task.FromResult((BotBase.RepopulateTaskAsync(command,bot),ResponseType.BASIC)),

            "console" => Task.FromResult((DI.SendSlashReplyAsync("WIP",command),ResponseType.BASIC)),

            "modaltest" => Task.FromResult((DI.SendModalAsync(new("test","modaltest",[new("TestLabel","texttest")]),command),ResponseType.MODAL)),

            "componenttest" => Task.FromResult((DI.SendComponentsAsync(new(),command),ResponseType.COMPONENT)),

            _ => Task.FromResult((DI.SendSlashReplyAsync($"Caught {command.Data.Name} by owner handler but found no command",command),ResponseType.BASIC))
        };
    }

    private async static Task<(Task,ResponseType)> StartCommandHandler(SocketSlashCommand command, Bot bot){
        return await GetGame(command) switch
        {
            Game.SCP => (IServerWorker.StartTaskAsync(bot.scpIPairs,command),ResponseType.BASIC),
            
            Game.MINECRAFT => (DI.SendSlashReplyAsync("Minecraft not available",command),ResponseType.BASIC),

            _ => (DI.SendSlashReplyAsync($"Caught {command.Data.Options.First().Value} by start command handler but found no game",command),ResponseType.BASIC)
        };
    }

    private async static Task<(Task,ResponseType)> StopCommandHandler(SocketSlashCommand command, Bot bot){
        return await GetGame(command) switch
        {
            Game.SCP => (IServerWorker.StopTaskAsync(bot.scpIPairs,command),ResponseType.BASIC),
            
            Game.MINECRAFT => (DI.SendSlashReplyAsync("Minecraft not available",command),ResponseType.BASIC),

            _ => (DI.SendSlashReplyAsync($"Caught {command.Data.Options.First().Value} by stop command handler but found no game",command),ResponseType.BASIC)
        };
    }

    private async static Task<(Task,ResponseType)> ConfigureCommandHandler(SocketSlashCommand command){
        return await GetGame(command) switch{

            //create configuration prompt to change all settings associated with SCPInterface
            Game.SCP => (DI.SendComponentsAsync(await SCPComponentBuilder.GetConfigurationComponents(command.GuildId),command),ResponseType.COMPONENT),

            Game.MINECRAFT => (DI.SendSlashReplyAsync("Minecraft not available",command),ResponseType.BASIC),

            _ => (DI.SendSlashReplyAsync($"Caught {command.Data.Options.First().Value} by stop command handler but found no game",command),ResponseType.BASIC)
        
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
