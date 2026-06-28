using Sandbox;

public sealed class TriggerTeleport : Component, Component.ITriggerListener
{
	[Property] public GameObject Spawn { get; set; }

	public void OnTriggerEnter( Collider other )
	{
		if ( !Networking.IsHost || !Spawn.IsValid() )
			return;

		if ( !TryGetPlayer( other.GameObject, out var player ) )
			return;

		player.WorldPosition = Spawn.WorldPosition;
		player.WorldRotation = Spawn.WorldRotation;

		if ( player.Controller.IsValid() )
		{
			player.Controller.EyeAngles = Spawn.WorldRotation.Angles();

			if ( player.Controller.Body.IsValid() )
			{
				player.Controller.Body.Velocity = Vector3.Zero;
				player.Controller.Body.AngularVelocity = Vector3.Zero;
			}
		}
	}

	public void OnTriggerExit( Collider other )
	{
	}

	private bool TryGetPlayer( GameObject gameObject, out Player player )
	{
		player = null;
		return gameObject.IsValid() && gameObject.Components.TryGet( out player, FindMode.EverythingInSelfAndParent );
	}
}
