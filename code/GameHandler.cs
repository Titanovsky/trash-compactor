public static class GameHandler
{
	public static void Init()
	{
		InitRoles();
		//InitTrashTrigger();
		//SpawnPlayer();
	}

	private static void InitRoles()
	{
		RoleHandler.Reset(); // cuz static

		RoleHandler.Add( new Role( "spectator", "Spectator" ) );
		RoleHandler.Add( new Role( "trashman", "Trashman" ) );
		RoleHandler.Add( new Role( "soccer", "Soccer" ) );
	}
}
