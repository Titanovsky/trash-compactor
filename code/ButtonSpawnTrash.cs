using System;
using Sandbox.Platform;

public sealed class ButtonSpawnTrash : Component, Component.IPressable
{
	[Property] public float Delay { get; set; } = 3f;

	private TimeUntil _spawnDelay;

	public bool Press( IPressable.Event e )
	{
		if ( !TryGetPlayerFromPress( e, out var player ) )
			return false;

        RequestPressRpc( player.GameObject );

        return true;
	}

	[Rpc.Host]
	private void RequestPressRpc( GameObject playerObject )
	{
		if (!playerObject.Components.TryGet( out Player player, FindMode.EverythingInSelfAndParent ))
			return;

        if (!Networking.IsHost || !player.IsValid())
            return;

        if (!player.CanUseTrashmanTools)
        {
            ShowChatMessage(playerObject, "Only Trashman can use this button.");
            return;
        }

        if (!_spawnDelay)
        {
            var secondsLeft = MathF.Max((float)_spawnDelay, 0.1f);
            ShowChatMessage(playerObject, $"Trash spawn is on cooldown. Wait {secondsLeft:0.0}s.");
            return;
        }

        var trash = SpawnerTrash.Instance?.SpawnBaseTrashServer();
        if (!trash.IsValid())
        {
            ShowChatMessage(playerObject, "Trash spawn failed.");
            return;
        }

        ShowChatMessage(playerObject, "Trash spawned!");

        PlaySound();

        _spawnDelay = MathF.Max(Delay, 0.1f);
    }

	private void ShowChatMessage(GameObject ply, string message )
	{
		using (Rpc.FilterInclude(c => c == ply.Network.Owner))
		{
            ShowChatMessageRpc(message);
		};
	}

    [Rpc.Broadcast]
    private void ShowChatMessageRpc(string message) 
	{
        Chat.AddText(message);
    }

    [Rpc.Broadcast]
	private void PlaySound()
	{
		Sound.Play(RoundManager.Instance.ButtonClickSound, WorldPosition);
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
