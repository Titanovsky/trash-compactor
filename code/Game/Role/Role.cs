using System;

public abstract class Role
{
	public virtual string Name { get; set; } = "none";
	public virtual Transform SpawnTransform { get; set; } = new Transform(Vector3.Zero, Rotation.Identity);

	public virtual void Setup(Player player) 
    { 
        player.WorldPosition = SpawnTransform.Position;
    }

	public virtual bool Check(Type type)
	{
        return type == GetType();
    }

    public virtual bool Check(string type)
    {
        return type.ToLower() == GetType().ToString().ToLower();
    }
}
