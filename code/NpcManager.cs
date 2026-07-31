using System;
using System.Linq;
using Sandbox;

public sealed class NpcManager : Component
{
	public static NpcManager Instance { get; private set; }

	[Property, Description( "Prefab cloned for every NPC survivor. Must contain an NpcSurvivor component." )]
	public GameObject NpcPrefab { get; set; }

	[Property, Description( "Upper bound for the random NPC count. Every solo Trashman round spawns between 1 and this many NPCs." )]
	public int MaxNpcCount { get; set; } = 3;

	[Property, Description( "Pool of display names randomly assigned to spawned NPCs." )]
	public List<string> NpcNames { get; set; } = new()
	{
		"Zhora",
		"Tolyan",
		"Kabanchik",
		"Vasyan",
		"Pahan",
		"Semen",
		"Kolyan",
		"Vitalik",
		"Sanya",
		"Gena"
	};

	private readonly List<NpcSurvivor> _npcs = new();

	public bool CanSpawnNpcsServer()
	{
		if ( !Networking.IsHost || !NpcPrefab.IsValid() )
			return false;

		if ( !Scene.NavMesh.IsEnabled )
			return false;

		var spawns = MapInfo.Instance?.SpawnSurvivals ?? new();
		return spawns.Any( spawn => spawn.IsValid() );
	}

	public void SpawnNpcsServer()
	{
		if ( !CanSpawnNpcsServer() )
			return;

		ClearNpcsServer();

		var spawns = MapInfo.Instance.SpawnSurvivals
			.Where( spawn => spawn.IsValid() )
			.OrderBy( _ => Game.Random.Next() )
			.ToList();

		var count = Game.Random.Int( 1, Math.Max( 1, MaxNpcCount ) );
		var names = GetNamePoolServer( count );

		for ( var i = 0; i < count; i++ )
		{
			var spawn = spawns[i % spawns.Count];
			SpawnNpcServer( spawn.WorldPosition, spawn.WorldRotation, names[i] );
		}

		Log.Info( $"[NpcManager] Spawned {_npcs.Count} NPC survivors." );
	}

	public void ClearNpcsServer()
	{
		if ( !Networking.IsHost )
			return;

		foreach ( var npc in _npcs.ToArray() )
		{
			if ( npc.IsValid() && npc.GameObject.IsValid() )
				npc.GameObject.Destroy();
		}

		_npcs.Clear();
	}

	public void ForgetNpcServer( NpcSurvivor npc )
	{
		_npcs.Remove( npc );
	}

	public int NpcCountServer()
	{
		RemoveInvalidNpcsServer();
		return _npcs.Count;
	}

	public int AliveNpcCountServer()
	{
		RemoveInvalidNpcsServer();
		return _npcs.Count( npc => npc.IsAlive );
	}

	private void SpawnNpcServer( Vector3 position, Rotation rotation, string displayName )
	{
		var npcObject = NpcPrefab.Clone( position, rotation );
		if ( !npcObject.IsValid() )
		{
			Log.Error( "[NpcManager] Failed to clone NpcPrefab." );
			return;
		}

		npcObject.Name = $"npc_{displayName}";
		npcObject.NetworkMode = NetworkMode.Object;
		npcObject.Network.SetOrphanedMode( NetworkOrphaned.Host );

		var npc = npcObject.Components.Get<NpcSurvivor>( FindMode.EverythingInSelfAndDescendants );
		if ( !npc.IsValid() )
		{
			Log.Error( "[NpcManager] NpcPrefab has no NpcSurvivor component." );
			npcObject.Destroy();
			return;
		}

		if ( npc.Agent.IsValid() )
			npc.Agent.SetAgentPosition( position );

		npc.SetupServer( displayName );

		if ( !npcObject.NetworkSpawn() )
		{
			Log.Error( "[NpcManager] NetworkSpawn failed for an NPC survivor." );
			npcObject.Destroy();
			return;
		}

		_npcs.Add( npc );
	}

	private List<string> GetNamePoolServer( int count )
	{
		var pool = (NpcNames ?? new())
			.Where( name => !string.IsNullOrWhiteSpace( name ) )
			.Distinct()
			.OrderBy( _ => Game.Random.Next() )
			.ToList();

		var names = new List<string>();
		for ( var i = 0; i < count; i++ )
		{
			names.Add( pool.Count > 0
				? pool[i % pool.Count]
				: $"Bot {i + 1}" );
		}

		return names;
	}

	private void RemoveInvalidNpcsServer()
	{
		_npcs.RemoveAll( npc => !npc.IsValid() );
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
}
