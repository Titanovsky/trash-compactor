using System.Linq;
using Sandbox;

public sealed class SpawnerTrash : Component
{
	public static SpawnerTrash Instance { get; private set; }

	[Property, Description( "Trash prop prefabs used by this spawner." )] public List<GameObject> TrashPrefabs { get; set; } = new();

	[Property, Group( "Round Stock" ), Description( "Number of trash props spawned at the beginning of each standard round." )] public int PropsPerRound { get; set; } = 12;
	[Property, Group( "Solo" ), Description( "Time in seconds between automatic trash prop spawns during a solo round." )] public float SoloAutoSpawnInterval { get; set; } = 3f;
	[Property, Group( "Solo" ), Description( "Time in seconds before solo trash automatically becomes harmless to players." )] public float SoloSafetyDelay { get; set; } = 6f;
	[Property, Group( "Solo" ), Description( "Maximum positional offset applied to solo trash spawn positions. Each axis is randomized in range [-value, +value] every spawn." )] public Vector3 SoloSpawnOffset { get; set; } = Vector3.Zero;
	[Property, Group( "Round Stock" ), Description( "Time in seconds before a round-stock trash prop becomes harmless after it is grabbed for the first time." )] public float GrabbedSafetyDelay { get; set; } = 12f;
	[Property, Group( "Cleanup" ), Description( "Time in seconds after which a spawned trash prop is automatically destroyed." )] public float TrashLifetime { get; set; } = 30f;

	private readonly List<GameObject> _spawnedTrash = new();
	private bool _soloAutoSpawnEnabled;
	private TimeUntil _nextSoloAutoSpawn;

	public void StartRoundServer( bool soloRound )
	{
		if ( !Networking.IsHost )
			return;

		PrepareRoundServer( soloRound );
		FinalizeRoundStartServer();
	}

	public void PrepareRoundServer( bool soloRound )
	{
		if ( !Networking.IsHost )
			return;

		ClearSpawnedTrashServer();
		_soloAutoSpawnEnabled = soloRound;
		_nextSoloAutoSpawn = SoloAutoSpawnInterval;
	}

	public void FinalizeRoundStartServer()
	{
		if ( !Networking.IsHost )
			return;

		if ( !_soloAutoSpawnEnabled )
			SpawnRoundStockServer();
	}

	public void FinishRoundServer()
	{
		if ( !Networking.IsHost )
			return;

		_soloAutoSpawnEnabled = false;
	}

	private void SpawnRoundStockServer()
	{
		var spawns = MapInfo.Instance?.TrashPropSpawns ?? new();
		for ( var i = 0; i < PropsPerRound; i++ )
		{
			var spawn = spawns.Count > 0 ? spawns[i % spawns.Count] : GameObject;
			SpawnTrashServer( spawn.WorldPosition, Rotation.Random, false );
		}
	}

	private void UpdateSoloAutoSpawnServer()
	{
		if ( !_soloAutoSpawnEnabled || !_nextSoloAutoSpawn )
			return;

		var spawns = MapInfo.Instance?.SoloTrashPropSpawns ?? new();
		var spawn = spawns.Count > 0 ? spawns.GetRandom() : GameObject;

		var offset = new Vector3(
			Game.Random.Float( -SoloSpawnOffset.x, SoloSpawnOffset.x ),
			Game.Random.Float( -SoloSpawnOffset.y, SoloSpawnOffset.y ),
			Game.Random.Float( -SoloSpawnOffset.z, SoloSpawnOffset.z )
		);
		SpawnTrashServer( spawn.WorldPosition + offset, Rotation.Random, true );
		_nextSoloAutoSpawn = SoloAutoSpawnInterval;
	}

	private GameObject SpawnTrashServer( Vector3 position, Rotation rotation, bool startLifetimeImmediately )
	{
		var prefab = GetTrashPrefab();
		if ( !prefab.IsValid() )
		{
			Log.Warning( "[Trash] TrashPrefabs is empty." );
			return null;
		}

		var trash = prefab.Clone( position, rotation );
		trash.Network.SetOrphanedMode( NetworkOrphaned.Host );
		trash.NetworkSpawn();
		_spawnedTrash.Add( trash );

		if ( startLifetimeImmediately )
		{
			StartTrashLifetimeServer( trash );
			StartTrashSafetyServer( trash, SoloSafetyDelay );
		}

		return trash;
	}

	private GameObject GetTrashPrefab()
	{
		var prefabs = TrashPrefabs.Where( prefab => prefab.IsValid() ).ToList();
		if ( prefabs.Count == 0 )
			return null;

		return prefabs.GetRandom();
	}

	public void ForgetTrashServer( GameObject trash )
	{
		_spawnedTrash.Remove( trash );
	}

	public void StartTrashLifetimeServer( GameObject trash )
	{
		if ( !Networking.IsHost || !trash.IsValid() )
			return;

		var trashComponent = trash.Components.Get<Trash>();
		if ( !trashComponent.IsValid() )
			return;

		trashComponent.StartLifetimeTimerOnce( TrashLifetime );
	}

	public void StartTrashSafetyServer( GameObject trash, float delay )
	{
		if ( !Networking.IsHost || !trash.IsValid() )
			return;

		var trashComponent = trash.Components.Get<Trash>();
		if ( !trashComponent.IsValid() )
			return;

		trashComponent.StartSafetyModeTimerOnce( delay );
	}

	public GameObject SpawnBaseTrashServer()
	{
		if ( !Networking.IsHost )
			return null;

		var spawns = MapInfo.Instance?.TrashPropSpawns ?? new();
		var spawn = spawns.Count > 0 ? spawns.GetRandom() : GameObject;

		return SpawnTrashServer( spawn.WorldPosition, spawn.WorldRotation, false );
	}

	private void ClearSpawnedTrashServer()
	{
		foreach ( var trash in _spawnedTrash.ToArray() )
		{
			if ( trash.IsValid() )
				trash.Destroy();
		}

		_spawnedTrash.Clear();
	}

	private void CreateSingleton()
	{
		if ( Instance != null )
			return;

		Instance = this;
	}

	private void RemoveSingleton()
	{
		if ( Instance != this )
			return;

		Instance = null;
	}

	protected override void OnAwake()
	{
		CreateSingleton();
	}

	protected override void OnDestroy()
	{
		RemoveSingleton();
	}

	protected override void OnUpdate()
	{
		UpdateSoloAutoSpawnServer();
	}
}
