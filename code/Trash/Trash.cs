using Sandbox;
using System;

public sealed class Trash : Component, Component.ICollisionListener
{
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
}
