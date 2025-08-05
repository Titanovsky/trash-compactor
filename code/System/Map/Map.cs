public class Map()
{
    public Transform TrashmanTransform { get; set; } = new Transform(Vector3.Zero, Rotation.Identity);
    public Transform SurvivalTransform { get; set; } = new Transform(Vector3.Zero, Rotation.Identity);
    public Transform SpectatorTransform { get; set; } = new Transform(Vector3.Zero, Rotation.Identity);
}