
using System;
using System.Linq;

public class MapInfo() : Component
{
    public static MapInfo Instance { get; private set; }

    [Property, Description( "Spawn points used for players assigned the Trashman role." )] public List<GameObject> SpawnTrashmans { get; set; } = new();
    [Property, Description( "Spawn points used for players assigned the Survival role." )] public List<GameObject> SpawnSurvivals { get; set; } = new();
    [Property, Description( "Spawn points used for players assigned the Spectator role." )] public List<GameObject> SpawnSpectors { get; set; } = new();
    [Property, Description( "Spawn points used to place trash props at the start of a regular round." )] public List<GameObject> TrashPropSpawns { get; set; } = new();
    [Property, Description( "Spawn points used for automatically spawning trash props during a solo round." )] public List<GameObject> SoloTrashPropSpawns { get; set; } = new();

	public void ResolveSpawnPoints( GameObject mapRoot )
	{
		if ( !mapRoot.IsValid() )
			return;

		var hierarchy = EnumerateHierarchy( mapRoot ).ToList();
		SpawnTrashmans = ResolveList( SpawnTrashmans, hierarchy, name => name.StartsWith( "Spawn Trashman", StringComparison.OrdinalIgnoreCase ) );
		SpawnSurvivals = ResolveList( SpawnSurvivals, hierarchy, name => name.StartsWith( "Spawn Surv", StringComparison.OrdinalIgnoreCase ) );
		SpawnSpectors = ResolveList( SpawnSpectors, hierarchy, name => name.StartsWith( "Spawn Spectator", StringComparison.OrdinalIgnoreCase ) );
		TrashPropSpawns = ResolveList( TrashPropSpawns, hierarchy, name => name.StartsWith( "Spawn Prop", StringComparison.OrdinalIgnoreCase ) && !name.Equals( "Spawn Props", StringComparison.OrdinalIgnoreCase ) );
		SoloTrashPropSpawns = ResolveList( SoloTrashPropSpawns, hierarchy, name => name.Equals( "Props Spawn Point", StringComparison.OrdinalIgnoreCase ) || name.Equals( "Solo Spawner Props", StringComparison.OrdinalIgnoreCase ) );
	}

	private static List<GameObject> ResolveList( List<GameObject> configured, List<GameObject> hierarchy, Func<string, bool> matchesName )
	{
		configured ??= new();
		var valid = configured.Where( gameObject => gameObject.IsValid() ).ToList();
		if ( configured.Count > 0 && valid.Count == configured.Count )
			return valid;

		var discovered = hierarchy
			.Where( gameObject => gameObject.IsValid() && matchesName( gameObject.Name ?? "" ) )
			.ToList();

		return discovered.Count > 0 ? discovered : valid;
	}

	private static IEnumerable<GameObject> EnumerateHierarchy( GameObject root )
	{
		yield return root;

		foreach ( var child in root.Children )
		{
			foreach ( var descendant in EnumerateHierarchy( child ) )
				yield return descendant;
		}
	}

	public void ReleaseForMapUnloadServer()
	{
		if ( Networking.IsHost )
			RemoveSingleton();
	}

    private void CreateSingleton()
    {
        if (Instance == null)
            Instance = this;
    }

    private void RemoveSingleton()
    {
        if (Instance == this)
            Instance = null;
    }

    protected override void OnDestroy()
    {
        RemoveSingleton();
    }

    protected override void OnAwake()
    {
        CreateSingleton();
    }
}
