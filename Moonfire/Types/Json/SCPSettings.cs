using Newtonsoft.Json;

namespace Moonfire.Types.Json;

public class SCPSettings{
    [JsonProperty(nameof(AdminUsers))]
    public List<AdminUser> AdminUsers  = [];
    [JsonProperty(nameof(Branch))]
    public SCPBranch Branch = SCPBranch.PUBLIC;

    public record AdminUser {
        public required ulong Id {get; init;}
        public required string Role {get; init;}
    }
}

public enum SCPBranch{
    PUBLIC
}