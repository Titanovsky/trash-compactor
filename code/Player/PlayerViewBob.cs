using System;

public sealed class PlayerViewBob : Component, PlayerController.IEvents
{
	[Property, Description( "Reference to the player state component used to disable bob for dead players and spectators." )]
	public Player PlayerState { get; private set; }

	[Property, Description( "Reference to the PlayerController component that provides movement state." )]
	public PlayerController Controller { get; private set; }

	[Property, Description( "Enable first-person camera bob while the local player is walking or running." )]
	public bool EnableBob { get; set; } = true;

	[Property, Range( 0, 2 ), Description( "Global multiplier for all view bob movement and rotation." )]
	public float Intensity { get; set; } = 1.0f;

	[Property, Range( 0.1f, 4.0f ), Description( "Base speed of the walking bob cycle." )]
	public float Frequency { get; set; } = 1.8f;

	[Property, Range( 0, 8 ), Description( "Up and down camera movement in units." )]
	public float VerticalAmount { get; set; } = 1.2f;

	[Property, Range( 0, 8 ), Description( "Side to side camera movement in units." )]
	public float HorizontalAmount { get; set; } = 0.45f;

	[Property, Range( 0, 5 ), Description( "Pitch sway in degrees." )]
	public float PitchAmount { get; set; } = 0.25f;

	[Property, Range( 0, 5 ), Description( "Roll sway in degrees." )]
	public float RollAmount { get; set; } = 0.55f;

	[Property, Range( 1, 30 ), Description( "How quickly view bob blends in and out." )]
	public float Smoothness { get; set; } = 12.0f;

	[Property, Range( 0, 120 ), Description( "Minimum horizontal speed needed before view bob starts." )]
	public float MinSpeed { get; set; } = 12.0f;

	private float _phase;
	private float _blend;

	public void PostCameraSetup( CameraComponent cam )
	{
		if ( IsProxy )
			return;

		var player = GetPlayer();
		var controller = GetController();

		if ( !player.IsValid() || !controller.IsValid() || !EnableBob || !player.IsAlive || player.IsSpectator )
		{
			_blend = 0f;
			return;
		}

		cam = cam.IsValid() ? cam : Scene.Camera;

		if ( !cam.IsValid() )
			return;

		var velocity = controller.Velocity;
		var horizontalSpeed = new Vector3( velocity.x, velocity.y, 0f ).Length;
		var maxMoveSpeed = MathF.Max( MathF.Max( controller.WalkSpeed, controller.RunSpeed ), 1f );
		var speedFraction = Math.Clamp( horizontalSpeed / maxMoveSpeed, 0f, 1f );
		var targetBlend = controller.IsOnGround && horizontalSpeed > MinSpeed ? speedFraction : 0f;
		var blendDelta = 1f - MathF.Exp( -Smoothness * Time.Delta );

		_blend += (targetBlend - _blend) * Math.Clamp( blendDelta, 0f, 1f );

		if ( _blend <= 0.001f || Intensity <= 0f )
			return;

		_phase += Time.Delta * Frequency * (0.65f + speedFraction * 0.7f);

		var phase = _phase * MathF.PI * 2f;
		var amount = _blend * Intensity;
		var bobRight = MathF.Cos( phase ) * HorizontalAmount * amount;
		var bobUp = MathF.Sin( phase * 2f ) * VerticalAmount * amount;

		cam.WorldPosition += cam.WorldRotation.Right * bobRight;
		cam.WorldPosition += cam.WorldRotation.Up * bobUp;

		var angles = cam.WorldRotation.Angles();
		angles.pitch += MathF.Sin( phase * 2f ) * PitchAmount * amount;
		angles.roll += MathF.Cos( phase ) * RollAmount * amount;
		cam.WorldRotation = angles.ToRotation();
	}

	private Player GetPlayer()
	{
		return PlayerState.IsValid() ? PlayerState : Components.Get<Player>();
	}

	private PlayerController GetController()
	{
		return Controller.IsValid() ? Controller : Components.Get<PlayerController>();
	}
}
