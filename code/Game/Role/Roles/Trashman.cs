public class Trashman : Role
{
    public override Transform SpawnTransform { get; set; } = Gameplay.Map.TrashmanTransform;

    public override void Setup(Player player)
    {

    }
}