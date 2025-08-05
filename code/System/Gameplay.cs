using TrashCompactor.System;

public class Gameplay : Component
{
	public static Gameplay Instance { get; private set; }

	[Property] public MapInfo MapInfo { get; set; }

	public static void Init()
	{
		InitRoles();
		//InitTrashTrigger();
		SpawnPlayer();
	}

	private static void InitRoles()
	{
		//RoleManager.Reset(); // cuz static

		//string spectator = TrashCompactorRole.Spectator.ToString().ToLower();
		//RoleManager.Add( new RoleBase( spectator, "Spectator" ) );

		//string trashman = TrashCompactorRole.Trashman.ToString().ToLower();
		//RoleManager.Add( new RoleBase( trashman, "Trashman" ) );

		//string soccer = TrashCompactorRole.Soccer.ToString().ToLower();
		//RoleManager.Add( new RoleBase( soccer, "Soccer" ) );
	}

	private static void SpawnPlayer()
	{
		//var ply = Player.Local;

		//ply.SetRole( "soccer" );
		//ply.SetupRole();
	}

    protected override void OnAwake()
    {
		if (Instance == null)
			Instance = this;
    }

    protected override void OnDestroy()
    {
		Instance = null;
    }
}