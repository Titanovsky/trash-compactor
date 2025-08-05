public class Spectator : Role
{
    public override string Name { get; set; } = "Spectator";
    public override Transform SpawnTransform { get; set; } = Gameplay.Instance.MapInfo?.SpectatorTransform ?? new Transform();

    public override void Setup(Player player)
    {
        base.Setup(player);

        player.CanFly = true;
        player.Godmode = true;
    }
}