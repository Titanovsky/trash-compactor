using System;
using System.Linq;

public class RoundManager : Component
{
	public static RoundManager Instance { get; private set; }

	[Property, Description( "Duration of a standard round in seconds." )] public float RoundTime { get; set; } = 60f;
	[Property, Description( "Duration of a solo round (single player) in seconds." )] public float SoloRoundTime { get; set; } = 60f;
	[Property, Description( "Duration of the intermission phase between rounds in seconds." )] public float IntermissionTime { get; set; } = 5f;
	[Property, Description( "Number of rounds played before a map vote is triggered." )] public int MaxRoundsBeforeVote { get; set; } = 10;

	[Sync( SyncFlags.FromHost )] public RoundState State { get; private set; } = RoundState.None;
	[Sync( SyncFlags.FromHost )] public int RoundNumber { get; private set; } = 0;
	[Sync( SyncFlags.FromHost )] public float SyncedEndTime { get; private set; } = 0f;
	[Sync( SyncFlags.FromHost )] public bool IsSoloRound { get; private set; }

	public float TimeLeft => MathF.Max( SyncedEndTime - Time.Now, 0f );

	private readonly List<Player> _trashmanHistory = new();

	public void RegisterPlayerServer( Player player )
	{
		if ( !Networking.IsHost || !player.IsValid() )
			return;

		var role = State == RoundState.Started
			? RoleTrashCompactor.Spectator
			: RoleTrashCompactor.Survival;

		player.RespawnForRoundServer( role );
	}

	public void CheckRoundEndServer()
	{
		if ( !Networking.IsHost || State != RoundState.Started )
			return;

		var hasAliveSurvival = GetPlayersServer().Any( player => player.IsAlive && player.RoleEnum == RoleTrashCompactor.Survival );
		if ( !hasAliveSurvival )
			FinishRoundServer();
	}

	private void UpdateRoundAuthorityServer()
	{
		if ( !Networking.IsHost )
			return;

		if ( State == RoundState.None )
			StartIntermissionServer();

		if ( TimeLeft > 0f )
			return;

		if ( State == RoundState.Started )
			FinishRoundServer();
		else
			StartRoundServer();
	}

	private void StartIntermissionServer()
	{
		if ( !Networking.IsHost )
			return;

		State = RoundState.Finished;
		SyncedEndTime = Time.Now + IntermissionTime;
		SpawnerTrash.Instance?.FinishRoundServer();
	}

	private void StartRoundServer()
	{
		if ( !Networking.IsHost )
			return;

		var players = GetPlayersServer();
		if ( players.Count == 0 )
		{
			StartIntermissionServer();
			return;
		}

		RoundNumber++;
		// TODO: After MaxRoundsBeforeVote, start map vote instead of immediately continuing the round loop.

		AssignRolesServer( players );

		State = RoundState.Started;
		SyncedEndTime = Time.Now + (IsSoloRound ? SoloRoundTime : RoundTime);

		SpawnerTrash.Instance?.StartRoundServer( IsSoloRound );
	}

	private void FinishRoundServer()
	{
		if ( !Networking.IsHost || State != RoundState.Started )
			return;

		State = RoundState.Finished;
		SyncedEndTime = Time.Now + IntermissionTime;

		SpawnerTrash.Instance?.FinishRoundServer();
	}

	private void AssignRolesServer( List<Player> players )
	{
		IsSoloRound = players.Count <= 1;

		if ( IsSoloRound )
		{
			foreach ( var player in players )
				player.RespawnForRoundServer( RoleTrashCompactor.Survival );

			return;
		}

		var trashmanCount = Math.Max( 1, players.Count / 4 );
		var trashmen = SelectTrashmenServer( players, trashmanCount );

		foreach ( var player in players )
		{
			var role = trashmen.Contains( player )
				? RoleTrashCompactor.Trashman
				: RoleTrashCompactor.Survival;

			player.RespawnForRoundServer( role );
		}
	}

	private List<Player> SelectTrashmenServer( List<Player> players, int trashmanCount )
	{
		var candidates = players;

		if ( players.Count > 2 )
		{
			_trashmanHistory.RemoveAll( player => !player.IsValid() || !players.Contains( player ) );
			candidates = players.Where( player => !_trashmanHistory.Contains( player ) ).ToList();

			if ( candidates.Count < trashmanCount )
			{
				_trashmanHistory.Clear();
				candidates = players;
			}
		}

		var selected = candidates
			.OrderBy( _ => Random.Shared.Next() )
			.Take( trashmanCount )
			.ToList();

		if ( players.Count > 2 )
		{
			foreach ( var player in selected )
			{
				if ( !_trashmanHistory.Contains( player ) )
					_trashmanHistory.Add( player );
			}

			if ( _trashmanHistory.Count >= players.Count )
				_trashmanHistory.Clear();
		}

		return selected;
	}

	private List<Player> GetPlayersServer()
	{
		return Scene.GetAllComponents<Player>()
			.Where( player => player.IsValid() )
			.ToList();
	}

	private void CreateSingleton()
	{
		if ( Instance != null )
			return;

		Instance = this;
	}

	private void RemoveSingleton()
	{
		if ( Instance != this )
			return;

		Instance = null;
	}

	protected override void OnAwake()
	{
		CreateSingleton();
	}

	protected override void OnDestroy()
	{
		RemoveSingleton();
	}

	protected override void OnStart()
	{
		StartIntermissionServer();
	}

	protected override void OnUpdate()
	{
		UpdateRoundAuthorityServer();
	}
}
