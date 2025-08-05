using Sandbox;

public sealed class Trash : Component, Component.ICollisionListener
{
	public void OnCollisionStart( Collision collision )
	{
		var damagable = collision.Other.GameObject.Parent?.Components.Get<IDamageable>();
		if (damagable is not Player) return;

		var rb = GetComponent<Rigidbody>();

		Log.Info( rb.Velocity.Length );

		damagable?.OnDamage(new DamageInfo( rb.Velocity.Length, GameObject, null));
	}
}
