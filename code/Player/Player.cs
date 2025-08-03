using Sandbox;

public sealed class Player : Component, Component.IDamageable
{
	public static Player Local { get; set; }

	public Role Role { get; set; }
	public PlayerController Controller { get; set; }
	public FpPlayerGrabber Grabber { get; set; }

	public void SetRole( Role role )
	{
		if (!role.IsValid()) return;

		Role = role;

		Log.Info( $"The Player {GameObject} has the role: {role.Name} ({role.Id})" );
	}

	public void SetRole( string id )
	{
		SetRole( RoleManager.Get( id ) );
	}

	public void SetupRole()
	{
		Log.Info( $"IsValid Role {Role} : {Role.IsValid()}" );

		Transform transform = new( Vector3.Zero, Rotation.Identity );

		if (Role.Check(TrashCompactorRole.Spectator))
		{
			Log.Info("ds");
		} else if (Role.Check(TrashCompactorRole.Soccer))
		{
			Log.Info( "b" );
		} else if ( Role.Check(TrashCompactorRole.Trashman))
		{
			Log.Info( "c" );
		}

		//WorldPosition = transform.Position;
		//WorldRotation = transform.Rotation;
	}

	protected override void OnAwake()
	{
		if ( Local.IsValid() ) return;

		Local = this;
	}

	public void OnDamage( in DamageInfo damage )
	{
		Log.Info( damage.Attacker );
	}
}
