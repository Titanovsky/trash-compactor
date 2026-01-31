using Sandbox;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

public sealed class SpawnerTrash : Component
{
	[Property] public GameObject trashPrefab;

	[Property, Group( "Trash Config" )] public float force = 5000f;
	[Property, Group( "Trash Config" )] public float delayPush = .5f;
	[Property, Group( "Trash Config" )] public float delayRemove = 3f;

	protected override void OnStart()
	{
		//SpawnTrash();
	}

	protected override void OnUpdate()
	{
		CheckSpawnTrashInput();
	}

	private void CheckSpawnTrashInput()
	{
		if ( Input.Pressed( "use" ) )
			SpawnTrash();
	}

	private async void SpawnTrash()
	{
		if ( !trashPrefab.IsValid() )
		{
			Log.Error( "ׂû המכבמ¸ב" );

			return;
		}

		var trash = trashPrefab.Clone( WorldPosition, Rotation.Random );

		await Push( trash );
		await RemoveObjectDelay( trash );
	}

	private async Task Push(GameObject go)
	{
		await Task.DelaySeconds( delayPush );
		if ( !go.IsValid() ) return;

		var rb = go.GetComponent<Rigidbody>();
		var dir = (Player.Local.WorldPosition - WorldPosition).Normal;

		rb.Velocity = dir * force;
	}

	private async Task RemoveObjectDelay( GameObject go )
	{
		await Task.DelaySeconds( delayRemove );
		if ( !go.IsValid() ) return;

		go.Destroy();
	}
}
