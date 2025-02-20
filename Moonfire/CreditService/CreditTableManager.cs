namespace Moonfire.CreditService;

public static class CreditTableManager
{
    public static async Task<(string, double)> DecrementCredit(string accountKey, double amount, string reason){
        await LogValueChange(amount, reason, CreditAction.DECREMENT);

        //perform decrement

        //return accountKey and newAccountBalance

        throw new NotImplementedException();
    }

    public static async Task<(string, double)> IncrementCredit(string accountKey, double amount, string reason){
        await LogValueChange(amount, reason, CreditAction.INCREMENT);

        //perform increment

        //return accountKey and newAccountBalance

        throw new NotImplementedException();
    }

    private static Task LogValueChange(double amount, string reason, CreditAction action){

        //append action to log

        throw new NotImplementedException();
    }

    private enum CreditAction{
        INCREMENT,
        DECREMENT
    }

}
