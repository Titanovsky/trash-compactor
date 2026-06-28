using Sandbox;
using System;
using System.Threading.Tasks;

public sealed class Trash : Component, Component.ICollisionListener
{
	private bool _lifetimeStarted;
	private bool _safetyTimerStarted;
	private TimeUntil _timeUntilSafetyMode;

	public bool SafetyModeEnabled { get; private set; }
	public Player LastTrashman { get; private set; }

	public void StartLifetimeTimerOnce( float lifetime )
	{
		if ( !Networking.IsHost || _lifetimeStarted )
			return;

		_lifetimeStarted = true;
		_ = DestroyAfterLifetime( lifetime );
	}

	public void EnableSafetyModeServer()
	{
		if ( !Networking.IsHost )
			return;

		SafetyModeEnabled = true;
		_safetyTimerStarted = false;
	}

	public void DisableSafetyModeServer()
	{
		if ( !Networking.IsHost )
			return;

		SafetyModeEnabled = false;
		_safetyTimerStarted = false;
	}

	public void SetLastTrashmanServer( Player trashman )
	{
		if ( !Networking.IsHost || !trashman.IsValid() || !trashman.IsTrashman )
			return;

		LastTrashman = trashman;
	}

	public void StartSafetyModeTimerOnce( float delay )
	{
		if ( !Networking.IsHost || SafetyModeEnabled || _safetyTimerStarted )
			return;

		_safetyTimerStarted = true;
		_timeUntilSafetyMode = delay;
	}

	public void OnCollisionStart( Collision collision )
	{
		if ( !Networking.IsHost )
			return;

		if ( SafetyModeEnabled )
			return;

		var player = FindPlayer( collision.Other.GameObject );
		if ( !player.IsValid() || !player.IsAlive || player.IsTrashman )
			return;

		var speed = GetImpactSpeed( collision, player );
		const float minSpeed = 150f;
		if ( speed < minSpeed )
			return;

		var damage = (speed - minSpeed) * 0.25f;
		if ( damage <= 0f )
			return;

		var info = new DamageInfo
		{
			Damage = damage,
			Attacker = GameObject,
			Position = collision.Contact.Point,
		};

		((Component.IDamageable)player).OnDamage( info );
	}

	private float GetImpactSpeed( Collision collision, Player player )
	{
		var contactSpeed = MathF.Abs( collision.Contact.NormalSpeed );

		var trashBody = GameObject.Components.Get<Rigidbody>();
		var trashVelocity = trashBody.IsValid() ? trashBody.Velocity : Vector3.Zero;

		var playerBody = player.Controller?.Body;
		var playerVelocity = playerBody.IsValid() ? playerBody.Velocity : Vector3.Zero;

		var relativeSpeed = (trashVelocity - playerVelocity).Length;
		var trashSpeed = trashVelocity.Length;

		return MathF.Max( contactSpeed, MathF.Max( relativeSpeed, trashSpeed ) );
	}

	private async Task DestroyAfterLifetime( float lifetime )
	{
		await Task.DelaySeconds( lifetime );
		if ( !GameObject.IsValid() )
			return;

		SpawnerTrash.Instance?.ForgetTrashServer( GameObject );
		GameObject.Destroy();
	}

	private Player FindPlayer( GameObject gameObject )
	{
		var current = gameObject;
		while ( current.IsValid() )
		{
			var player = current.Components.Get<Player>();
			if ( player.IsValid() )
				return player;

			current = current.Parent;
		}

		return null;
	}

	protected override void OnDestroy()
	{
		if ( Networking.IsHost )
			SpawnerTrash.Instance?.ForgetTrashServer( GameObject );
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost || !_safetyTimerStarted || !_timeUntilSafetyMode )
			return;

		EnableSafetyModeServer();
	}
}
