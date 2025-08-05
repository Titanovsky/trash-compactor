public class Spectator : Role
{
    public override string Name { get; set; } = "Spectator";
    public override Transform SpawnTransform { get; set; } = Gameplay.Map.TrashmanTransform;

    public override void Setup(Player player)
    {
        player.CanFly = true;
        player.Godmode = true;
    }
}