using Sandbox;
using System;

public sealed class FpPlayerGrabber : Component
{
	[Property, Description( "Particle effect spawned at the point of impact when shooting." )] public GameObject ImpactEffect { get; set; }
	[Property, Description( "Decal effect applied to the surface at the point of impact when shooting." )] public GameObject DecalEffect { get; set; }
	[Property, Description( "Damage dealt to a target per shot." )] public float ShootDamage { get; set; } = 9.0f;
	[Property, Description( "Maximum distance in units at which the player can grab or shoot a trash prop." )] public float MaxGrabDistance { get; set; } = 450f;

	[Property, Range( 1, 16 ), Description( "Smoothness factor applied when moving a grabbed prop towards the target position. Higher values feel snappier." )]
	public float MovementSmoothness { get; set; } = 3.0f;

	private Rigidbody _grabbedBody;
	private Vector3 _grabReleaseVelocity;
	private Vector3 _lastGrabTargetPosition;
	private float _lastGrabMoveTime;
	private bool _hasGrabMoveSample;

	private bool _clientHasGrab;
	private Transform _clientGrabbedOffset;
	private bool _waitForUp;

	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;

		if ( _waitForUp )
		{
			if ( !Input.Down( "attack1" ) && !Input.Down( "attack2" ) )
				_waitForUp = false;

			return;
		}

		if ( !CanUseLocal() )
		{
			StopClientGrab();
			return;
		}

		var aimTransform = Scene.Camera.WorldTransform;

		if ( _clientHasGrab )
		{
			if ( Input.Pressed( "attack2" ) || !Input.Down( "attack1" ) )
				StopClientGrab();

			return;
		}

		if ( !Input.Pressed( "attack1" ) )
			return;

		var trace = Scene.Trace
			.Ray( Scene.Camera.WorldPosition, Scene.Camera.WorldPosition + Scene.Camera.WorldRotation.Forward * MaxGrabDistance )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();

		if ( !trace.Hit || !trace.Body.IsValid() || trace.Body.BodyType == PhysicsBodyType.Static )
			return;

		var trash = FindTrash( trace.GameObject );
		if ( !trash.IsValid() )
			return;

		_clientGrabbedOffset = aimTransform.ToLocal( trace.Body.Transform );
		_clientHasGrab = true;

		RequestStartGrabRpc( trash.GameObject );
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy || !_clientHasGrab || _waitForUp )
			return;

		if ( !Input.Down( "attack1" ) || !CanUseLocal() )
		{
			StopClientGrab();
			return;
		}

		var target = Scene.Camera.WorldTransform.ToWorld( _clientGrabbedOffset );
		RequestMoveGrabRpc( target.Position, target.Rotation );
	}

	private void StopClientGrab()
	{
		if ( !_clientHasGrab )
			return;

		_clientHasGrab = false;
		_clientGrabbedOffset = default;
		_waitForUp = true;

		RequestReleaseGrabRpc();
	}

	protected override void OnPreRender()
	{
		if ( IsProxy || !CanUseLocal() || _clientHasGrab )
			return;

		var trace = Scene.Trace
			.Ray( Scene.Camera.ScreenNormalToRay( 0.5f ), MaxGrabDistance )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();

		if ( trace.Hit && FindTrash( trace.GameObject ).IsValid() )
		{
			Gizmo.Draw.Color = Color.Green;
			Gizmo.Draw.SolidSphere( trace.HitPosition, 1 );
		}
	}

	private bool CanUseLocal()
	{
		var player = Components.Get<Player>();
		return player.IsValid() && player.CanUseTrashmanTools;
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

	[Rpc.Host( NetFlags.Reliable )]
	private void RequestStartGrabRpc( GameObject target )
	{
		var trash = FindTrash( target );
		if ( !CanGrabServer( target ) || !trash.IsValid() )
			return;

		var body = target.Components.Get<Rigidbody>();
		if ( !body.IsValid() )
			return;

		var player = Components.Get<Player>();
		trash.SetLastTrashmanServer( player );

		SpawnerTrash.Instance?.StartTrashLifetimeServer( trash.GameObject );
		SpawnerTrash.Instance?.StartTrashSafetyServer( trash.GameObject, SpawnerTrash.Instance.GrabbedSafetyDelay );

		_grabbedBody = body;
		_grabbedBody.MotionEnabled = true;
		_grabReleaseVelocity = Vector3.Zero;
		_lastGrabTargetPosition = body.WorldPosition;
		_lastGrabMoveTime = Time.Now;
		_hasGrabMoveSample = false;
	}

	[Rpc.Host( NetFlags.Unreliable )]
	private void RequestMoveGrabRpc( Vector3 targetPosition, Rotation targetRotation )
	{
		if ( !_grabbedBody.IsValid() || !CanGrabServer( _grabbedBody.GameObject ) )
			return;

		UpdateGrabReleaseVelocity( targetPosition );

		var targetTransform = new Transform( targetPosition, targetRotation );
		_grabbedBody.SmoothMove( targetTransform, 0.02f * MovementSmoothness, Time.Delta );
	}

	[Rpc.Host( NetFlags.Reliable )]
	private void RequestReleaseGrabRpc()
	{
		if ( _grabbedBody.IsValid() && _grabReleaseVelocity.Length > 0.1f )
		{
			_grabbedBody.MotionEnabled = true;
			_grabbedBody.Velocity = _grabReleaseVelocity;
		}

		_grabbedBody = null;
		_grabReleaseVelocity = Vector3.Zero;
		_hasGrabMoveSample = false;
	}

	private void UpdateGrabReleaseVelocity( Vector3 targetPosition )
	{
		var now = Time.Now;
		var dt = MathF.Max( now - _lastGrabMoveTime, 0.001f );

		if ( _hasGrabMoveSample )
		{
			var velocity = (targetPosition - _lastGrabTargetPosition) / dt;
			const float maxThrowSpeed = 2500f;

			if ( velocity.Length > maxThrowSpeed )
				velocity = velocity.Normal * maxThrowSpeed;

			_grabReleaseVelocity = velocity;
		}

		_lastGrabTargetPosition = targetPosition;
		_lastGrabMoveTime = now;
		_hasGrabMoveSample = true;
	}

	private bool CanGrabServer( GameObject target )
	{
		if ( !Networking.IsHost || !target.IsValid() )
			return false;

		var player = Components.Get<Player>();
		if ( !player.IsValid() || !player.CanUseTrashmanTools )
			return false;

		if ( (player.WorldPosition - target.WorldPosition).Length > MaxGrabDistance )
			return false;

		return FindTrash( target ).IsValid();
	}
}
