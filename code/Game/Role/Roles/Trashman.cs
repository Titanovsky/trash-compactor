public class Trashman : Role
{
    public override Transform SpawnTransform { get; set; } = Gameplay.MapInfo.TrashmanTransform;

    public override void Setup(Player player)
    {

    }
}