using System;
using static Sandbox.Gizmo;

public sealed class Player : Component, Component.IDamageable
{
	public static Player Local { get; set; }

	[Property] public PlayerController Controller { get; private set; }
	[Property] public FpPlayerGrabber Grabber { get; private set; }
    [Property] public CameraComponent Camera { get; set; }

    public Role Role { get; private set; }
    [Sync] public RoleTrashCompactor RoleEnum { get; set; } = RoleTrashCompactor.Survival;

    public bool CanFly = false;
    public bool Godmode = false;

    public int Health = 0;
    public int Armor = 0;
    [Sync] public string Name { get; set; } = "";

    private Vector3 _jumpForceSpawn = new Vector3(.1f, .1f, .1f);

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

    public void Spawn()
    {
        if (IsProxy) return;

        WorldPosition = Gameplay.Instance.Spawn.WorldPosition;

        Controller.Jump(_jumpForceSpawn); // workaround cuz stuck
    }

    public void Prepare()
    {
        if (IsProxy) return;

        Log.Info($"Your role: {Role.Name}");
        Log.Info($"Check: {Role.Check("spectator")}");
    }

    public void ChangeRole(Role role)
	{
        if (IsProxy) return;

        Role = role;

		role.Setup(this);
    }

	public void ResetStats()
	{
        if (IsProxy) return;

        CanFly = false;
        Godmode = false;
    }

    public void OnDamage(in DamageInfo damage)
    {
        //Log.Info(damage.Attacker);
    }

    private void CreateSingleton()
    {
        if (IsProxy) return;

        Local = this;
    }

    private void RemoveSingleton()
    {
        if (Local != this) return;

        Local = null;
    }

    protected override void OnStart()
    {
        CreateSingleton(); // cuz network good works only OnStart
        ChangeRole(new Spectator());
        Prepare();

        Log.Info(Connection.Local.DisplayName);
        Name = Connection.Local.DisplayName;
    }

    protected override void OnDestroy()
    {
        RemoveSingleton();
    }
}
