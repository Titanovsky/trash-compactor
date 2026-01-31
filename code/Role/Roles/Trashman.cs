using TrashCompactor.System;

public class Trashman : Role
{
    public override string Name { get; set; } = "Trashman";
    public override List<GameObject> Spawns { get; set; } = MapInfo.Instance.SpawnTrashmans;

    public override void Setup(Player player)
    {
        var spawns = MapInfo.Instance.SpawnTrashmans;

        player.WorldPosition = spawns.GetRandom().WorldPosition;
    }
}