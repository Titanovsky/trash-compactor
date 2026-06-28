using Sandbox;
using System.Linq;
using Sandbox.Platform;

public sealed class ButtonToggleTriggerPunch : Component, Component.IPressable
{
	[Property] public TriggerPropPush Trigger { get; set; }

	public bool Press( IPressable.Event e )
	{
		ResolveTrigger();

		if ( !Trigger.IsValid() )
			return false;

		if ( Networking.IsHost )
			return ToggleTriggerServer();

		RequestToggleRpc();
		return true;
	}

	[Rpc.Host( NetFlags.Reliable )]
	private void RequestToggleRpc()
	{
		ToggleTriggerServer();
	}

	private bool ToggleTriggerServer()
	{
		ResolveTrigger();

		if ( !Networking.IsHost || !Trigger.IsValid() )
			return false;

		var newEnabled = !Trigger.Enabled;
		Trigger.Enabled = newEnabled;

		ShowChatMessageRpc( newEnabled ? "Trigger punch enabled." : "Trigger punch disabled." );
		PlaySound();

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

    private void ResolveTrigger()
	{
		if ( Trigger.IsValid() )
			return;

		Trigger = Scene.GetAllComponents<TriggerPropPush>().FirstOrDefault( component => component.IsValid() );
	}
}
