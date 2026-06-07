namespace TrashCompactor.System;

public class MapInfo() : Component
{
    public static MapInfo Instance { get; private set; }

    [Property, Description( "Spawn points used for players assigned the Trashman role." )] public List<GameObject> SpawnTrashmans { get; set; } = new();
    [Property, Description( "Spawn points used for players assigned the Survival role." )] public List<GameObject> SpawnSurvivals { get; set; } = new();
    [Property, Description( "Spawn points used for players assigned the Spectator role." )] public List<GameObject> SpawnSpectors { get; set; } = new();
    [Property, Description( "Spawn points used to place trash props at the start of a regular round." )] public List<GameObject> TrashPropSpawns { get; set; } = new();
    [Property, Description( "Spawn points used for automatically spawning trash props during a solo round." )] public List<GameObject> SoloTrashPropSpawns { get; set; } = new();

    private void CreateSingleton()
    {
        if (Instance == null)
            Instance = this;
    }

    private void RemoveSingleton()
    {
        if (Instance != null)
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
