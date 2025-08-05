public class Survival : Role
{
    public override Transform SpawnTransform { get; set; } = Gameplay.Map.SurvivalTransform;

    public override void Setup(Player player)
    {

    }
}