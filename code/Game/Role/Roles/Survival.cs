public class Survival : Role
{
    public override Transform SpawnTransform { get; set; } = Gameplay.MapInfo.SurvivalTransform;

    public override void Setup(Player player)
    {

    }
}