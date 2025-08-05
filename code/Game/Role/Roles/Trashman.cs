public class Trashman : Role
{
    public override string Name { get; set; } = "Trashman";
    public override Transform SpawnTransform { get; set; } = Gameplay.Instance.MapInfo.TrashmanTransform;

    public override void Setup(Player player)
    {

    }
}