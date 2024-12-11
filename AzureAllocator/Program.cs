using Azure.Identity;
using Azure.ResourceManager;

namespace AzureAllocator;

public class Program{
    
    //entry point
    public static async Task Main(){
        //loading .env
        var envPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "Moonfire",
            ".env"
        );
        foreach(string line in File.ReadAllLines(envPath)){
            //From NAME="value" Env.Set(NAME,value,process)
            Environment.SetEnvironmentVariable(line[0..line.IndexOf('=')],line[(line.IndexOf('=')+2)..line.LastIndexOf('"')],EnvironmentVariableTarget.Process);
        }

        //creating ArmClient
        var clientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID") ?? "";
        var clientSecret = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET") ?? "";
        var tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID") ?? "";
        var subscription = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID") ?? "";
        ClientSecretCredential credential = new(tenantId, clientId, clientSecret);
        ArmClient client = new(credential, subscription);

        //loading settings
        var templatePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "Moonfire",
            "Config",
            "Template.json"
        );
        var settings = await AzureSettings.CreateAsync(templatePath);

        //allocating vm
        var vm = await AzureManager.Allocate(client, settings, "TestRG", "TestVM");

        //deallocating vm
        //check for null, but should never be null here
        /*if(vm==null){
            return;
        }
        await vm.DeAllocate();*/
    }
}