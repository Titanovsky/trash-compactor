using System;
using System.Collections.Generic;
using Sandbox;

public sealed class NpcSurvivor : Component, Component.IDamageable
{
	private static readonly List<NpcSurvivor> _all = new();

	public static IReadOnlyList<NpcSurvivor> All => _all;

	[Property, Description( "Skinned model renderer used to drive the citizen animation graph." )]
	public SkinnedModelRenderer Renderer { get; set; }

	[Property, Description( "Navigation agent that moves this NPC across the generated navmesh. Only active on the host." )]
	public NavMeshAgent Agent { get; set; }

	[Property, Group( "Wander" ), Description( "Maximum distance in units from the current position when picking the next random wander destination." )]
	public float WanderRadius { get; set; } = 300f;

	[Property, Group( "Wander" ), Description( "Minimum delay in seconds before the NPC picks a new random wander destination." )]
	public float WanderIntervalMin { get; set; } = 0.5f;

	[Property, Group( "Wander" ), Description( "Maximum delay in seconds before the NPC picks a new random wander destination." )]
	public float WanderIntervalMax { get; set; } = 1f;

	[Property, Group( "Health" ), Description( "Health the NPC starts a round with." )]
	public int MaxHealth { get; set; } = 100;

	[Property, Group( "Death" ), Description( "Multiplier applied to the trash impact impulse when the NPC turns into a corpse." )]
	public float CorpseImpulseScale { get; set; } = 1f;

	[Property, Group( "Death" ), Description( "Minimum impulse strength applied to the corpse so it always reacts visibly to the hit." )]
	public float CorpseMinImpulse { get; set; } = 400f;

	[Property, Group( "Animation" ), Description( "How many times per second the host replicates animation state to clients." )]
	public float AnimationUpdateRate { get; set; } = 10f;

	[Property, Group( "Animation" ), Description( "How fast the NPC model turns towards its movement direction, in degrees per second." )]
	public float TurnSpeed { get; set; } = 540f;

	[Sync( SyncFlags.FromHost )] public string DisplayName { get; set; } = "";
	[Sync( SyncFlags.FromHost )] public bool IsAlive { get; private set; } = true;
	[Sync( SyncFlags.FromHost )] public int Health { get; private set; } = 100;

	private TimeUntil _nextWander;
	private TimeUntil _nextAnimationUpdate;
	private Vector3 _animationVelocity;
	private bool _isCorpse;

	public void SetupServer( string displayName )
	{
		if ( !Networking.IsHost )
			return;

		DisplayName = displayName;
		Health = MaxHealth;
		IsAlive = true;
		_isCorpse = false;
		_nextWander = 0f;
		_nextAnimationUpdate = 0f;
	}

	public void OnDamage( in DamageInfo damage )
	{
		if ( !Networking.IsHost || !IsAlive || _isCorpse )
			return;

		var amount = (int)MathF.Ceiling( damage.Damage );
		if ( amount <= 0 )
			return;

		Health = Math.Max( 0, Health - amount );

		if ( Health > 0 )
			return;

		var direction = GetImpactDirection( damage );
		KillServer( damage.Position, direction );
	}

	public void KillServer( Vector3 impactPosition, Vector3 impactForce )
	{
		if ( !Networking.IsHost || !IsAlive || _isCorpse )
			return;

		IsAlive = false;
		Health = 0;
		_isCorpse = true;

		StopAgentServer();
		CreateCorpseServer( impactPosition, impactForce );

		RoundManager.Instance?.PlayPlayerDeathSoundServer( WorldPosition );
		RoundManager.Instance?.PublishNpcKillFeedServer( GetName() );
		RoundManager.Instance?.CheckRoundEndServer();
	}

	public string GetName()
	{
		return string.IsNullOrWhiteSpace( DisplayName ) ? "Survivor" : DisplayName;
	}

	private Vector3 GetImpactDirection( in DamageInfo damage )
	{
		var attackerBody = damage.Attacker?.Components.Get<Rigidbody>();
		if ( attackerBody.IsValid() && attackerBody.Velocity.Length > 0.1f )
			return attackerBody.Velocity;

		var fallback = WorldPosition - damage.Position;
		return fallback.Length > 0.1f ? fallback.Normal * CorpseMinImpulse : Vector3.Up * CorpseMinImpulse;
	}

	private void StopAgentServer()
	{
		if ( !Agent.IsValid() )
			return;

		Agent.Stop();
		Agent.Destroy();
		Agent = null;
	}

	private void CreateCorpseServer( Vector3 impactPosition, Vector3 impactForce )
	{
		if ( !Networking.IsHost )
			return;

		var body = GameObject.Components.GetOrCreate<Rigidbody>();
		if ( !body.IsValid() )
			return;

		body.Enabled = true;
		body.Velocity = Vector3.Zero;
		body.AngularVelocity = Vector3.Random * 8f;

		var impulse = impactForce * CorpseImpulseScale;
		if ( impulse.Length < CorpseMinImpulse )
			impulse = (impulse.Length > 0.1f ? impulse.Normal : Vector3.Up) * CorpseMinImpulse;

		body.ApplyImpulseAt( impactPosition, impulse );

		ApplyDeathPresentationRpc();
	}

	[Rpc.Broadcast( NetFlags.Reliable )]
	private void ApplyDeathPresentationRpc()
	{
		if ( Agent.IsValid() )
			Agent.Enabled = false;

		if ( !Renderer.IsValid() )
			return;

		Renderer.Set( "move_speed", 0f );
		Renderer.Set( "move_groundspeed", 0f );
		Renderer.Set( "move_x", 0f );
		Renderer.Set( "move_y", 0f );
		Renderer.Set( "move_z", 0f );
		Renderer.Set( "b_grounded", false );
	}

	[Rpc.Broadcast]
	private void ApplyAnimationRpc( Vector3 velocity )
	{
		_animationVelocity = velocity;
		ApplyAnimation( velocity );
	}

	private void ApplyAnimation( Vector3 velocity )
	{
		if ( !Renderer.IsValid() )
			return;

		var rotation = Renderer.WorldRotation;
		var forward = rotation.Forward.Dot( velocity );
		var sideward = rotation.Right.Dot( velocity );
		var moveAngle = MathF.Atan2( sideward, forward ).RadianToDegree().NormalizeDegrees();

		Renderer.Set( "move_direction", moveAngle );
		Renderer.Set( "move_speed", velocity.Length );
		Renderer.Set( "move_groundspeed", velocity.WithZ( 0f ).Length );
		Renderer.Set( "move_x", forward );
		Renderer.Set( "move_y", sideward );
		Renderer.Set( "move_z", velocity.z );
		Renderer.Set( "b_grounded", true );
	}

	private void UpdateWanderServer()
	{
		if ( !Networking.IsHost || !IsAlive || _isCorpse || !Agent.IsValid() )
			return;

		if ( !_nextWander )
			return;

		_nextWander = Game.Random.Float( MathF.Min( WanderIntervalMin, WanderIntervalMax ), MathF.Max( WanderIntervalMin, WanderIntervalMax ) );

		if ( !Scene.NavMesh.IsEnabled )
			return;

		var target = Scene.NavMesh.GetRandomPoint( WorldPosition, WanderRadius );
		if ( !target.HasValue )
			return;

		Agent.MoveTo( target.Value );
	}

	private void UpdateAnimationServer()
	{
		if ( !Networking.IsHost || _isCorpse )
			return;

		var velocity = IsAlive && Agent.IsValid() ? Agent.Velocity : Vector3.Zero;
		_animationVelocity = velocity;
		ApplyAnimation( velocity );

		if ( !_nextAnimationUpdate )
			return;

		_nextAnimationUpdate = 1f / MathF.Max( 1f, AnimationUpdateRate );
		ApplyAnimationRpc( velocity );
	}

	private void UpdateFacingServer()
	{
		if ( !Networking.IsHost || _isCorpse || !Renderer.IsValid() )
			return;

		var flat = _animationVelocity.WithZ( 0f );
		if ( flat.Length < 1f )
			return;

		var target = Rotation.LookAt( flat.Normal, Vector3.Up );
		var fraction = MathX.Clamp( Time.Delta * (TurnSpeed / 180f), 0f, 1f );
		Renderer.WorldRotation = Rotation.Slerp( Renderer.WorldRotation, target, fraction );
	}

	protected override void OnEnabled()
	{
		if ( !_all.Contains( this ) )
			_all.Add( this );
	}

	protected override void OnDisabled()
	{
		_all.Remove( this );
	}

	protected override void OnStart()
	{
		if ( Networking.IsHost )
		{
			Health = MaxHealth;
			return;
		}

		if ( Agent.IsValid() )
			Agent.Enabled = false;
	}

	protected override void OnUpdate()
	{
		UpdateWanderServer();
		UpdateAnimationServer();

		if ( !Networking.IsHost )
			ApplyAnimation( _animationVelocity );

		UpdateFacingServer();
	}

	protected override void OnDestroy()
	{
		_all.Remove( this );

		if ( Networking.IsHost )
			NpcManager.Instance?.ForgetNpcServer( this );
	}
}
