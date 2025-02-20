using Sandbox;

public sealed class Player : Component, Component.IDamageable
{
	public static Player Instance { get; set; }

	public Role Role { get; set; }

	protected override void OnAwake()
	{
		if ( Instance.IsValid() ) return;

		Instance = this;
	}

	public void OnDamage( in DamageInfo damage )
	{
		Log.Info( damage.Attacker );
	}

	public void SetRole( Role role )
	{
		if (!role.IsValid()) return;

		Role = role;

		Log.Info( $"The Player {GameObject} has the role: {role.Name} ({role.Id})" );
	}

	public void SetRole( string id )
	{
		SetRole( RoleHandler.Get( id ) );
	}

	public void SetupRole()
	{
		Log.Info( $"IsValid Role {Role} : {Role.IsValid()}" );

		Transform transform = new( Vector3.Zero, Rotation.Identity );

		if (Role.Check(GameRoles.Spectator))
		{
			Log.Info("ds");
		} else if (Role.Check(GameRoles.Soccer))
		{
			Log.Info( "b" );
		} else if ( Role.Check(GameRoles.Trashman))
		{
			Log.Info( "c" );
		}

		WorldPosition = transform.Position;
		WorldRotation = transform.Rotation;
	}
}
