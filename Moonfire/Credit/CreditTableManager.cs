using AzureAllocator.Managers;
using Moonfire.TableEntities;

namespace Moonfire.Credit;

public static class CreditTableManager
{
    public static async Task<(string, double)> DecrementCredit(string accountKey, double amount, string reason){
        await LogValueChange(accountKey, amount, reason, CreditAction.DECREMENT);

        //perform decrement
        var creditEntity = await GetCreditEntity(accountKey);

        creditEntity.Credit -= amount;

        await StoreCreditEntity(creditEntity);

        return (accountKey, creditEntity.Credit);
    }

    public static async Task<(string, double)> IncrementCredit(string accountKey, double amount, string reason){
        await LogValueChange(accountKey, amount, reason, CreditAction.INCREMENT);

        //perform increment
        var creditEntity = await GetCreditEntity(accountKey);

        creditEntity.Credit += amount;

        await StoreCreditEntity(creditEntity);

        return (accountKey, creditEntity.Credit);
    }

    public static async Task IncrementCreditEntitlement(string accountKey, double amount){
        await LogValueChange(accountKey, amount, "Entitlement", CreditAction.INCREMENT);

        //perform increment
        var creditEntity = await GetCreditEntity(accountKey);

        creditEntity.Credit += amount;

        await StoreCreditEntity(creditEntity);
    }

    public static async Task<double> GetCredit(string accountKey){
        var creditEntity = await GetCreditEntity(accountKey);
        return creditEntity.Credit;
    }

    private static async Task<CreditEntity> GetCreditEntity(string accountKey){
        if(!accountKey.Contains(':')) accountKey = $"{accountKey}:";
        var accountKeyBase = accountKey[0..accountKey.IndexOf(':')];
        return await TableManager.GetITableEntity<CreditEntity>("MoonfireCredit", accountKeyBase, "0") ?? new (accountKeyBase, 0);
    }

    private static async Task StoreCreditEntity(CreditEntity creditEntity){
        await TableManager.StoreITableEntity("MoonfireCredit", creditEntity);
    }

    public static async Task<DateTime> GetLastActionTime(){
        //lastActionTime
        //get table value
        var timeEntity = await TableManager.GetITableEntity<TimeEntity>("MoonfireCredit", "CreditService", "lastActionTime");

        if(timeEntity is null){
            await SetLastActionTime(DateTime.UtcNow);
        }

        var lastActionTime = timeEntity is not null ? timeEntity.Time : DateTime.UtcNow;

        return lastActionTime;
    }

    public static async Task SetLastActionTime(DateTime dateTime){
        //set table value
        TimeEntity timeEntity = new("CreditService", "lastActionTime", dateTime);

        await TableManager.StoreITableEntity("MoonfireCredit", timeEntity);
    }

    private static async Task LogValueChange(string accountKey, double amount, string reason, CreditAction action){

        //append action to log

        //this should be added to a sperate permanent log

        await Console.Out.WriteLineAsync($"{nameof(CreditTableManager)}:{nameof(LogValueChange)}:accountKey:{accountKey}:amount:{amount}:reason:{reason}:action:{action}");
    }

    private enum CreditAction{
        INCREMENT,
        DECREMENT
    }

}
