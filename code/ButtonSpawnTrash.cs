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

        PlaySound();

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
            ShowChatMessageRpc(Rpc.Caller, "Only Trashman can use this button.");
            return;
        }

        if (!_spawnDelay)
        {
            var secondsLeft = MathF.Max((float)_spawnDelay, 0.1f);
            ShowChatMessageRpc(Rpc.Caller, $"Trash spawn is on cooldown. Wait {secondsLeft:0.0}s.");
            return;
        }

        var trash = SpawnerTrash.Instance?.SpawnBaseTrashServer();
        if (!trash.IsValid())
        {
            ShowChatMessageRpc(Rpc.Caller, "Trash spawn failed.");
            return;
        }

        ShowChatMessageRpc(Rpc.Caller, "Trash spawned!");

        _spawnDelay = MathF.Max(Delay, 0.1f);
    }

	[Rpc.Broadcast( NetFlags.Reliable )]
	private void ShowChatMessageRpc(Connection ply, string message )
	{
		using (Rpc.FilterInclude(c => c == ply))
		{
			Chat.AddText(message);
		};
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
