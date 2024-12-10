namespace AzureAllocator;

public class Program{
    
    //entry point
    public static async Task Main(){
        //load .env
        //this is a terrible way to do this, old way of doing this broke???, old way (File.ReadAllLines(".env")))
        var envpath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","..","..",".env");
        foreach(string line in File.ReadAllLines(envpath)){
            //From NAME="value" Env.Set(NAME,value,process)
            Environment.SetEnvironmentVariable(line[0..line.IndexOf('=')],line[(line.IndexOf('=')+2)..line.LastIndexOf('"')],EnvironmentVariableTarget.Process);
        }

        await Console.Out.WriteLineAsync("Hello World!");
    }
}