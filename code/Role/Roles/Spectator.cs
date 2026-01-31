public class Spectator : Role
{
    public override string Name { get; set; } = "Spectator";

    public override void Setup(Player player)
    {
        base.Setup(player);

        player.CanFly = true;
        player.Godmode = true;
    }
}