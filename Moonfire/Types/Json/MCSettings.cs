namespace Moonfire.Types.Json;

public class MCSettings{
    public GameVersionEnum GameVersion = GameVersionEnum.JAVA_LATEST;
    //Users used for whitelist and admins, role != null if role is to be assigned
    public List<UserRecord> Users = [];
    public int MaxPlayers = 20;
    public int SpawnProtection = 16;
    public int ViewDistance = 10;
    public bool Whitelisted = false;
    public DifficultyEnum Difficulty = DifficultyEnum.EASY;
    public bool AllowCheats = false;
    public bool AllowFlight = false;
    public bool PvpEnabled = true;

    public enum GameVersionEnum {
        JAVA_LATEST,
        BEDROCK_LATEST
    }
    public enum DifficultyEnum {
        HARD,
        NORMAL,
        EASY,
        PEACEFUL
    }
    public record UserRecord {
        public required string Uuid {get; init;}
        public required string Name {get; init;}
        public required string? Role {get; init;}
    }

}

