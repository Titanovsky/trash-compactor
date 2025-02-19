public static class GameHandler
{
	public static void Init()
	{
		InitRoles();
	}

	private static void InitRoles()
	{
		var grabber = new Role( "grabber", "Grabber" );
		var survival = new Role( "grabber", "Grabber" );

		RoleHandler.Add( grabber );
		RoleHandler.Add( survival );
	}
}
