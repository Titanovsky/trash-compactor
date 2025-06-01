using Sandbox;

public sealed class EntryPoint : Component
{
	protected override void OnStart()
	{
		GameHandler.Init();
	}
}
