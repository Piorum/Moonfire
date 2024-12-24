namespace GameUpdater;

public class Program{
    
    //entry point
    public static async Task Main(){
        var ver = nameof(Sunfire);

        var i = 0;
        foreach(var game in Enum.GetValues(typeof(Options))){
            await Console.Out.WriteLineAsync($"{i} - {game}");
            i++;
        }

        //build and deploy application
        try{
            await ((Options)int.Parse(await Console.In.ReadLineAsync() ?? "0") switch{

                Options.SCP => UpdateHelper.UpdateSCP(),

                Options.MINECRAFT => UpdateHelper.UpdateMINECRAFT(),

                Options.GMOD => UpdateHelper.UpdateGMOD(),

                Options.MAINTENANCE_LOCK => UpdateHelper.MaintenanceLock(ver),

                _ => Console.Out.WriteLineAsync("Invalid Option")

            });
        }catch(Exception e){
            await Console.Out.WriteLineAsync($"{e}");
        }
    }

    private enum Options{
            NONE,
            SCP,
            MINECRAFT,
            GMOD,
            MAINTENANCE_LOCK
    }
}
