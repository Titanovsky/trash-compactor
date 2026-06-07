using TrashCompactor.System;

public class Gameplay : Component
{
	public static Gameplay Instance { get; private set; }

	[Property, Description( "Reference to the MapInfo component that provides spawn point data for the current map." )] public MapInfo MapInfo { get; set; }
	[Property, Description( "Default fallback spawn point used when no role-specific spawn is available." )] public GameObject Spawn;

	public void Init()
	{
		InitRoles();
		//InitTrashTrigger();
		SpawnPlayers();
	}

	private void InitRoles()
	{
		//RoleManager.Reset(); // cuz static

		//string spectator = TrashCompactorRole.Spectator.ToString().ToLower();
		//RoleManager.Add( new RoleBase( spectator, "Spectator" ) );

		//string trashman = TrashCompactorRole.Trashman.ToString().ToLower();
		//RoleManager.Add( new RoleBase( trashman, "Trashman" ) );

		//string soccer = TrashCompactorRole.Soccer.ToString().ToLower();
		//RoleManager.Add( new RoleBase( soccer, "Soccer" ) );
	}

	[Rpc.Broadcast(NetFlags.Reliable | NetFlags.HostOnly)]
	public void SpawnPlayers()
	{
		if (IsProxy) return;

		Player.Local.Spawn();
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