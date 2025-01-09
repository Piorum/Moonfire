using Moonfire.ConfigHandlers;
using Moonfire.Types.Discord;

namespace Moonfire.ComponentBuilders;

public static class SCPComponentBuilder
{
    public static async Task<MoonfireComponent> GetConfigurationComponents(ulong? guildId, CancellationToken token = default){
        var gameSettings = await SCPConfigHandler.GetGameSettings(guildId,token);
        
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
                    )
                ],
                buttons:[
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

    public static Task<MoonfireComponent> GetAssignRoleMenuComponents(){
        return 
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
    }

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
