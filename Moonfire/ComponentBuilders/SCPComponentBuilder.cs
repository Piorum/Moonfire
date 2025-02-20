using Moonfire.ConfigHandlers;
using Moonfire.Types.Discord;

namespace Moonfire.ComponentBuilders;

public static class SCPComponentBuilder
{
    public static async Task<MoonfireComponent> GetConfigurationComponents(ulong? guildId, CancellationToken token = default){
        var gameSettings = await SCPConfigHandler.GetGameSettings(guildId,token);

        var standardPrice = await AzureVM.VmSizeToPrice(new(2,4));
        var standardPlusPrice = await AzureVM.VmSizeToPrice(new(2,8));
        var premiumPrice = await AzureVM.VmSizeToPrice(new(4,8));
        
        return 
            new MoonfireComponent
            (
                selectMenus:
                [
                    new(
                        customId:"scp_branch_menu",
                        placeholder:"Server Branch",
                        options:
                        [
                            new(
                                label:"Public",
                                value:"public",
                                description:"Main Public Branch - Default"
                            )
                        ]
                    ),
                    new(
                        customId:"scp_serversize_menu",
                        placeholder:"Server Size",
                        options:
                        [
                            new(
                                label:"Standard (1-20 Players)",
                                value:"0",
                                description:$"2x vCPU, 4GiB RAM - {standardPrice}c/hr - Default"
                            ),
                            new(
                                label:"Standard+ (21+ Players)",
                                value:"1",
                                description:$"2x vCPU, 8GiB RAM - {standardPlusPrice}c/hr"
                            ),
                            new(
                                label:"Premium (21+ Players)",
                                value:"2",
                                description:$"4x vCPU, 8GiB RAM - {premiumPrice}c/hr"
                            )
                        ]
                    )
                ],
                buttons:
                [
                    new(
                        label:"Assign Role",
                        customId:"scp_addadmin_button"
                    ),
                    new(
                        label:"Remove Role",
                        customId:"scp_removeadmin_button",
                        style:ButtonStyle.Danger,
                        disabled:gameSettings.AdminUsers is []
                    )
                ]
            );
    }

    public static Task<MoonfireComponent> GetAssignRoleMenuComponents() =>
        Task.FromResult(
            new MoonfireComponent
            (
                selectMenus:
                [
                    new(
                        customId:"scp_addadmin_role",
                        placeholder:$"Select Role",
                        options:
                        [
                            new(
                                label:"Owner",
                                value:$"owner"
                            ),
                            new(
                                label:"Admin",
                                value:$"admin"),
                            new(
                                label:"Moderator",
                                value:$"moderator"),
                        ]
                    )
                ]
            )
        );

    public static async Task<MoonfireComponent> GetRemoveRoleMenuComponents(ulong? guildId, CancellationToken token = default){
        var gameSettings = await SCPConfigHandler.GetGameSettings(guildId,token);

        List<MoonfireSelectMenuComponentOption> selectMenuOptions = [];

        foreach(var adminUser in gameSettings.AdminUsers){
            string id = adminUser.Id.ToString();
            selectMenuOptions.Add(
                new(
                    label:$"{id} - {adminUser.Role}",
                    value:id
                )
            );
        }

        return 
            new MoonfireComponent(
                selectMenus:[
                    new(
                        customId:"scp_removeadmin_role",
                        options:selectMenuOptions,
                        placeholder:"Select User"
                    )
                ]
            );
            
    }
}
