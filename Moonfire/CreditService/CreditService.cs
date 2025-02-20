using System.Collections.Concurrent;

namespace Moonfire.CreditService;

public class CreditService
{
    private static bool Actioning = false;
    private static readonly ConcurrentDictionary<string, CreditClient> creditClients = [];
    private static readonly ConcurrentDictionary<string, NewCreditClient> newCreditClients = [];

    public static async Task RegisterClient(ulong guildId, Game game, double hourlyCost, string reason, CancellationToken token = default){
        await AwaitActioning(token);

        var accountKey = await GenerateAccountKey(guildId, game);

        NewCreditClient newClient = new(DateTime.Now, hourlyCost, reason);

        newCreditClients.TryAdd(accountKey, newClient);

        throw new NotImplementedException();
    }

    public static async Task UnregisterClient(ulong guildId, Game game, CancellationToken token = default){
        await AwaitActioning(token);

        var accountKey = await GenerateAccountKey(guildId, game);

        await UnregisterClient(accountKey, token:token);

        throw new NotImplementedException();
    }

    public static async Task UnregisterClient(string accountKey, CancellationToken token = default){
        await ActionSingularClient(accountKey, token:token); //action end of credit cycle

        creditClients.TryRemove(accountKey, out _); //remove from clients list

        throw new NotImplementedException();
    }

    public static async Task ActionCredit(CancellationToken token = default){

        if(Actioning) {
            return;
        }
        Actioning = true;

        var lastActionTime = await GetLastActionTime();

        var currentTime = DateTime.Now;

        //used to enforce pausing
        if(lastActionTime > currentTime){
            return;
        }

        var percentOfHourSincePreviousAction = (currentTime - lastActionTime).TotalMinutes / 60.0;

        await SetLastActionTime(currentTime);

        List<Task<(string, double)>> actionTasks = [];

        foreach(var item in creditClients){
            var accountId = item.Key;
            var client = item.Value;

            var cost = client.HourlyCost * percentOfHourSincePreviousAction;

            var decrementTask = CreditTableManager.DecrementCredit(accountId, cost, client.Reason);
            actionTasks.Add(decrementTask);
        }

        foreach(var item in newCreditClients){
            var accountId = item.Key;
            var client = item.Value;
            
            var percentOfHourSinceCreation = (currentTime - client.CreationTime).TotalMinutes / 60.0;

            var cost  = client.HourlyCost * percentOfHourSinceCreation;

            var decrementTask = Task.Run(async () => {
                token.ThrowIfCancellationRequested();
                return await CreditTableManager.DecrementCredit(accountId, cost, client.Reason);
            }, cancellationToken:token);

            actionTasks.Add(decrementTask);

            creditClients.TryAdd(accountId, new(client.HourlyCost, client.Reason));
            newCreditClients.TryRemove(accountId, out _);
        }

        var actionResults = await Task.WhenAll(actionTasks);

        foreach(var result in actionResults){
            (var accountKey, var newAccountBalance) = result;

            if(newAccountBalance < 0){
                //invoke OutOfBalance event passing accountId
            }
        }

        Actioning = false;

        throw new NotImplementedException();
    }

    public static async Task PauseCredit(TimeSpan pauseLength, CancellationToken token = default){

        if(!Actioning){
            await ActionCredit(token:token);
        }

        await AwaitActioning(token);

        var newLastActionTime = await GetLastActionTime() + pauseLength;

        //load last time credit was actioned

        //set last time credit was actioned to newLastActionTime

        throw new NotImplementedException();


    }

    private static async Task ActionSingularClient(string accountKey, CancellationToken token = default){
        if(Actioning){
            await AwaitActioning(token);
            return;
        }

        //find client, new or normal
        //calculate percent of hour since either creation date or last action date
        //decrement by cost

        //if new move to normal client list

        throw new NotImplementedException();
    }

    private static async Task AwaitActioning(CancellationToken token = default){
        while(Actioning){
            token.ThrowIfCancellationRequested();
            await Task.Delay(1000, token);
        }
    }

    private static Task<string> GenerateAccountKey(ulong guid, Game game) => Task.FromResult(
        $"{guid}{game switch {
        Game.NONE => "NONE",
        Game.MINECRAFT => "MC",
        Game.SCP => "SCP",
        _ => "UNIMPLEMENTED"
    }}");

    private static async Task<DateTime> GetLastActionTime(){
        //lastActionTime
        //get table value

        //return lastActionTime

        throw new NotImplementedException();
    }

    private static async Task SetLastActionTime(DateTime dateTime){

        //set table value

        throw new NotImplementedException();
    }

    private class CreditClient(double hourlyCost, string reason){
        public double HourlyCost = hourlyCost;
        public string Reason = reason;
    }

    private class NewCreditClient(DateTime creationTime, double hourlyCost, string reason) : CreditClient(hourlyCost, reason) {
        public DateTime CreationTime = creationTime;

    }

}
