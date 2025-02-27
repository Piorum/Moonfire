using System.Collections.Concurrent;

namespace Moonfire.Credit;

public class CreditService
{
    public static event EventHandler<OutOfBalanceException>? OutOfBalanceAlert;

    private static bool Actioning = false;
    private static readonly ConcurrentDictionary<string, CreditClient> creditClients = [];
    private static readonly ConcurrentDictionary<string, NewCreditClient> newCreditClients = [];

    public static async Task<bool> OutOfBalance(ulong? guildId) =>
        await CreditTableManager.GetCredit($"{guildId}") <= 0;

    public static async Task RegisterClient(ulong guildId, Game game, double hourlyCost, string reason, CancellationToken token = default){
        await AwaitActioning(token);

        var accountKey = await GenerateAccountKey(guildId, game);

        await Console.Out.WriteLineAsync($"Registered Credit Client:accountKey:{accountKey}:hourlyCost:{hourlyCost}");

        NewCreditClient newClient = new(DateTime.UtcNow, hourlyCost, reason);

        newCreditClients.TryAdd(accountKey, newClient);
    }

    public static async Task UnregisterClient(ulong guildId, Game game, CancellationToken token = default){
        await AwaitActioning(token);

        var accountKey = await GenerateAccountKey(guildId, game);

        await Console.Out.WriteLineAsync($"Unregistered Credit Client:accountKey:{accountKey}");

        await UnregisterClient(accountKey, token:token);
    }

    public static async Task UnregisterClient(string accountKey, CancellationToken token = default){
        await ActionSingularClient(accountKey, token:token); //action end of credit cycle

        creditClients.TryRemove(accountKey, out _); //remove from clients list
    }

    public static async Task ActionCredit(CancellationToken token = default){

        if(Actioning) {
            return;
        }
        Actioning = true;

        var lastActionTime = await CreditTableManager.GetLastActionTime();

        var currentTime = DateTime.UtcNow;

        //used to enforce pausing
        if(lastActionTime > currentTime){
            return;
        }

        var percentOfHourSincePreviousAction = (currentTime - lastActionTime).TotalMinutes / 60.0;

        await CreditTableManager.SetLastActionTime(currentTime);

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

        //where Item2(newAccountBalance) <= 0 select accountKey to list
        var outOfBalanceAccounts = actionResults.Where(tuple => tuple.Item2 <= 0).Select(tuple => tuple.Item1).ToList();
            

        foreach(var accountKey in outOfBalanceAccounts){
            _ = Task.Run(() => SendOutOfBalanceAlert(accountKey), cancellationToken:token);
        }

        Actioning = false;
    }

    public static async Task PauseCredit(TimeSpan pauseLength, CancellationToken token = default){
        if(!Actioning){
            await ActionCredit(token:token);
        }

        await AwaitActioning(token);

        var lastActionTime = await CreditTableManager.GetLastActionTime();

        await CreditTableManager.SetLastActionTime(lastActionTime + pauseLength);
    }

    private static async Task ActionSingularClient(string accountKey, CancellationToken token = default){
        await AwaitActioning(token);

        var clientFound = creditClients.TryGetValue(accountKey, out var existingClient);
        NewCreditClient? newClient = null;
        if(!clientFound){
            clientFound = newCreditClients.TryGetValue(accountKey, out newClient);
            if(!clientFound){
                await Console.Out.WriteLineAsync($"{nameof(ActionSingularClient)}:Not Client Found");
                return;
            }
        }

        DateTime currentTime = DateTime.UtcNow;

        if(existingClient is not null){
            var lastActionTime = await CreditTableManager.GetLastActionTime();
            var percentOfHourSincePreviousAction = (currentTime - lastActionTime).TotalMinutes / 60.0;
            
            var cost = existingClient.HourlyCost * percentOfHourSincePreviousAction;

            await CreditTableManager.DecrementCredit(accountKey, cost, existingClient.Reason);
        }

        if(newClient is not null){
            var creationTime = newClient.CreationTime;
            var percentOfHourSincePreviousAction = (currentTime - creationTime).TotalMinutes / 60.0;
            
            var cost = newClient.HourlyCost * percentOfHourSincePreviousAction;

            await CreditTableManager.DecrementCredit(accountKey, cost, newClient.Reason);
            
            creditClients.TryAdd(accountKey, new(newClient.HourlyCost, newClient.Reason));
            newCreditClients.TryRemove(accountKey, out _);
        }
    }

    private static async Task AwaitActioning(CancellationToken token = default){
        while(Actioning){
            token.ThrowIfCancellationRequested();
            await Task.Delay(1000, token);
        }
    }

    private static Task<string> GenerateAccountKey(ulong guid, Game game) => Task.FromResult(
        $"{guid}:{game switch {
        Game.NONE => "NONE",
        Game.MINECRAFT => "MC",
        Game.SCP => "SCP",
        _ => "UNIMPLEMENTED"
    }}");

    private static void SendOutOfBalanceAlert(string accountKey){
        OutOfBalanceAlert?.Invoke(typeof(CreditService), new OutOfBalanceException(accountKey));
    }

    public class OutOfBalanceException : Exception
    {
        private OutOfBalanceException() { }

        public OutOfBalanceException(string accountKey) 
            : base(accountKey) { }

        public OutOfBalanceException(string accountKey, Exception innerException) 
            : base(accountKey, innerException) { }
    }


    private class CreditClient(double hourlyCost, string reason){
        public double HourlyCost = hourlyCost;
        public string Reason = reason;
    }

    private class NewCreditClient(DateTime creationTime, double hourlyCost, string reason) : CreditClient(hourlyCost, reason) {
        public DateTime CreationTime = creationTime;
    }

}
