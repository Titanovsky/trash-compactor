using Sandbox;

public sealed class EntryPoint : Component
{
	protected override void OnStart()
	{
		GameHandler.Init();
	}

	private float t = 0f;
	protected override void OnUpdate()
	{
		t += Time.Delta;
		Log.Info( t );
	}
}
