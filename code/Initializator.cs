using Sandbox;

public sealed class Initializator : Component
{
	protected override void OnStart()
	{
		GameHandler.Init();
	}
}
