namespace TrashCompactor.System;

public class MapInfo() : Component
{
    public static MapInfo Instance { get; private set; }

    [Property] public List<GameObject> SpawnTrashmans { get; set; } = new();
    [Property] public List<GameObject> SpawnSurvivals { get; set; } = new();
    [Property] public List<GameObject> SpawnSpectors { get; set; } = new();
    [Property] public List<GameObject> TrashPropSpawns { get; set; } = new();
    [Property] public List<GameObject> SoloTrashPropSpawns { get; set; } = new();

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
