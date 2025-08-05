using Sandbox;
using System;

public abstract class Role
{
	public virtual string Name { get; set; }
	public virtual Transform SpawnTransform { get; set; } = new Transform(Vector3.Zero, Rotation.Identity);

	public virtual void Setup(Player player) { }
}
