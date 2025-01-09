using System.Globalization;
using Moonfire.Types.Discord;

namespace Moonfire.ModalBuilders;

public static class SCPModalBuilder
{
    public static Task<MoonfireModal> GetSteamIdModal(string role){
        return 
            Task.FromResult(
                new MoonfireModal
                (
                    title:$"Assign {char.ToUpper(role[0])+role[1..]}",
                    customId:"scp_addadmin_modal",
                    textInputs:
                    [
                        new(
                            label:"Public SteamID",
                            customId:role,
                            placeholder:"76561198123456789",
                            minLength:17,
                            maxLength:17,
                            required:true
                        )
                    ]
                )
            );
    }
}
