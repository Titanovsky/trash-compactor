using System;
using System.Linq;

public sealed class GameManager : Component
{
	private const float MapVoteDuration = 10f;

	public const string InitialMapPackageId = "dmgames.trashcompactor";

	public static readonly string[] MapVoteChoices =
	[
		"titanovsky.tc_down",
		"dmgames.trashcompactor"
	];

	public static GameManager Instance { get; private set; }

	[Property, Description( "The MapInstance that loads the selected cloud map." )]
	public MapInstance MapInstance { get; set; }

	[Property, Description( "Map gameplay prefabs. Each prefab name must be its full package id, for example author.name." )]
	public List<GameObject> MapPrefabs { get; set; } = new();

	[Sync( SyncFlags.FromHost )]
	public string CurrentMapPackageId { get; private set; } = "";

	[Sync( SyncFlags.FromHost )]
	public int ActiveMapRevision { get; private set; }

	[Sync( SyncFlags.FromHost )]
	public bool IsMapReady { get; private set; }

	[Sync( SyncFlags.FromHost )]
	public int HostReadyMapRevision { get; private set; } = -1;

	[Sync( SyncFlags.FromHost )]
	public bool IsMapVoteOpen { get; private set; }

	[Sync( SyncFlags.FromHost )]
	public TimeUntil MapVoteTimer { get; private set; }

	[Sync( SyncFlags.FromHost )]
	public int TcDownVotes { get; private set; }

	[Sync( SyncFlags.FromHost )]
	public int TrashCompactorVotes { get; private set; }

	public bool IsHostReadyForCurrentMap =>
		(IsMapReady && HostReadyMapRevision == ActiveMapRevision)
		|| _hostReadyRpcRevision == ActiveMapRevision;

	public bool IsLocalMapLoadedForCurrentRevision =>
		MapInstance.IsValid()
		&& MapInstance.IsLoaded
		&& _appliedMapRevision == ActiveMapRevision
		&& string.Equals( CanonicalPackageId( MapInstance.MapName ), CanonicalPackageId( CurrentMapPackageId ), StringComparison.OrdinalIgnoreCase );

	public float MapVoteSecondsLeft => IsMapVoteOpen
		? MathF.Max( 0f, MapVoteTimer.Relative )
		: 0f;

	public bool CanRequestMapVote =>
		Networking.IsHost
		&& !IsMapVoteOpen
		&& IsHostReadyForCurrentMap
		&& IsLocalMapLoadedForCurrentRevision;

	private readonly Dictionary<string, string> _mapVotes = new();
	private GameObject _activeMapPrefab;
	private string _spawnedMapPackageId = "";
	private bool _callbacksSubscribed;
	private string _pendingMapPackageId = "";
	private int _pendingMapRevision;
	private int _appliedMapRevision;
	private int _mapLoadRevision;
	private int _mapUnloadObservedRevision = -1;
	private int _hostReadyRpcRevision = -1;
	private int _navMeshRevision = -1;

	public bool LoadMapServer( string packageIdOrUrl )
	{
		if ( !Networking.IsHost )
			return false;

		var packageId = NormalizePackageId( packageIdOrUrl );
		if ( !ValidateMapPackageId( packageId ) )
			return false;

		if ( !EnsureMapInstance() )
			return false;

		CloseMapVote();
		ActiveMapRevision++;
		CurrentMapPackageId = packageId;
		BeginMapLoadingServer();
		BeginMapChange( packageId, ActiveMapRevision, true );
		return true;
	}

	public void RequestMapVote()
	{
		if ( !CanRequestMapVote )
			return;

		OpenMapVote();
	}

	public void VoteForMap( string mapName )
	{
		if ( !IsMapVoteOpen || !MapVoteChoices.Contains( mapName ) )
			return;

		if ( Networking.IsHost )
		{
			RegisterMapVote( mapName, GetLocalVoterId(), GetLocalVoterName() );
			return;
		}

		SubmitMapVote( mapName );
	}

	public int GetMapVoteCount( string mapName )
	{
		if ( string.Equals( mapName, MapVoteChoices[0], StringComparison.OrdinalIgnoreCase ) )
			return TcDownVotes;

		if ( string.Equals( mapName, MapVoteChoices[1], StringComparison.OrdinalIgnoreCase ) )
			return TrashCompactorVotes;

		return 0;
	}

	[Rpc.Host( NetFlags.Reliable )]
	private void SubmitMapVote( string mapName )
	{
		var caller = Rpc.Caller;
		if ( caller is null )
			return;

		RegisterMapVote( mapName, caller.Id.ToString(), caller.DisplayName );
	}

	private void RegisterMapVote( string mapName, string voterId, string voterName )
	{
		if ( !Networking.IsHost || !IsMapVoteOpen )
			return;

		if ( !MapVoteChoices.Contains( mapName ) || string.IsNullOrWhiteSpace( voterId ) )
			return;

		_mapVotes[voterId] = mapName;
		RecountMapVotes();
		Log.Info( $"[GameManager] Vote accepted from {voterName} ({voterId}): {mapName}." );

		if ( HaveAllPlayersVoted() )
			FinishMapVote();
	}

	private void OpenMapVote()
	{
		if ( !Networking.IsHost || IsMapVoteOpen || !IsHostReadyForCurrentMap )
			return;

		RoundManager.Instance?.BeginMapVoteServer();
		IsMapVoteOpen = true;
		MapVoteTimer = MapVoteDuration;
		_mapVotes.Clear();
		RecountMapVotes();
		Log.Info( "[GameManager] Map vote opened." );
	}

	private void FinishMapVote()
	{
		if ( !Networking.IsHost || !IsMapVoteOpen )
			return;

		PruneDisconnectedVotes();

		if ( _mapVotes.Count == 0 )
		{
			CloseMapVote();
			RoundManager.Instance?.ResumeAfterMapVoteServer();
			return;
		}

		var selectedMap = MapVoteChoices
			.Select( (mapName, index) => new { MapName = mapName, Votes = GetMapVoteCount( mapName ), Index = index } )
			.OrderByDescending( choice => choice.Votes )
			.ThenBy( choice => choice.Index )
			.First().MapName;

		Log.Info( $"[GameManager] Map vote finished. Selected {selectedMap}." );
		CloseMapVote();

		if ( !LoadMapServer( selectedMap ) )
			RoundManager.Instance?.ResumeAfterMapVoteServer();
	}

	private void CloseMapVote()
	{
		IsMapVoteOpen = false;
		MapVoteTimer = 0f;
		_mapVotes.Clear();
		RecountMapVotes();
	}

	private bool HaveAllPlayersVoted()
	{
		if ( !Networking.IsHost )
			return false;

		PruneDisconnectedVotes();
		var voterIds = Connection.All.Select( connection => connection.Id.ToString() ).ToList();
		return voterIds.Count > 0 && voterIds.All( voterId => _mapVotes.ContainsKey( voterId ) );
	}

	private void PruneDisconnectedVotes()
	{
		var connectedVoters = Connection.All
			.Select( connection => connection.Id.ToString() )
			.ToHashSet();

		foreach ( var voterId in _mapVotes.Keys.Where( voterId => !connectedVoters.Contains( voterId ) ).ToArray() )
			_mapVotes.Remove( voterId );

		RecountMapVotes();
	}

	private void RecountMapVotes()
	{
		TcDownVotes = _mapVotes.Values.Count( mapName => string.Equals( mapName, MapVoteChoices[0], StringComparison.OrdinalIgnoreCase ) );
		TrashCompactorVotes = _mapVotes.Values.Count( mapName => string.Equals( mapName, MapVoteChoices[1], StringComparison.OrdinalIgnoreCase ) );
	}

	private static string GetLocalVoterId()
	{
		return Connection.Local is not null ? Connection.Local.Id.ToString() : "local-host";
	}

	private static string GetLocalVoterName()
	{
		return Connection.Local is not null ? Connection.Local.DisplayName : "Local Host";
	}

	private bool ValidateMapPackageId( string packageId )
	{
		if ( !Package.TryParseIdent( packageId, out _ ) )
		{
			Log.Error( $"[GameManager] Invalid map package id: '{packageId}'." );
			return false;
		}

		var prefabPackageId = CanonicalPackageId( packageId );
		if ( FindMapPrefab( prefabPackageId ).IsValid() )
			return true;

		Log.Error( $"[GameManager] No map prefab named '{prefabPackageId}' was assigned to MapPrefabs." );
		return false;
	}

	private void LoadInitialMapServer()
	{
		var packageId = NormalizePackageId( InitialMapPackageId );
		if ( !ValidateMapPackageId( packageId ) || !EnsureMapInstance() )
			return;

		CurrentMapPackageId = packageId;
		ActiveMapRevision = Math.Max( 1, ActiveMapRevision );
		BeginMapLoadingServer();

		var canReuseLoadedMap = MapInstance.IsLoaded
			&& string.Equals( CanonicalPackageId( MapInstance.MapName ), CanonicalPackageId( packageId ), StringComparison.OrdinalIgnoreCase );
		BeginMapChange( packageId, ActiveMapRevision, MapInstance.IsLoaded && !canReuseLoadedMap );

		if ( canReuseLoadedMap )
			HandleMapLoaded();
	}

	private void BeginMapLoadingServer()
	{
		ResetHostMapReady();
		_spawnedMapPackageId = "";
		_navMeshRevision = -1;

		NpcManager.Instance?.ClearNpcsServer();
		RoundManager.Instance?.SetMapLoadingServer();
		MapInfo.Instance?.ReleaseForMapUnloadServer();
		if ( Gameplay.Instance.IsValid() )
			Gameplay.Instance.MapInfo = null;

		if ( _activeMapPrefab.IsValid() )
			_activeMapPrefab.Destroy();

		_activeMapPrefab = null;
	}

	private void BeginMapChange( string packageId, int revision, bool forceReload )
	{
		if ( !EnsureMapInstance() )
			return;

		_mapLoadRevision = revision;
		_mapUnloadObservedRevision = !forceReload || !MapInstance.IsLoaded ? revision : -1;

		var sameMap = string.Equals( CanonicalPackageId( MapInstance.MapName ), CanonicalPackageId( packageId ), StringComparison.OrdinalIgnoreCase );
		if ( forceReload && sameMap && MapInstance.IsLoaded )
		{
			_pendingMapPackageId = packageId;
			_pendingMapRevision = revision;
			Log.Info( $"[GameManager] Reloading map '{packageId}' for revision {revision}: unloading the current MapInstance first." );
			MapInstance.UnloadMap();
			MapInstance.MapName = string.Empty;
			return;
		}

		_pendingMapPackageId = "";
		_pendingMapRevision = 0;
		MapInstance.MapName = packageId;
		_appliedMapRevision = revision;
		Log.Info( $"[GameManager] MapInstance.MapName set to '{packageId}' for revision {revision}." );
	}

	private void ApplyPendingMapChange()
	{
		if ( !EnsureMapInstance() || string.IsNullOrWhiteSpace( _pendingMapPackageId ) || MapInstance.IsLoaded )
			return;

		var packageId = _pendingMapPackageId;
		var revision = _pendingMapRevision;
		_mapUnloadObservedRevision = revision;
		MapInstance.MapName = packageId;
		_appliedMapRevision = revision;
		_pendingMapPackageId = "";
		_pendingMapRevision = 0;
		Log.Info( $"[GameManager] MapInstance.MapName set to '{packageId}' after unload for revision {revision}." );
	}

	private void ApplyActiveMap()
	{
		if ( Networking.IsHost || !EnsureMapInstance() || string.IsNullOrWhiteSpace( CurrentMapPackageId ) )
			return;

		var targetPackageId = CanonicalPackageId( CurrentMapPackageId );
		if ( !string.IsNullOrWhiteSpace( _pendingMapPackageId ) )
		{
			if ( _pendingMapRevision != ActiveMapRevision
				|| !string.Equals( CanonicalPackageId( _pendingMapPackageId ), targetPackageId, StringComparison.OrdinalIgnoreCase ) )
			{
				_pendingMapPackageId = CurrentMapPackageId;
				_pendingMapRevision = ActiveMapRevision;
			}

			return;
		}

		var currentMapMatches = string.Equals(
			CanonicalPackageId( MapInstance.MapName ),
			targetPackageId,
			StringComparison.OrdinalIgnoreCase );
		if ( _appliedMapRevision == ActiveMapRevision && currentMapMatches )
			return;

		var sameLoadedMap = MapInstance.IsLoaded && currentMapMatches;
		var forceReload = ActiveMapRevision > 1 && sameLoadedMap;
		BeginMapChange( CurrentMapPackageId, ActiveMapRevision, forceReload );
	}

	private void HandleMapLoaded()
	{
		TrySetupLoadedMapServer();
	}

	private void TrySetupLoadedMapServer()
	{
		if ( !Networking.IsHost || !EnsureMapInstance() || !MapInstance.IsLoaded )
			return;

		if ( _mapLoadRevision != ActiveMapRevision
			|| _appliedMapRevision != ActiveMapRevision
			|| _mapUnloadObservedRevision != ActiveMapRevision )
			return;

		var prefabPackageId = CanonicalPackageId( CurrentMapPackageId );
		if ( string.IsNullOrWhiteSpace( prefabPackageId ) )
			return;

		if ( _activeMapPrefab.IsValid() && string.Equals( _spawnedMapPackageId, prefabPackageId, StringComparison.OrdinalIgnoreCase ) )
			return;

		var reportedMapName = MapInstance.MapName;
		var reportedPackageId = CanonicalPackageId( reportedMapName );
		if ( !string.Equals( reportedPackageId, prefabPackageId, StringComparison.OrdinalIgnoreCase ) )
		{
			Log.Warning( $"[GameManager] MapInstance loaded '{reportedMapName}' while revision {ActiveMapRevision} targets '{prefabPackageId}'. Using the host-selected PackageId for prefab lookup." );
		}

		var prefab = FindMapPrefab( prefabPackageId );
		if ( !prefab.IsValid() )
		{
			Log.Error( $"[GameManager] Map '{reportedMapName}' loaded for '{prefabPackageId}', but the assigned prefab was not found in MapPrefabs." );
			return;
		}

		var mapRoot = prefab.Clone();
		if ( !mapRoot.IsValid() )
		{
			Log.Error( $"[GameManager] Failed to clone map prefab '{prefabPackageId}'." );
			return;
		}

		var mapInfo = mapRoot.Components.Get<MapInfo>( FindMode.EverythingInSelfAndDescendants );
		if ( !mapInfo.IsValid() )
		{
			Log.Error( $"[GameManager] Map prefab '{prefabPackageId}' has no MapInfo component." );
			mapRoot.Destroy();
			return;
		}

		mapInfo.ResolveSpawnPoints( mapRoot );
		if ( Gameplay.Instance.IsValid() )
			Gameplay.Instance.MapInfo = mapInfo;

		ConfigureNetworkHierarchy( mapRoot );
		if ( !mapRoot.NetworkSpawn() )
		{
			Log.Error( $"[GameManager] NetworkSpawn failed for map prefab '{prefabPackageId}'." );
			mapRoot.Destroy();
			return;
		}

		_activeMapPrefab = mapRoot;
		_spawnedMapPackageId = prefabPackageId;
		CurrentMapPackageId = prefabPackageId;
		_appliedMapRevision = ActiveMapRevision;

		GenerateNavMeshServer( ActiveMapRevision );

		RoundManager.Instance?.SetMapReadyServer();
		MarkHostMapReady();
		Log.Info( $"[GameManager] Map '{prefabPackageId}' revision {ActiveMapRevision} is ready (MapInstance: '{reportedMapName}')." );
	}

	private async void GenerateNavMeshServer( int mapRevision )
	{
		if ( !Networking.IsHost || _navMeshRevision == mapRevision )
			return;

		_navMeshRevision = mapRevision;
		Scene.NavMesh.IsEnabled = true;

		Log.Info( $"[GameManager] Generating navmesh for map revision {mapRevision}." );
		await Scene.NavMesh.Generate( Scene.PhysicsWorld );

		if ( !this.IsValid() )
			return;

		Log.Info( $"[GameManager] Navmesh for map revision {mapRevision} generated." );
	}

	private void HandleMapUnloaded()
	{
		if ( !Networking.IsHost )
			return;

		_mapUnloadObservedRevision = ActiveMapRevision;
		ResetHostMapReady();
		RoundManager.Instance?.SetMapLoadingServer();
	}

	private void ResetHostMapReady()
	{
		if ( !Networking.IsHost )
			return;

		IsMapReady = false;
		HostReadyMapRevision = -1;
		_hostReadyRpcRevision = -1;
	}

	private void MarkHostMapReady()
	{
		if ( !Networking.IsHost )
			return;

		IsMapReady = true;
		HostReadyMapRevision = ActiveMapRevision;
		_hostReadyRpcRevision = ActiveMapRevision;
		ReceiveHostMapReady( ActiveMapRevision );
	}

	[Rpc.Broadcast( NetFlags.Reliable | NetFlags.HostOnly )]
	private void ReceiveHostMapReady( int mapRevision )
	{
		_hostReadyRpcRevision = mapRevision;
	}

	private GameObject FindMapPrefab( string packageId )
	{
		var canonicalPackageId = CanonicalPackageId( packageId );
		return MapPrefabs.FirstOrDefault( prefab =>
			prefab.IsValid()
			&& string.Equals( CanonicalPackageId( prefab.Name ), canonicalPackageId, StringComparison.OrdinalIgnoreCase ) );
	}

	private bool EnsureMapInstance()
	{
		if ( MapInstance.IsValid() )
			return true;

		MapInstance = Scene.GetAllComponents<MapInstance>().FirstOrDefault();
		if ( MapInstance.IsValid() )
			return true;

		Log.Error( "[GameManager] The scene has no MapInstance component." );
		return false;
	}

	private void SubscribeMapCallbacks()
	{
		if ( _callbacksSubscribed || !EnsureMapInstance() )
			return;

		MapInstance.OnMapLoaded += HandleMapLoaded;
		MapInstance.OnMapUnloaded += HandleMapUnloaded;
		_callbacksSubscribed = true;
	}

	private void UnsubscribeMapCallbacks()
	{
		if ( !_callbacksSubscribed || !MapInstance.IsValid() )
			return;

		MapInstance.OnMapLoaded -= HandleMapLoaded;
		MapInstance.OnMapUnloaded -= HandleMapUnloaded;
		_callbacksSubscribed = false;
	}

	private static void ConfigureNetworkHierarchy( GameObject gameObject )
	{
		gameObject.NetworkMode = NetworkMode.Object;
		gameObject.Network.SetOrphanedMode( NetworkOrphaned.Host );

		foreach ( var child in gameObject.Children )
			ConfigureNetworkHierarchy( child );
	}

	private static string NormalizePackageId( string packageIdOrUrl )
	{
		var value = packageIdOrUrl?.Trim() ?? "";
		const string sboxUrlPrefix = "https://sbox.game/";

		if ( value.StartsWith( sboxUrlPrefix, StringComparison.OrdinalIgnoreCase ) )
			value = value[sboxUrlPrefix.Length..];

		value = value.Trim( '/', '\\' );
		return value.Replace( '/', '.' ).Replace( '\\', '.' );
	}

	private static string WithoutVersion( string packageId )
	{
		var versionSeparator = packageId.IndexOf( '#' );
		return versionSeparator >= 0 ? packageId[..versionSeparator] : packageId;
	}

	private static string CanonicalPackageId( string packageIdOrUrl )
	{
		return WithoutVersion( NormalizePackageId( packageIdOrUrl ) );
	}

	private void CreateSingleton()
	{
		if ( Instance is null )
			Instance = this;
	}

	private void RemoveSingleton()
	{
		if ( Instance == this )
			Instance = null;
	}

	protected override void OnAwake()
	{
		CreateSingleton();
		SubscribeMapCallbacks();
	}

	protected override void OnStart()
	{
		SubscribeMapCallbacks();

		if ( Networking.IsHost )
			LoadInitialMapServer();
	}

	protected override void OnUpdate()
	{
		ApplyPendingMapChange();
		ApplyActiveMap();

		if ( Networking.IsHost && EnsureMapInstance() )
		{
			if ( !MapInstance.IsLoaded && _mapLoadRevision == ActiveMapRevision )
				_mapUnloadObservedRevision = ActiveMapRevision;

			TrySetupLoadedMapServer();
		}

		if ( Networking.IsHost && IsMapVoteOpen && (MapVoteTimer || HaveAllPlayersVoted()) )
			FinishMapVote();
	}

	protected override void OnDestroy()
	{
		UnsubscribeMapCallbacks();
		_mapVotes.Clear();
		RemoveSingleton();
	}
}
