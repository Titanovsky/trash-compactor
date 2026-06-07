using TrashCompactor.System;

public class Survival : Role
{
	public override string Name { get; set; } = "Survival";
	public override RoleTrashCompactor RoleEnum => RoleTrashCompactor.Survival;

	public override List<GameObject> GetSpawns( MapInfo mapInfo )
	{
		return mapInfo?.SpawnSurvivals ?? new();
	}
}
