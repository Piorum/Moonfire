using Moonfire.Interfaces;
using Moonfire.ComponentBuilders;
using Moonfire.ModalBuilders;
using Moonfire.ConfigHandlers;
using AzureAllocator.Types;
using Moonfire.Credit;

namespace Moonfire.Sorters;

public class ComponentSorter
{
    public static async Task<Task> GetTask(SocketMessageComponent component, Bot bot){
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

            //sets branch to menu option selected
            "scp_serversize_menu" => SCPServerSizeMenuTask(component),

            //sets preferred region
            "bot_region_menu" => BOTRegionMenuTask(component),

            //consume buttons
            //5 Credits
            "consume_1339100750487355446" => Task.Run(async() => {
                var consumed = await bot.ConsumeEntitlement(component.User.Id, 1339100750487355446);
                if(consumed){
                    await CreditTableManager.IncrementCreditEntitlement($"{component.GuildId}", 5.0);
                    await DI.SendResponseAsync("Applied 5 Credits", component);
                } else {
                    await DI.SendResponseAsync("Found No Consumable", component);
                }
            }),

            //10 Credits
            "consume_1345145487824785490" => Task.Run(async() => {
                var consumed = await bot.ConsumeEntitlement(component.User.Id, 1345145487824785490);
                if(consumed){
                    await CreditTableManager.IncrementCreditEntitlement($"{component.GuildId}", 10.0);
                    await DI.SendResponseAsync("Applied 10 Credits", component);
                } else {
                    await DI.SendResponseAsync("Found No Consumable", component);
                }
            }),

            //20 Credits
            "consume_1345145596549529763" => Task.Run(async() => {
                var consumed = await bot.ConsumeEntitlement(component.User.Id, 1345145596549529763);
                if(consumed){
                    await CreditTableManager.IncrementCreditEntitlement($"{component.GuildId}", 20.0);
                    await DI.SendResponseAsync("Applied 20 Credits", component);
                } else {
                    await DI.SendResponseAsync("Found No Consumable", component);
                }
            }),
            
            _ => DI.SendResponseAsync($"Caught {component.Data.CustomId} by component handler but found no case",component)
        };
    }

    private static async Task SCPBranchMenuTask(SocketMessageComponent component){
        var branch = component.Data.Values.FirstOrDefault();

        var valid = await SCPConfigHandler.SetBranch(component.GuildId,branch??"");

        if(!valid){
            await DI.SendResponseAsync("Broken Menu",component);
            return;
        }

        await DI.GenericConfigUpdateResponse($"Updated Branch To {branch?[0].ToString().ToUpper() + branch?[1..]}","SCP",component);
    }

    private static async Task SCPServerSizeMenuTask(SocketMessageComponent component){
        var selectionIndex = component.Data.Values.FirstOrDefault();

        if(selectionIndex is null){
            await DI.SendResponseAsync($"Broken Menu. Failed To Change.","SCP",component);
            return;
        }

        //Should match values in SCPComponentBuilder config menu server size selection list
        InternalVmSize? internalVMSize = selectionIndex switch {
            "0" => new(2,4),
            "1" => new(2,8),
            "2" => new(4,8),
            _ => null
        };

        if(internalVMSize is null){
            await DI.SendResponseAsync($"Broken Menu. Failed To Change.","SCP",component);
            return;
        }

        await SCPConfigHandler.SetServerSize(component.GuildId,internalVMSize);

        await DI.GenericConfigUpdateResponse($"Updated Server Size","SCP",component);
    }

    private static async Task SCPRemoveAdminTask(SocketMessageComponent component){
        var steamIdString = component.Data.Values.FirstOrDefault();

        var valid = ulong.TryParse(steamIdString,out var steamId);

        if(!valid){
            await DI.SendResponseAsync("Broken Config",component);
            return;
        }

        await SCPConfigHandler.RemoveRole(component.GuildId,steamId);

        await DI.GenericConfigUpdateResponse($"Removed role from {steamIdString}.","SCP",component);
    }

    private static async Task BOTRegionMenuTask(SocketMessageComponent component){

        string region = component.Data.Values.FirstOrDefault() ?? "NA";
        var guildId = component.GuildId;

        if(guildId is not null){
            if(await Bot.ServerRunning((ulong)guildId)){
                await DI.SendResponseAsync($"Servers must be off to change region.","BOT",component);
            }
        }

        var globalSettings = await GLOBALConfigHandler.GetSettings(guildId);
        
        globalSettings.region = region;

        await GLOBALConfigHandler.SetSettings(guildId, globalSettings);

        await Console.Out.WriteLineAsync($"guildId:{guildId}:region:{region}");
        await DI.SendResponseAsync($"Region Set To {region}","BOT",component);
    }
}
