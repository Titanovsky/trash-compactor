using TrashCompactor.System;

public class Gameplay : Component
{
	public static Gameplay Instance { get; private set; }

	[Property] public MapInfo MapInfo { get; set; }
	[Property] public GameObject Spawn;

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