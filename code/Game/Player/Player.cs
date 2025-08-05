public sealed class Player : Component, Component.IDamageable
{
	public static Player Local { get; set; }

	public Role Role { get; set; }
	public PlayerController Controller { get; set; }
	public FpPlayerGrabber Grabber { get; set; }

	public bool CanFly = false;
    public bool Godmode = false;

    //public void SetRole( RoleBase role )
    //{
    //	if (!role.IsValid()) return;

    //	Role = role;

    //	Log.Info( $"The Player {GameObject} has the role: {role.Name} ({role.Id})" );
    //}

    //public void SetRole( string id )
    //{
    //	SetRole( RoleManager.Get( id ) );
    //}

    //public void SetupRole()
    //{
    //	Log.Info( $"IsValid Role {Role} : {Role.IsValid()}" );

    //	Transform transform = new( Vector3.Zero, Rotation.Identity );

    //	if (Role.Check(TrashCompactorRole.Spectator))
    //	{
    //		Log.Info("ds");
    //	} else if (Role.Check(TrashCompactorRole.Soccer))
    //	{
    //		Log.Info( "b" );
    //	} else if ( Role.Check(TrashCompactorRole.Trashman))
    //	{
    //		Log.Info( "c" );
    //	}

    //	//WorldPosition = transform.Position;
    //	//WorldRotation = transform.Rotation;
    //}

    public void Prepare()
    {
        Log.Info($"Your role: {Role.Name}");
        Log.Info($"Check: {Role.Check("spectator")}");
    }

    public void ChangeRole(Role role)
	{
		Role = role;

		role.Setup(this);
	}

	public void ResetStats()
	{
		CanFly = false;
        Godmode = false;

    }

	protected override void OnAwake()
	{
		if ( Local.IsValid() ) return;

		Local = this; //todo to network
	}

    protected override void OnStart()
    {
        ChangeRole(new Spectator());
        Prepare();
    }

	public void OnDamage( in DamageInfo damage )
	{
		Log.Info( damage.Attacker );
	}
}
