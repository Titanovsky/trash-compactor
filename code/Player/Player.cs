using TrashCompactor.System;

public sealed class Player : Component, Component.IDamageable
{
	public static Player Local { get; private set; }

	[Property] public PlayerController Controller { get; private set; }
	[Property] public FpPlayerGrabber Grabber { get; private set; }
	[Property] public CameraComponent Camera { get; set; }

	public Role Role { get; private set; } = Role.Create( RoleTrashCompactor.Survival );

	[Sync( SyncFlags.FromHost ), Change( nameof( OnRoleChanged ) )]
	public RoleTrashCompactor RoleEnum { get; private set; } = RoleTrashCompactor.Survival;

	[Sync( SyncFlags.FromHost ), Change( nameof( OnAliveChanged ) )]
	public bool IsAlive { get; private set; } = true;

	[Sync] public string Name { get; set; } = "";

	public bool CanFly { get; private set; }
	public bool Godmode { get; private set; }

	public int Health = 0;
	public int Armor = 0;

	private readonly Vector3 _jumpForceSpawn = new( .1f, .1f, .1f );
	private GameObject _ragdoll;

	public bool IsTrashman => RoleEnum == RoleTrashCompactor.Trashman;
	public bool IsSurvival => RoleEnum == RoleTrashCompactor.Survival;
	public bool IsSpectator => RoleEnum == RoleTrashCompactor.Spectator;

	public bool CanUseTrashmanTools =>
		IsAlive
		&& IsTrashman
		&& RoundManager.Instance.IsValid()
		&& RoundManager.Instance.State == RoundState.Started;

	public void Spawn()
	{
		SpawnForCurrentRoleServer();
	}

	public void Prepare()
	{
		if ( IsProxy )
			return;

		Log.Info( $"Your role: {Role.Name}" );
	}

	public void ResetStats()
	{
		CanFly = false;
		Godmode = false;
	}

	public void OnDamage( in DamageInfo damage )
	{
	}

	public void SetRoleServer( RoleTrashCompactor role )
	{
		if ( !Networking.IsHost )
			return;

		RoleEnum = role;
		ApplyRoleState();
	}

	public void RespawnForRoundServer( RoleTrashCompactor role )
	{
		if ( !Networking.IsHost )
			return;

		IsAlive = role != RoleTrashCompactor.Spectator;
		RoleEnum = role;

		SpawnForCurrentRoleServer();
		ApplySpawnPresentationRpc();
		ApplyRoleState();
	}

	public void SpawnForCurrentRoleServer()
	{
		if ( !Networking.IsHost )
			return;

		var spawns = Role.Create( RoleEnum ).GetSpawns( MapInfo.Instance );
		if ( spawns.Count > 0 )
			WorldPosition = spawns.GetRandom().WorldPosition;

		if ( Controller.IsValid() )
		{
			Controller.Enabled = IsAlive && RoleEnum != RoleTrashCompactor.Spectator;
			Controller.Jump( _jumpForceSpawn );

			if ( Controller.Body.IsValid() )
			{
				Controller.Body.Velocity = Vector3.Zero;
				Controller.Body.AngularVelocity = Vector3.Zero;
			}
		}
	}

	public void KillByTrashServer()
	{
		if ( !Networking.IsHost )
			return;

		if ( !IsAlive || RoleEnum != RoleTrashCompactor.Survival )
			return;

		IsAlive = false;
		RoleEnum = RoleTrashCompactor.Spectator;

		ApplyDeathPresentationRpc();
		ApplyRoleState();

		RoundManager.Instance?.CheckRoundEndServer();
	}

	private void OnRoleChanged( RoleTrashCompactor oldRole, RoleTrashCompactor newRole )
	{
		ApplyRoleState();
	}

	private void OnAliveChanged( bool oldValue, bool newValue )
	{
		ApplyRoleState();
	}

	private void ApplyRoleState()
	{
		Role = Role.Create( RoleEnum );
		CanFly = IsSpectator;
		Godmode = IsSpectator;

		var canMove = IsAlive && !IsSpectator;
		var canGrab = IsAlive && IsTrashman;

		if ( Controller.IsValid() )
		{
			Controller.Enabled = canMove;

			if ( Controller.Renderer.IsValid() )
				Controller.Renderer.Enabled = IsAlive;
		}

		if ( Grabber.IsValid() )
			Grabber.Enabled = canGrab;
	}

	private void CreateSingleton()
	{
		if ( IsProxy )
			return;

		Local = this;
	}

	private void RemoveSingleton()
	{
		if ( Local != this )
			return;

		Local = null;
	}

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	private void ApplyDeathPresentationRpc()
	{
		if ( _ragdoll.IsValid() )
			_ragdoll.Destroy();

		if ( Controller.IsValid() )
			_ragdoll = Controller.CreateRagdoll( string.IsNullOrWhiteSpace( Name ) ? "player_ragdoll" : $"{Name}_ragdoll" );

		if ( Controller.IsValid() )
		{
			Controller.Enabled = false;

			if ( Controller.Renderer.IsValid() )
				Controller.Renderer.Enabled = false;
		}

		if ( Grabber.IsValid() )
			Grabber.Enabled = false;
	}

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	private void ApplySpawnPresentationRpc()
	{
		if ( _ragdoll.IsValid() )
		{
			_ragdoll.Destroy();
			_ragdoll = null;
		}

		ApplyRoleState();
	}

	protected override void OnStart()
	{
		CreateSingleton();
		ApplyRoleState();

		if ( !IsProxy )
		{
			Name = Connection.Local.DisplayName;
			Prepare();
		}

		RoundManager.Instance?.RegisterPlayerServer( this );
	}

	protected override void OnDestroy()
	{
		RemoveSingleton();
	}
}
