using Sandbox;

public sealed class Trash : Component, Component.ICollisionListener
{
	public void OnCollisionStart( Collision collision )
	{
		if ( !Networking.IsHost )
			return;

		var player = FindPlayer( collision.Other.GameObject );
		if ( !player.IsValid() || !player.IsAlive || player.RoleEnum != RoleTrashCompactor.Survival )
			return;

		player.KillByTrashServer();
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
