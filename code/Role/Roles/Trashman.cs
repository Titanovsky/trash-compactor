using TrashCompactor.System;

public class Trashman : Role
{
	public override string Name { get; set; } = "Trashman";
	public override RoleTrashCompactor RoleEnum => RoleTrashCompactor.Trashman;

	public override List<GameObject> GetSpawns( MapInfo mapInfo )
	{
		return mapInfo?.SpawnTrashmans ?? new();
	}
}
