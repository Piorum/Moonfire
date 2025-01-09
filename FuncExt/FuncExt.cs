using System.Diagnostics;

namespace FuncExt;

public static class Ext
{   
    //option for default start/end message
    public static async Task Time(
        Func<Task> function,
        object? warning = null,
        Func<TimeSpan, bool>? warningCondition = null
    ) =>
        await Time(function, "FuncExt.Time Started", "FuncExt.Time Ended", warning, warningCondition);
    
    //option for default output method, console
    public static async Task Time(
        Func<Task> function,
        object start,
        object end,
        object? warning = null,
        Func<TimeSpan, bool>? warningCondition = null
    ) =>
        await Time(
            (a) => {Console.WriteLine(a);return Task.CompletedTask;},
            function, start, end, warning, warningCondition);

    //option for no modifymessage
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

    //option for modifymessage that doesn't need a return from sendmessage
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
        
    //Main Time Function
    public static async Task Time<T>(
        Func<string, Task<T>> sendMessage,
        Func<T, string, Task> modifyMessage,
        Func<Task> function,
        object start,
        object end,
        object? warning = null,
        Func<TimeSpan, bool>? warningCondition = null
    ){
        //makes strings into string functions, nulls other objects
        Func<string>? Transform(object? input) => input switch {
            string a => () => a,
            Func<string> b => b,
            _ => null
        };

        //ensure strings are string funcs and other objects are nulled
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
        await modifyMessage(tmp, endFunc() + $" - Took {stopwatch.ElapsedMilliseconds:D3}ms");
        if(warningCondition==null || warningFunc == null) return;
        await Task.Delay(1000); //small delay because async is being weird
        if(warningCondition(stopwatch.Elapsed)) await modifyMessage(tmp, warningFunc());
    }

    public static async Task TimeoutTask(Task mainTask, Lazy<Task>? cleanupTask, TimeSpan timeoutLength, CancellationTokenSource cts){
        TaskCompletionSource<bool> finished = new();

        var wrapperTask = Task.Run(async () => {
            try {
                await mainTask;
                finished.TrySetResult(true);
            } catch (OperationCanceledException){
                _ = Console.Out.WriteLineAsync($"EXT:TASK_CANCELLED_BY_TIMEOUT");
                if(cleanupTask!=null) await cleanupTask.Value;
                finished.TrySetResult(true);
            } catch (Exception e){
                _ = Console.Out.WriteLineAsync($"EXT:TASK_ENCOUNTED_EXCEPTION");
                _ = Console.Out.WriteLineAsync($"{e}");
                if(cleanupTask!=null) await cleanupTask.Value;
                finished.TrySetResult(true);
            }
        },cts.Token);

        var timeoutTask = Task.Delay(timeoutLength).ContinueWith(_ => {
            if(!finished.Task.IsCompleted) cts.Cancel();
        });

        await Task.WhenAny(wrapperTask, timeoutTask);
        if(!finished.Task.IsCompleted) await wrapperTask;
    }

    public static async Task TimeoutTask(Task mainTask, TimeSpan timeoutLength, CancellationTokenSource cts){
        await TimeoutTask(mainTask,null,timeoutLength,cts);
    }

    public static async Task RunUnderTry(Task mainTask){
        await Task.Run(async () => {
            try{
                await mainTask;
            }catch(Exception e){
                //log here to prevent silent failure of background task
                await Console.Out.WriteLineAsync($"{e}");
            }
        });
    }

}