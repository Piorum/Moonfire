using System.Diagnostics;

namespace SCDisc.Utility;

/*
Task example(){
    Console.WriteLine("Task!");
    return Task.CompletedTask;
}
Task sendMessage(string a){
    Console.WriteLine(a);
    return Task.CompletedTask;
}
await FunctionTimer.Time(
    sendMessage,
    example,
    "start",
    "end"
);
*/

public static class FunctionTimer
{
    public static async Task Time(
        Func<string, Task> sendMessage,
        Func<Task> function,
        object start,
        object end,
        object? warning = null,
        Func<TimeSpan, bool>? warningCondition = null
    ) =>
        await Time(
            sendMessage,
            a => { sendMessage(a); return Task.CompletedTask; },
            function,start,end,warning,warningCondition);

    public static async Task Time(
        Func<string, Task> sendMessage,
        Func<string, Task> modifyMessage,
        Func<Task> function,
        object start,
        object end,
        object? warning = null,
        Func<TimeSpan, bool>? warningCondition = null
    ) =>
        await Time(
            a => {sendMessage(a); return Task.FromResult(true);}, 
            (a, b) => {modifyMessage(b); return Task.CompletedTask;}, 
            function, start, end, warning, warningCondition);
        
    public static async Task Time<T>(
        Func<string, Task<T>> sendMessage,
        Func<T, string, Task> modifyMessage,
        Func<Task> function,
        object start,
        object end,
        object? warning = null,
        Func<TimeSpan, bool>? warningCondition = null
    ){
        Func<string>? Transform(object? input) => input switch {
            string a => () => a,
            Func<string> b => b,
            _ => null
        };

        var startFunc = Transform(start);
        var endFunc = Transform(end);
        var warningFunc = Transform(warning);

        if(startFunc == null || endFunc == null){
            Console.WriteLine($"Non String/Func<string> passed to FunctionTimer.Time");
            return;
        }

        var tmp = await sendMessage(startFunc());
        var stopwatch = Stopwatch.StartNew();
        await function();
        stopwatch.Stop();
        await modifyMessage(tmp, endFunc() + $" - {stopwatch.ElapsedMilliseconds:D3}ms");
        if(warningCondition==null || warningFunc == null) return;
        if(warningCondition(stopwatch.Elapsed)) await modifyMessage(tmp, warningFunc());
    }

}