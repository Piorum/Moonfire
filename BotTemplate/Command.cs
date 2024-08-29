namespace SCDisc;

public class Command(string name, string description, SocketGuild guild){
    public string Name{ get; set; } = name;
    public string Description { get; set; } = description;
    public SocketGuild Guild { get; set; } = guild;

}
