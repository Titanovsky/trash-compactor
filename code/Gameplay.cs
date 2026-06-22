public class Gameplay : Component
{
	public static Gameplay Instance { get; private set; }

	[Property, Description( "Reference to the MapInfo component that provides spawn point data for the current map." )] public MapInfo MapInfo { get; set; }

	public void Init()
	{
		// RoundManager owns player spawn and role placement.
	}

	[Rpc.Broadcast(NetFlags.Reliable | NetFlags.HostOnly)]
	public void SpawnPlayers()
	{
		// Legacy entry point kept for scene/API compatibility.
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
