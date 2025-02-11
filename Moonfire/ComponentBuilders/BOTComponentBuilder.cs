using Moonfire.Types.Discord;

namespace Moonfire.ComponentBuilders;

public static class BOTComponentBuilder
{
    public static Task<MoonfireComponent> GetRegionSelectionComponents(CancellationToken token = default) =>
        Task.FromResult
        (
            new MoonfireComponent
            (
                selectMenus:
                [
                    new(
                        customId:"bot_region_menu",
                        placeholder:"Server Region",
                        options:
                        [
                            new(
                                label:"North America",
                                value:"NA"
                            ),
                            new(
                                label:"Europe",
                                value:"EU"
                            ),
                            new(
                                label:"Asia",
                                value:"AS"
                            ),
                            new(
                                label:"Oceania",
                                value:"OC"
                            ),
                            new(
                                label:"South America",
                                value:"SA"
                            ),
                            new(
                                label:"Africa",
                                value:"AF"
                            )
                        ]
                    )
                ]
            )
        );
}
