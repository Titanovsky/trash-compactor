using Sandbox;

public sealed class Player : Component, Component.IDamageable
{
	public static Player Instance { get; set; }

	protected override void OnAwake()
	{
		if ( Instance.IsValid() ) return;

		Instance = this;
	}

	public void OnDamage( in DamageInfo damage )
	{
		Log.Info( damage.Attacker );
	}
}
