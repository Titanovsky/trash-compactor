using System;

public abstract class Role
{
	public virtual string Name { get; set; } = "none";
	public virtual List<GameObject> Spawns { get; set; }

	public virtual void Setup(Player player) 
    { 
        //player.WorldPosition = SpawnTransform.Position;
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
