public class Survival : Role
{
    public override string Name { get; set; } = "Survival";
    public override Transform SpawnTransform { get; set; } = Gameplay.MapInfo.SurvivalTransform;

    public override void Setup(Player player)
    {

    }
}