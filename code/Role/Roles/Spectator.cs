using TrashCompactor.System;

public class Spectator : Role
{
	public override string Name { get; set; } = "Spectator";
	public override RoleTrashCompactor RoleEnum => RoleTrashCompactor.Spectator;

	public override List<GameObject> GetSpawns( MapInfo mapInfo )
	{
		return mapInfo?.SpawnSpectors ?? new();
	}
}
