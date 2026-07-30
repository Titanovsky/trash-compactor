using System;
using System.Linq;

public sealed class GameManager : Component
{
	public const string InitialMapPackageId = "dmgames.trashcompactor";

	public static GameManager Instance { get; private set; }

	[Property, Description( "The MapInstance that loads the selected cloud map." )]
	public MapInstance MapInstance { get; set; }

	[Property, Description( "Map gameplay prefabs. Each prefab name must be its full package id, for example author.name." )]
	public List<GameObject> MapPrefabs { get; set; } = new();

	[Sync( SyncFlags.FromHost ), Change( nameof( OnCurrentMapPackageIdChanged ) )]
	public string CurrentMapPackageId { get; private set; } = "";

	[Sync( SyncFlags.FromHost )]
	public bool IsMapReady { get; private set; }

	private GameObject _activeMapPrefab;
	private string _spawnedMapPackageId = "";
	private bool _callbacksSubscribed;

	public bool LoadMapServer( string packageIdOrUrl )
	{
		if ( !Networking.IsHost )
			return false;

		var packageId = NormalizePackageId( packageIdOrUrl );
		if ( !Package.TryParseIdent( packageId, out _ ) )
		{
			Log.Error( $"[GameManager] Invalid map package id: '{packageIdOrUrl}'." );
			return false;
		}

		var prefabPackageId = WithoutVersion( packageId );
		if ( !FindMapPrefab( prefabPackageId ).IsValid() )
		{
			Log.Error( $"[GameManager] No map prefab named '{prefabPackageId}' was assigned to MapPrefabs." );
			return false;
		}

		if ( !EnsureMapInstance() )
			return false;

		BeginMapLoadingServer();
		CurrentMapPackageId = packageId;
		MapInstance.MapName = packageId;

		if ( MapInstance.IsLoaded )
			HandleMapLoaded();

		return true;
	}

	private void BeginMapLoadingServer()
	{
		IsMapReady = false;
		_spawnedMapPackageId = "";

		RoundManager.Instance?.SetMapLoadingServer();
		MapInfo.Instance?.ReleaseForMapUnloadServer();
		if ( Gameplay.Instance.IsValid() )
			Gameplay.Instance.MapInfo = null;

		if ( _activeMapPrefab.IsValid() )
			_activeMapPrefab.Destroy();

		_activeMapPrefab = null;
	}

	private void HandleMapLoaded()
	{
		if ( !Networking.IsHost || !EnsureMapInstance() || !MapInstance.IsLoaded )
			return;

		var loadedPackageId = NormalizePackageId( MapInstance.MapName );
		var prefabPackageId = WithoutVersion( loadedPackageId );

		if ( _activeMapPrefab.IsValid() && string.Equals( _spawnedMapPackageId, prefabPackageId, StringComparison.OrdinalIgnoreCase ) )
			return;

		var prefab = FindMapPrefab( prefabPackageId );
		if ( !prefab.IsValid() )
		{
			Log.Error( $"[GameManager] Map '{loadedPackageId}' loaded, but prefab '{prefabPackageId}' is missing from MapPrefabs." );
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
		CurrentMapPackageId = loadedPackageId;
		IsMapReady = true;

		RoundManager.Instance?.SetMapReadyServer();
		Log.Info( $"[GameManager] Map '{loadedPackageId}' and prefab '{prefabPackageId}' are ready." );
	}

	private void HandleMapUnloaded()
	{
		if ( !Networking.IsHost )
			return;

		BeginMapLoadingServer();
	}

	private void OnCurrentMapPackageIdChanged( string oldValue, string newValue )
	{
		if ( Networking.IsHost || string.IsNullOrWhiteSpace( newValue ) || !EnsureMapInstance() )
			return;

		if ( !string.Equals( MapInstance.MapName, newValue, StringComparison.OrdinalIgnoreCase ) )
			MapInstance.MapName = newValue;
	}

	private GameObject FindMapPrefab( string packageId )
	{
		return MapPrefabs.FirstOrDefault( prefab =>
			prefab.IsValid()
			&& string.Equals( prefab.Name, packageId, StringComparison.OrdinalIgnoreCase ) );
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

		if ( !Networking.IsHost )
			return;

		if ( IsMapReady && string.Equals( WithoutVersion( CurrentMapPackageId ), InitialMapPackageId, StringComparison.OrdinalIgnoreCase ) )
			return;

		LoadMapServer( InitialMapPackageId );
	}

	protected override void OnDestroy()
	{
		UnsubscribeMapCallbacks();
		RemoveSingleton();
	}
}
