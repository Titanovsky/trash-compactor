using Sandbox;

public sealed class TriggerPropPush : Component, Component.ITriggerListener
{
	[Property] public Vector3 PushDirection { get; set; } = Vector3.Forward;
	[Property] public float SpeedMultiplier { get; set; } = 1000f;

	public void OnTriggerEnter( Collider other )
	{
		if ( !Networking.IsHost )
			return;

		var trash = FindTrash( other.GameObject );
		if ( !trash.IsValid() )
			return;

		var body = other.Rigidbody;
		if ( !body.IsValid() )
			body = trash.GetComponent<Rigidbody>();

		if ( !body.IsValid() || PushDirection.Length < 0.001f )
			return;

		body.MotionEnabled = true;
		body.Velocity = PushDirection.Normal * SpeedMultiplier;
	}

	public void OnTriggerExit( Collider other )
	{
	}

	private Trash FindTrash( GameObject gameObject )
	{
		var current = gameObject;
		while ( current.IsValid() )
		{
			var trash = current.Components.Get<Trash>();
			if ( trash.IsValid() )
				return trash;

			current = current.Parent;
		}

		return null;
	}
}
