using Sandbox;
using Sandbox.Platform;
using System.Linq;
using System.Numerics;

public sealed class ButtonToggleTriggerPunch : Component, Component.IPressable
{
	[Property] public TriggerPropPush Trigger { get; set; }

	public bool Press( IPressable.Event e )
	{
		if ( !Trigger.IsValid() )
			return false;

        if (!e.Source.GameObject.Components.TryGet<Player>(out var ply, FindMode.EverythingInSelfAndParent))
            return false;

        RequestToggleRpc(ply);

        return true;
	}

	[Rpc.Host]
	private void RequestToggleRpc(Player ply)
	{
		ToggleTriggerServer(ply);
	}

	private bool ToggleTriggerServer(Player ply)
	{
		if ( !Networking.IsHost || !Trigger.IsValid() )
			return false;

		var newEnabled = !Trigger.Enabled;
		Trigger.Enabled = newEnabled;

        ShowChatMessage(ply.GameObject, newEnabled ? "Trigger punch enabled." : "Trigger punch disabled." );
		PlaySound();

        return true;
	}

    private void ShowChatMessage(GameObject ply, string message)
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
}
