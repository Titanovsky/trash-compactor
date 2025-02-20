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
		RoleHandler.Reset(); // cuz static

		string spectator = GameRoles.Spectator.ToString().ToLower();
		RoleHandler.Add( new Role( spectator, "Spectator" ) );

		string trashman = GameRoles.Trashman.ToString().ToLower();
		RoleHandler.Add( new Role( trashman, "Trashman" ) );

		string soccer = GameRoles.Soccer.ToString().ToLower();
		RoleHandler.Add( new Role( soccer, "Soccer" ) );
	}

	private static void SpawnPlayer()
	{
		var ply = Player.Instance;

		ply.SetRole( "soccer" );
		ply.SetupRole();
	}
}

public enum GameRoles
{
	Spectator,
	Trashman,
	Soccer
}
