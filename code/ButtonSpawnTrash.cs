using System;
using Sandbox;
using Sandbox.Platform;

public sealed class ButtonSpawnTrash : Component, Component.IPressable
{
	[Property] public float Delay { get; set; } = 3f;

	private TimeUntil _spawnDelay;

	public bool Press( IPressable.Event e )
	{
		if ( !TryGetPlayerFromPress( e, out var player ) )
			return false;

		if ( Networking.IsHost )
			return HandlePressServer( player );

		RequestPressRpc( player.GameObject );
		return true;
	}

	[Rpc.Host( NetFlags.Reliable )]
	private void RequestPressRpc( GameObject playerObject )
	{
		if ( !playerObject.IsValid() || !playerObject.Components.TryGet( out Player player, FindMode.EverythingInSelfAndParent ) )
			return;

		HandlePressServer( player );
	}

	private bool HandlePressServer( Player player )
	{
		if ( !Networking.IsHost || !player.IsValid() )
			return false;

		if ( !player.CanUseTrashmanTools )
		{
			ShowChatMessageRpc( "Only Trashman can use this button." );
			return false;
		}

		if ( !_spawnDelay )
		{
			var secondsLeft = MathF.Max( (float)_spawnDelay, 0.1f );
			ShowChatMessageRpc( $"Trash spawn is on cooldown. Wait {secondsLeft:0.0}s." );
			return false;
		}

		var trash = SpawnerTrash.Instance?.SpawnBaseTrashServer();
		if ( !trash.IsValid() )
		{
			ShowChatMessageRpc( "Trash spawn failed." );
			return false;
		}

		PlaySound();

        _spawnDelay = MathF.Max( Delay, 0.1f );
		return true;
	}

	[Rpc.Broadcast( NetFlags.Reliable )]
	private void ShowChatMessageRpc( string message )
	{
		Chat.AddText( message );
	}

    [Rpc.Broadcast()]
    private void PlaySound()
    {
        Sound.Play(RoundManager.Instance.ButtonClickSound);
    }

    private bool TryGetPlayerFromPress( IPressable.Event e, out Player player )
	{
		player = null;

		var source = e.Source?.GameObject;
		if ( !source.IsValid() )
			return false;

		return source.Components.TryGet( out player, FindMode.EverythingInSelfAndParent );
	}
}
