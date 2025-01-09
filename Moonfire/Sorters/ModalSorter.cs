using Moonfire.ConfigHandlers;
using Moonfire.Types.Json;
using Moonfire.Interfaces;
using Moonfire.ComponentBuilders;

namespace Moonfire.Sorters;

public class ModalSorter
{
    public static Task<Task> GetTask(SocketModal modal){
        return modal.Data.CustomId switch{

            "scp_addadmin_modal" => Task.FromResult(SCPAddAdminTask(modal)),

            _ => Task.FromResult(DI.SendModalResponseAsync($"Caught {modal.Data.CustomId} by modal handler but found no case",modal))
        };
    }

    private static async Task SCPAddAdminTask(SocketModal modal){
        var component = modal.Data.Components.ToList().FirstOrDefault();

        string role = component?.CustomId ?? "";
        string steamIdString = component?.Value ?? "";

        var valid = ulong.TryParse(steamIdString,out var steamId);

        if(!valid){
            await DI.SendModalResponseAsync($"Invalid Id","SCP Assign Role",modal);
            return;
        }

        await SCPConfigHandler.AssignRole(modal.GuildId,steamId,role);

        await DI.SendModalResponseAsync($"Added {steamIdString} as {role}","SCP Assign Role",modal);
    }
}
