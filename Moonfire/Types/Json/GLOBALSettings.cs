using Newtonsoft.Json;

namespace Moonfire.Types.Json;

public class GLOBALSettings{
    [JsonProperty(nameof(region))]
    public string region  = "NA";
}
