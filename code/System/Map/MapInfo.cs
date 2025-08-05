namespace TrashCompactor.System;

public class MapInfo() : Component
{
    [Property] public Transform TrashmanTransform { get; set; } = new Transform(Vector3.Zero, Rotation.Identity);
    [Property] public Transform SurvivalTransform { get; set; } = new Transform(Vector3.Zero, Rotation.Identity);
    [Property] public Transform SpectatorTransform { get; set; } = new Transform(Vector3.Zero, Rotation.Identity);
}