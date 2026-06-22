using System;

public sealed class Player : Component, Component.IDamageable
{
	public static Player Local { get; private set; }

	[Property, Description( "Reference to the PlayerController component that handles movement and physics." )] public PlayerController Controller { get; private set; }
	[Property, Description( "Reference to the FpPlayerGrabber component used by the Trashman to grab and throw trash props." )] public FpPlayerGrabber Grabber { get; private set; }
	[Property, Description( "Reference to the player's first-person camera component." )] public CameraComponent Camera { get; set; }

	public Role Role { get; private set; } = Role.Create( RoleTrashCompactor.Survival );

	[Sync( SyncFlags.FromHost ), Change( nameof( OnRoleChanged ) )]
	public RoleTrashCompactor RoleEnum { get; private set; } = RoleTrashCompactor.Survival;

	[Sync( SyncFlags.FromHost ), Change( nameof( OnAliveChanged ) )]
	public bool IsAlive { get; private set; } = true;

	[Sync] public string Name { get; set; } = "";

	public bool CanFly { get; private set; }
	public bool Godmode { get; private set; }

	[Sync( SyncFlags.FromHost )]
	public int MaxHealth { get; private set; } = 100;

	[Sync( SyncFlags.FromHost )]
	public int Health { get; private set; } = 100;

	[Sync( SyncFlags.FromHost )]
	public bool HasRoundSpawn { get; private set; }

	public int Armor = 0;

	private readonly Vector3 _jumpForceSpawn = new( .1f, .1f, .1f );
	private GameObject _ragdoll;
	private TimeUntil _nextRegistrationRequest;

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
		if ( !Networking.IsHost )
			return;

		if ( !IsAlive || Godmode )
			return;

		if ( RoleEnum != RoleTrashCompactor.Survival )
			return;

		var amount = (int)MathF.Ceiling( damage.Damage );
		if ( amount <= 0 )
			return;

		Health = Math.Max( 0, Health - amount );

		if ( Health <= 0 )
			KillByTrashServer();
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
		Health = MaxHealth;

		SpawnForCurrentRoleServer();
		ApplySpawnPresentationRpc();
		ApplyRoleState();
	}

	public void SpawnForCurrentRoleServer()
	{
		if ( !Networking.IsHost )
			return;

		var spawns = Role.Create( RoleEnum ).GetSpawns( MapInfo.Instance );
		var spawn = spawns.Count > 0 ? spawns.GetRandom() : null;

		if ( spawn.IsValid() )
		{
			WorldPosition = spawn.WorldPosition;
			HasRoundSpawn = true;
			ApplySpawnTransformRpc( spawn.WorldPosition, spawn.WorldRotation.Angles().yaw );
		}
		else
		{
			HasRoundSpawn = false;
			Log.Warning( $"[Player] No spawn point found for role {RoleEnum}. Keeping current fallback position." );
		}

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

		if ( spawn.IsValid() && IsAlive && ( IsSurvival || IsTrashman ) )
			ApplySpawnEyeYaw( spawn.WorldRotation.Angles().yaw );
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

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	private void ApplySpawnTransformRpc( Vector3 position, float yaw )
	{
		WorldPosition = position;

		if ( Controller.IsValid() && Controller.Body.IsValid() )
		{
			Controller.Body.Velocity = Vector3.Zero;
			Controller.Body.AngularVelocity = Vector3.Zero;
		}

		ApplySpawnEyeYaw( yaw );
	}

	private void ApplySpawnEyeYaw( float yaw )
	{
		if ( !Controller.IsValid() )
			return;

		Controller.EyeAngles = Controller.EyeAngles.WithYaw( yaw );
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

		TryRegisterWithRoundManager();
	}

	protected override void OnUpdate()
	{
		TryRegisterWithRoundManager();
	}

	protected override void OnDestroy()
	{
		RemoveSingleton();
	}

	[Rpc.Host( NetFlags.Reliable )]
	private void RequestRegisterPlayerRpc()
	{
		RoundManager.Instance?.RegisterPlayerServer( this );
	}

	private void TryRegisterWithRoundManager()
	{
		if ( HasRoundSpawn || !_nextRegistrationRequest )
			return;

		_nextRegistrationRequest = 0.25f;

		if ( Networking.IsHost )
		{
			RoundManager.Instance?.RegisterPlayerServer( this );
			return;
		}

		if ( !IsProxy )
			RequestRegisterPlayerRpc();
	}
}
