using System.Threading.Tasks;
using Sandbox;
using TrashCompactor.System;

public sealed class SpawnerTrash : Component
{
	public static SpawnerTrash Instance { get; private set; }

	[Property] public GameObject TrashPrefab { get; set; }

	[Property, Group( "Round Stock" )] public int PropsPerRound { get; set; } = 12;
	[Property, Group( "Solo" )] public float SoloAutoSpawnInterval { get; set; } = 3f;
	[Property, Group( "Cleanup" )] public float TrashLifetime { get; set; } = 30f;

	private readonly List<GameObject> _spawnedTrash = new();
	private bool _soloAutoSpawnEnabled;
	private TimeUntil _nextSoloAutoSpawn;

	public void StartRoundServer( bool soloRound )
	{
		if ( !Networking.IsHost )
			return;

		ClearSpawnedTrashServer();
		_soloAutoSpawnEnabled = soloRound;
		_nextSoloAutoSpawn = 0f;

		if ( !soloRound )
			SpawnRoundStockServer();
	}

	public void FinishRoundServer()
	{
		if ( !Networking.IsHost )
			return;

		_soloAutoSpawnEnabled = false;
		ClearSpawnedTrashServer();
	}

	private void SpawnRoundStockServer()
	{
		var spawns = MapInfo.Instance?.TrashPropSpawns ?? new();
		for ( var i = 0; i < PropsPerRound; i++ )
		{
			var spawn = spawns.Count > 0 ? spawns[i % spawns.Count] : GameObject;
			SpawnTrashServer( spawn.WorldPosition, Rotation.Random );
		}
	}

	private void UpdateSoloAutoSpawnServer()
	{
		if ( !_soloAutoSpawnEnabled || !_nextSoloAutoSpawn )
			return;

		var spawns = MapInfo.Instance?.SoloTrashPropSpawns ?? new();
		var spawn = spawns.Count > 0 ? spawns.GetRandom() : GameObject;

		SpawnTrashServer( spawn.WorldPosition, Rotation.Random );
		_nextSoloAutoSpawn = SoloAutoSpawnInterval;
	}

	private GameObject SpawnTrashServer( Vector3 position, Rotation rotation )
	{
		if ( !TrashPrefab.IsValid() )
		{
			Log.Warning( "[Trash] TrashPrefab is not set." );
			return null;
		}

		var trash = TrashPrefab.Clone( position, rotation );
		_spawnedTrash.Add( trash );

		_ = RemoveTrashDelayServer( trash );
		return trash;
	}

	private async Task RemoveTrashDelayServer( GameObject trash )
	{
		await Task.DelaySeconds( TrashLifetime );
		if ( !trash.IsValid() )
			return;

		_spawnedTrash.Remove( trash );
		trash.Destroy();
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
