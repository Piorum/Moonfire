using System;

namespace Moonfire.Sorters;

public class ModalSorter
{
    public static Task<Task> GetTask(SocketModal modal){
        return Task.FromResult(modal.Data.CustomId switch{
            "food_menu" => modal.RespondAsync("test",ephemeral:true),

            _ => modal.RespondAsync($"Caught {modal.Data.CustomId} by modal handler but found no case",ephemeral:true)
        });
    }
}
