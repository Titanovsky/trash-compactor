using Sandbox;
using System;
using System.Threading.Tasks;

public sealed class Trash : Component, Component.ICollisionListener
{
	private bool _lifetimeStarted;

	public void StartLifetimeTimerOnce( float lifetime )
	{
		if ( !Networking.IsHost || _lifetimeStarted )
			return;

		_lifetimeStarted = true;
		_ = DestroyAfterLifetime( lifetime );
	}

	public void OnCollisionStart( Collision collision )
	{
		if ( !Networking.IsHost )
			return;

		var player = FindPlayer( collision.Other.GameObject );
		if ( !player.IsValid() || !player.IsAlive || player.RoleEnum != RoleTrashCompactor.Survival )
			return;

		var speed = MathF.Abs( collision.Contact.NormalSpeed );
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
}
