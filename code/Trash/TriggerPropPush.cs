using Sandbox;

public sealed class TriggerPropPush : Component, Component.ITriggerListener
{
	[Property, Description( "World-space direction in which trash props are pushed when entering the trigger volume." )] public Vector3 PushDirection { get; set; } = Vector3.Forward;
	[Property, Description( "Minimum speed applied to a trash prop's velocity when pushed by this trigger, in units per second." )] public float SpeedMultiplierMin { get; set; } = 1000f;
	[Property, Description( "Maximum speed applied to a trash prop's velocity when pushed by this trigger, in units per second." )] public float SpeedMultiplierMax { get; set; } = 1000f;
	[Property, Description( "Maximum directional offset applied to the push direction. Each axis is randomized in range [-value, +value] every push." )] public Vector3 DirectionOffset { get; set; } = Vector3.Zero;

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

		var dirOffset = new Vector3(
			Game.Random.Float( -DirectionOffset.x, DirectionOffset.x ),
			Game.Random.Float( -DirectionOffset.y, DirectionOffset.y ),
			Game.Random.Float( -DirectionOffset.z, DirectionOffset.z )
		);
		body.MotionEnabled = true;
		body.Velocity = (PushDirection.Normal + dirOffset).Normal * Game.Random.Float( SpeedMultiplierMin, SpeedMultiplierMax );
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
