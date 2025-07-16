public static class GameHandler
{
	public static void Init()
	{
		InitRoles();
		//InitTrashTrigger();
		SpawnPlayer();
	}

	private static void InitRoles()
	{
		RoleManager.Reset(); // cuz static

		string spectator = TrashCompactorRole.Spectator.ToString().ToLower();
		RoleManager.Add( new Role( spectator, "Spectator" ) );

		string trashman = TrashCompactorRole.Trashman.ToString().ToLower();
		RoleManager.Add( new Role( trashman, "Trashman" ) );

		string soccer = TrashCompactorRole.Soccer.ToString().ToLower();
		RoleManager.Add( new Role( soccer, "Soccer" ) );
	}

	private static void SpawnPlayer()
	{
		var ply = Player.Local;

		ply.SetRole( "soccer" );
		ply.SetupRole();
	}
}

public enum TrashCompactorRole
{
	Spectator,
	Trashman,
	Soccer
}
