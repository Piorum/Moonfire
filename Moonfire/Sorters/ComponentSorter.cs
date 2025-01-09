using Moonfire.Interfaces;
using Moonfire.ComponentBuilders;
using Moonfire.ModalBuilders;
using Moonfire.ConfigHandlers;

namespace Moonfire.Sorters;

public class ComponentSorter
{
    public static async Task<Task> GetTask(SocketMessageComponent component){
        return component.Data.CustomId switch{
            //creates select menu to select role to assign
            "scp_addadmin_button" => DI.SendComponentsAsync(await SCPComponentBuilder.GetAssignRoleMenuComponents(),component),

            //creates select menu to select user to remove from adminusers
            "scp_removeadmin_button" => DI.SendComponentsAsync(await SCPComponentBuilder.GetRemoveRoleMenuComponents(component.GuildId),component),

            //data value is picked role, creates modal to get steam id
            "scp_addadmin_role" => DI.SendModalAsync(await SCPModalBuilder.GetSteamIdModal(component.Data.Values.FirstOrDefault()??""),component),

            //removes user selected by select menu from adminusers
            "scp_removeadmin_role" => SCPRemoveAdminTask(component),

            //sets branch to menu option selected
            "scp_branch_menu" => SCPBranchMenuTask(component),
            
            _ => DI.SendComponentResponseAsync($"Caught {component.Data.CustomId} by component handler but found no case",component)
        };
    }

    private static async Task SCPBranchMenuTask(SocketMessageComponent component){
        var branch = component.Data.Values.FirstOrDefault();

        var valid = await SCPConfigHandler.SetBranch(component.GuildId,branch??"");

        if(!valid){
            await DI.SendComponentResponseAsync("Broken Menu",component);
            return;
        }

        await DI.SendComponentResponseAsync($"Set branch to {branch}","SCP Set Branch",component);
    }

    private static async Task SCPRemoveAdminTask(SocketMessageComponent component){
        var steamIdString = component.Data.Values.FirstOrDefault();

        var valid = ulong.TryParse(steamIdString,out var steamId);

        if(!valid){
            await DI.SendComponentResponseAsync("Broken Config",component);
            return;
        }

        await SCPConfigHandler.RemoveRole(component.GuildId,steamId);

        await DI.SendComponentResponseAsync($"Removed role from {steamIdString}","SCP Remove Role",component);
    }
}
