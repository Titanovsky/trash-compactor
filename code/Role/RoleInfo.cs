using Sandbox;

public class RoleInfo : IRoleInfo
{
	public Transform TransformSpawn { get; set; } = new( Vector3.Zero, Rotation.Identity );

	public RoleInfo(Role role, Transform transformSpawn )
	{
		
	}

	public static void Get(Role Role)
	{

	}
}
