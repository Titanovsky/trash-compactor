using System;

public abstract class Role
{
	public virtual string Name { get; set; } = "none";
	public abstract RoleTrashCompactor RoleEnum { get; }

	public virtual List<GameObject> GetSpawns( MapInfo mapInfo ) => new();

	public virtual bool Check( Type type )
	{
		return type == GetType();
	}

	public virtual bool Check( string type )
	{
		return type.ToLower() == GetType().ToString().ToLower();
	}

	public bool Check( RoleTrashCompactor role )
	{
		return RoleEnum == role;
	}

	public static Role Create( RoleTrashCompactor role )
	{
		return role switch
		{
			RoleTrashCompactor.Trashman => new Trashman(),
			RoleTrashCompactor.Spectator => new Spectator(),
			_ => new Survival()
		};
	}
}
