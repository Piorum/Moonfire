namespace Moonfire.Sorters;

public class ComponentSorter
{
    public static Task<Task> GetTask(SocketMessageComponent component){
        return component.Data.CustomId switch{
            "testmenu" => testmenuHandler(component),
            
            _ => Task.FromResult(component.RespondAsync($"Caught {component.Data.CustomId} by component handler but found no case",ephemeral:true))
        };
    }

    public static Task<Task> testmenuHandler(SocketMessageComponent component){
        return component.Data.Values.FirstOrDefault() switch {
            "opt1" => Task.FromResult(component.RespondAsync("opt1",ephemeral:true)),
            "opt2" => Task.FromResult(component.RespondAsync("opt2",ephemeral:true)),
            _ => Task.FromResult(component.RespondAsync($"Caught {component.Data.Value} by testmenu component handler but found no case",ephemeral:true))
        };
    }
}
