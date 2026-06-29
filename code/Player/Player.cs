using System;

public sealed class Player : Component, Component.IDamageable
{
	public static Player Local { get; private set; }

	[ConCmd( "kill" )]
	public static void KillCommand()
	{
		var player = Local;
		if ( !player.IsValid() )
			return;

		if ( Networking.IsHost )
		{
			player.TryKillByConsoleServer( player, player.Network.Owner );
			return;
		}

		if ( !player.IsProxy )
			player.RequestKillRpc( player );
	}

	[Property, Description( "Reference to the PlayerController component that handles movement and physics." )] public PlayerController Controller { get; private set; }
	[Property, Description( "Reference to the FpPlayerGrabber component used by the Trashman to grab and throw trash props." )] public FpPlayerGrabber Grabber { get; private set; }
	[Property, Description( "Reference to the player's first-person camera component." )] public CameraComponent Camera { get; set; }
    [Property] public PlayerHudWorld WorldHud { get; set; }

    public Role Role { get; private set; } = Role.Create( RoleTrashCompactor.Survival );

	[Sync( SyncFlags.FromHost ), Change( nameof( OnRoleChanged ) )]
	public RoleTrashCompactor RoleEnum { get; private set; } = RoleTrashCompactor.Survival;

	[Sync( SyncFlags.FromHost ), Change( nameof( OnAliveChanged ) )]
	public bool IsAlive { get; private set; } = true;

    [Property] public Dresser Dresser { get; set; }

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
	private bool _lockSpectatorCamera;
	private Vector3 _spectatorCameraPosition;
	private Rotation _spectatorCameraRotation;

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

		if ( IsTrashman )
			return;

		var trash = FindTrash( damage.Attacker );
		if ( trash.IsValid() && trash.SafetyModeEnabled )
			return;

		if ( RoleEnum != RoleTrashCompactor.Survival )
			return;

		var amount = (int)MathF.Ceiling( damage.Damage );
		if ( amount <= 0 )
			return;

		Health = Math.Max( 0, Health - amount );

		if ( Health <= 0 )
			KillByTrashServer( GetDeathVelocity( damage ), trash?.LastTrashman );
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

		DestroyRagdollServer();

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

	public void KillByTrashServer( Vector3 deathVelocity, Player trashman )
	{
		if ( !Networking.IsHost )
			return;

		if ( !IsAlive || RoleEnum != RoleTrashCompactor.Survival )
			return;

		KillServer( deathVelocity, trashman, false );
	}

	private void KillServer( Vector3 deathVelocity, Player killer, bool isSuicide )
	{
		if ( !Networking.IsHost || !IsAlive || IsSpectator )
			return;

		var spectatorSpawn = GetSpectatorSpawn();
		var spectatorPosition = spectatorSpawn.IsValid() ? spectatorSpawn.WorldPosition : WorldPosition;
		var spectatorRotation = spectatorSpawn.IsValid() ? spectatorSpawn.WorldRotation : WorldRotation;
		var deathPosition = WorldPosition;

		CreateDeathRagdollServer( deathVelocity );
		RoundManager.Instance?.PlayPlayerDeathSoundServer( deathPosition );
		RoundManager.Instance?.PublishKillFeedServer( killer, this, isSuicide );

		IsAlive = false;
		RoleEnum = RoleTrashCompactor.Spectator;

		ApplyDeathPresentationRpc( spectatorPosition, spectatorRotation );
		ApplyRoleState();

		RoundManager.Instance?.CheckRoundEndServer();
	}

    [Rpc.Broadcast]
    private void DressForHost(Dresser dresser)
    {
        Log.Info($"Dresser from: {Rpc.Caller.DisplayName} - {dresser.Network.Owner.DisplayName}");

        Dresser.Clear();
        Dresser.Apply();
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

			if (IsTrashman)
            {
				Controller.RunSpeed = 280f;
                Controller.WalkSpeed = 190f;
            } else
			{
                Controller.RunSpeed = 170f;
                Controller.WalkSpeed = 170f;
            }
        }

		SetCharacterRenderersEnabled( IsAlive );
		SetWorldHudEnabled( IsAlive );

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

	[Rpc.Broadcast]
	private void ApplyDeathPresentationRpc( Vector3 spectatorPosition, Rotation spectatorRotation )
	{
		if ( Controller.IsValid() )
			Controller.Enabled = false;

		if ( Grabber.IsValid() )
			Grabber.Enabled = false;

		SetCharacterRenderersEnabled( false );
		SetWorldHudEnabled( false );

		if ( !IsProxy )
			LockSpectatorCamera( spectatorPosition, spectatorRotation );
	}

	[Rpc.Broadcast]
	private void ApplySpawnPresentationRpc()
	{
		_lockSpectatorCamera = false;
		SetCharacterRenderersEnabled( true );
		SetWorldHudEnabled( true );
		ApplyRoleState();
	}

	[Rpc.Broadcast]
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

	private void SetCharacterRenderersEnabled( bool enabled )
	{
		foreach ( var renderer in GameObject.Components.GetAll<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( renderer.IsValid() )
				renderer.Enabled = enabled;
		}

		foreach ( var renderer in GameObject.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( renderer.IsValid() )
				renderer.Enabled = enabled;
		}
	}

	private void SetWorldHudEnabled( bool enabled )
	{
		if ( WorldHud.IsValid() )
			WorldHud.Enabled = enabled;
	}

	protected override void OnStart()
	{
		CreateSingleton();
		ApplyRoleState();

		if ( !IsProxy )
		{
			Name = Connection.Local.DisplayName;
			Prepare();
			DressForHost(Dresser);
            SendNameEveryoneRpc(WorldHud, Name);
        }

		TryRegisterWithRoundManager();
	}

	protected override void OnUpdate()
	{
		TryRegisterWithRoundManager();
		UpdateSpectatorCamera();
	}

	protected override void OnDestroy()
	{
		RemoveSingleton();
	}

	[Rpc.Broadcast]
	private void SendNameEveryoneRpc(PlayerHudWorld worldHud, string name)
	{
        worldHud.Name = name;
    }

	[Rpc.Host]
	private void RequestRegisterPlayerRpc()
	{
		RoundManager.Instance?.RegisterPlayerServer( this );
	}

	[Rpc.Host]
	private void RequestKillRpc( Player player )
	{
		TryKillByConsoleServer( player, Rpc.Caller );
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

	private Vector3 GetDeathVelocity( in DamageInfo damage )
	{
		var attackerBody = damage.Attacker?.Components.Get<Rigidbody>();
		if ( attackerBody.IsValid() && attackerBody.Velocity.Length > 0.1f )
			return attackerBody.Velocity.Normal * 1000f;

		var body = Controller?.Body;
		if ( body.IsValid() && body.Velocity.Length > 0.1f )
			return body.Velocity.Normal * 1000f;

		return Vector3.Up * 1000f;
	}

	private Vector3 GetSelfDeathVelocity()
	{
		var body = Controller?.Body;
		if ( body.IsValid() && body.Velocity.Length > 0.1f )
			return body.Velocity.Normal * 1000f;

		return Vector3.Up * 1000f;
	}

	private void TryKillByConsoleServer( Player player, Connection caller )
	{
		if ( !Networking.IsHost || !player.IsValid() )
			return;

		if ( player != this )
			return;

		if ( caller != player.Network.Owner )
			return;

		if ( !player.IsAlive || player.IsSpectator )
			return;

		player.KillServer( player.GetSelfDeathVelocity(), null, true );
	}

	private Trash FindTrash( GameObject gameObject )
	{
		var current = gameObject;
		while ( current.IsValid() )
		{
			var trash = current.Components.Get<Trash>();
			if ( trash.IsValid() )
				return trash;

			current = current.Parent;
		}

		return null;
	}

	private GameObject GetSpectatorSpawn()
	{
		var spawns = Role.Create( RoleTrashCompactor.Spectator ).GetSpawns( MapInfo.Instance );
		return spawns.Count > 0 ? spawns.GetRandom() : null;
	}

	private void LockSpectatorCamera( Vector3 position, Rotation rotation )
	{
		_lockSpectatorCamera = true;
		_spectatorCameraPosition = position;
		_spectatorCameraRotation = rotation;
		ApplySpectatorCamera();
	}

	private void UpdateSpectatorCamera()
	{
		if ( !_lockSpectatorCamera || IsProxy || IsAlive || !IsSpectator )
			return;

		ApplySpectatorCamera();
	}

	private void ApplySpectatorCamera()
	{
		var camera = Camera.IsValid() ? Camera : Scene.Camera;
		if ( !camera.IsValid() )
			return;

		camera.WorldPosition = _spectatorCameraPosition;
		camera.WorldRotation = _spectatorCameraRotation;

		if ( Controller.IsValid() )
			Controller.EyeAngles = _spectatorCameraRotation.Angles();
	}

	private void ApplyRagdollVelocity( Vector3 deathVelocity )
	{
		if ( !_ragdoll.IsValid() || deathVelocity.Length <= 0.1f )
			return;

		ApplyRagdollVelocityRecursive( _ragdoll, deathVelocity );
	}

	private void ApplyRagdollVelocityRecursive( GameObject gameObject, Vector3 deathVelocity )
	{
		var body = gameObject.Components.Get<Rigidbody>();
		if ( body.IsValid() )
		{
			body.Velocity = deathVelocity;
			body.AngularVelocity = Vector3.Random * 8f;
		}

		foreach ( var child in gameObject.Children )
			ApplyRagdollVelocityRecursive( child, deathVelocity );
	}

	private void CreateDeathRagdollServer( Vector3 deathVelocity )
	{
		if ( !Networking.IsHost || !Controller.IsValid() )
			return;

		DestroyRagdollServer();

		_ragdoll = Controller.CreateRagdoll( string.IsNullOrWhiteSpace( Name ) ? "player_ragdoll" : $"{Name}_ragdoll" );
		if ( !_ragdoll.IsValid() )
			return;

		ApplyRagdollVelocity( deathVelocity );
		_ragdoll.Network.SetOrphanedMode( NetworkOrphaned.Host );
		_ragdoll.NetworkSpawn();
	}

	private void DestroyRagdollServer()
	{
		if ( !Networking.IsHost || !_ragdoll.IsValid() )
			return;

		_ragdoll.Destroy();
		_ragdoll = null;
	}
}
