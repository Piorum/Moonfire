using Moonfire.Interfaces;
using Moonfire.Workers;

namespace Moonfire;

public static class CommandSorter
{
    public static Task GetTask(SocketSlashCommand command, Bot bot) =>
        //finds first command that matches name of passed command
        //if a command was found gets the rank of that command
        //gets proper task to run as background task
        bot.commands.FirstOrDefault(p => p.Name == command.Data.Name)?.Rank switch
        {
            Rank.User => UserCommandHandler(command, bot),

            Rank.Admin => AdminCommandHandler(command, bot),

            Rank.Owner => OwnerCommandHandler(command, bot),

            _ => DI.SendSlashReplyAsync("Failed to find command in commands list",command)
        };

    private static Task UserCommandHandler(SocketSlashCommand command, Bot bot) =>
        command.Data.Name switch
        {
            BotBase.helpCmd => BotBase.PrintHelpTaskAsync(command, bot),

            _ => DI.SendSlashReplyAsync($"Caught {command.Data.Name} by user handler but found no command", command),
        };



    private static Task AdminCommandHandler(SocketSlashCommand command, Bot bot){
        if(!((SocketGuildUser)command.User).GuildPermissions.Administrator){
            return DI.SendSlashReplyAsync("You are not an admin",command);
        }

        return command.Data.Name switch
        {
            "start" => StartCommandHandler(command,bot),

            "stop" => StopCommandHandler(command,bot),

            _ => DI.SendSlashReplyAsync($"Caught {command.Data.Name} by admin handler but found no command",command),
        };
    }

    private static Task OwnerCommandHandler(SocketSlashCommand command, Bot bot){
        //owner commands are only registered in owner server
        //allows admin in owner server to manage bot
        if(!((SocketGuildUser)command.User).GuildPermissions.Administrator){
            return DI.SendSlashReplyAsync("You are not an admin",command);
        }

        return command.Data.Name switch
        {
            "repopulate" => BotBase.RepopulateTaskAsync(command,bot),

            "console" => DI.SendSlashReplyAsync("WIP",command),

            _ => DI.SendSlashReplyAsync($"Caught {command.Data.Name} by owner handler but found no command",command),
        };
    }

    private static Task StartCommandHandler(SocketSlashCommand command, Bot bot) =>
        (Game)Convert.ToInt32(command.Data.Options.First().Value) switch
        {

            Game.SCP => IServerWorker.StartTaskAsync(bot.scpIPairs,command),
            
            Game.MINECRAFT => DI.SendSlashReplyAsync("Minecraft not available",command),

            _ => DI.SendSlashReplyAsync($"Caught {command.Data.Options.First().Value} by start command handler but found no game",command),
        };

    private static Task StopCommandHandler(SocketSlashCommand command, Bot bot) =>
        (Game)Convert.ToInt32(command.Data.Options.First().Value) switch
        {
            
            Game.SCP => IServerWorker.StopTaskAsync(bot.scpIPairs,command),
            
            Game.MINECRAFT => DI.SendSlashReplyAsync("Minecraft not available",command),

            _ => DI.SendSlashReplyAsync($"Caught {command.Data.Options.First().Value} by stop command handler but found no game",command),
        };
}
