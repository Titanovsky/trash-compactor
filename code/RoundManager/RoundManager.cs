using System;
using System.Linq;

public class RoundManager : Component
{
	public static RoundManager Instance { get; private set; }

	[Property, Description( "Duration of a standard round in seconds." )] public float RoundTime { get; set; } = 60f;
	[Property, Description( "Duration of a solo round (single player) in seconds." )] public float SoloRoundTime { get; set; } = 60f;
	[Property, Description( "Duration of the intermission phase between rounds in seconds." )] public float IntermissionTime { get; set; } = 5f;
	[Property, Description( "Number of completed rounds before a map vote is triggered. Set to 0 to disable automatic voting." )] public int MaxRoundsBeforeVote { get; set; } = 2;
    [Property] public SoundEvent PlayerDeadSound { get; set; }
    [Property] public SoundEvent RoundStartSound { get; set; }
    [Property] public SoundEvent ButtonClickSound { get; set; }

    [Sync( SyncFlags.FromHost )] public RoundState State { get; private set; } = RoundState.None;
	[Sync( SyncFlags.FromHost )] public int RoundNumber { get; private set; } = 0;
	[Sync( SyncFlags.FromHost )] public float SyncedEndTime { get; private set; } = 0f;
	[Sync( SyncFlags.FromHost )] public bool IsSoloRound { get; private set; }
	[Sync( SyncFlags.FromHost )] public bool IsSoloTrashmanRound { get; private set; }
	[Sync( SyncFlags.FromHost )] public RoundWinner LastWinner { get; private set; } = RoundWinner.None;

	public float TimeLeft => MathF.Max( SyncedEndTime - Time.Now, 0f );

	private readonly List<Player> _trashmanHistory = new();
	private readonly List<Player> _registeredPlayers = new();
	private readonly List<Player> _pendingRegistrations = new();
	private bool _isMapReadyServer;

	public void RegisterPlayerServer( Player player )
	{
		if ( !Networking.IsHost || !player.IsValid() )
			return;

		if ( _registeredPlayers.Contains( player ) || _pendingRegistrations.Contains( player ) )
			return;

		if ( !_isMapReadyServer || State == RoundState.MapVote )
		{
			_pendingRegistrations.Add( player );
			player.PrepareForMapLoadingServer();
			return;
		}

		var role = State == RoundState.Started
			? RoleTrashCompactor.Spectator
			: RoleTrashCompactor.Survival;

		if ( !CanSpawnRoleServer( role ) )
		{
			_pendingRegistrations.Add( player );
			Log.Warning( $"[RoundManager] Delaying registration for {player.GameObject.Name}: no {role} spawn points are ready yet." );
			return;
		}

		CompletePlayerRegistrationServer( player, role );

		if ( State == RoundState.Started && IsSoloRound && GetPlayersServer().Count > 1 )
			RestartSoloRoundForNewPlayerServer();
	}

	public void CheckRoundEndServer()
	{
		if ( !Networking.IsHost || State != RoundState.Started )
			return;

		var hasAliveTrashman = GetPlayersServer().Any( player => player.IsAlive && player.RoleEnum == RoleTrashCompactor.Trashman );
		var hasAliveSurvival = GetPlayersServer().Any( player => player.IsAlive && player.RoleEnum == RoleTrashCompactor.Survival )
			|| HasAliveNpcSurvivorsServer();

		if ( !IsSoloRound && !hasAliveTrashman )
		{
			FinishRoundServer( RoundWinner.Survival );
			return;
		}

		if ( !hasAliveSurvival )
			FinishRoundServer( RoundWinner.Trashman );
	}

	private bool HasAliveNpcSurvivorsServer()
	{
		return IsSoloTrashmanRound && NpcManager.Instance.IsValid() && NpcManager.Instance.AliveNpcCountServer() > 0;
	}

	private void UpdateRoundAuthorityServer()
	{
		if ( !Networking.IsHost )
			return;

		RemoveInvalidPlayersServer();

		if ( !_isMapReadyServer )
			return;

		if ( State == RoundState.MapVote )
			return;

		ProcessPendingRegistrationsServer();

		if ( State is RoundState.None or RoundState.PreStarted )
		{
			if ( GetPlayersServer().Count > 0 )
				StartRoundServer();

			return;
		}

		if ( State == RoundState.Started && HandleActiveRoundStateServer() )
			return;

		if ( TimeLeft > 0f )
			return;

		if ( State == RoundState.Started )
			FinishRoundServer( RoundWinner.Survival );
		else if ( State == RoundState.Finished && IsMapVoteDueServer() )
			GameManager.Instance?.RequestMapVote();
		else
			StartRoundServer();
	}

	private void StartIntermissionServer()
	{
		if ( !Networking.IsHost )
			return;

		State = RoundState.Finished;
		LastWinner = RoundWinner.None;
		var isSolo = GetPlayersServer().Count <= 1;
		SyncedEndTime = Time.Now + (isSolo ? 0f : IntermissionTime);
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

		AssignRolesServer( players );
		SpawnerTrash.Instance?.PrepareRoundServer( IsSoloRound && !IsSoloTrashmanRound );

		LastWinner = RoundWinner.None;
		State = RoundState.Started;
		SyncedEndTime = Time.Now + (IsSoloRound ? SoloRoundTime : RoundTime);

		SpawnerTrash.Instance?.FinalizeRoundStartServer();
		PlayRoundStartSoundRpc();
	}

	public void SetMapLoadingServer()
	{
		if ( !Networking.IsHost )
			return;

		_isMapReadyServer = false;
		State = RoundState.PreStarted;
		RoundNumber = 0;
		LastWinner = RoundWinner.None;
		IsSoloRound = false;
		IsSoloTrashmanRound = false;
		SyncedEndTime = 0f;

		NpcManager.Instance?.ClearNpcsServer();
		SpawnerTrash.Instance?.ClearForMapUnloadServer();
		_trashmanHistory.Clear();
		_registeredPlayers.Clear();
		_pendingRegistrations.Clear();

		foreach ( var player in GetPlayersServer() )
		{
			player.PrepareForMapLoadingServer();
			_pendingRegistrations.Add( player );
		}
	}

	public void SetMapReadyServer()
	{
		if ( !Networking.IsHost || !MapInfo.Instance.IsValid() )
			return;

		_isMapReadyServer = true;
		State = RoundState.PreStarted;
		SyncedEndTime = 0f;
	}

	public void BeginMapVoteServer()
	{
		if ( !Networking.IsHost || !_isMapReadyServer || State == RoundState.MapVote )
			return;

		State = RoundState.MapVote;
		LastWinner = RoundWinner.None;
		IsSoloRound = false;
		IsSoloTrashmanRound = false;
		SyncedEndTime = 0f;
		NpcManager.Instance?.ClearNpcsServer();
		SpawnerTrash.Instance?.ClearForMapVoteServer();

		foreach ( var player in GetPlayersServer() )
			player.PrepareForMapLoadingServer();
	}

	public void ResumeAfterMapVoteServer()
	{
		if ( !Networking.IsHost || !_isMapReadyServer || State != RoundState.MapVote )
			return;

		RoundNumber = 0;
		LastWinner = RoundWinner.None;
		State = RoundState.PreStarted;
		SyncedEndTime = 0f;
	}

	private void FinishRoundServer( RoundWinner winner )
	{
		if ( !Networking.IsHost || State != RoundState.Started )
			return;

		State = RoundState.Finished;
		LastWinner = winner;
		SyncedEndTime = Time.Now + IntermissionTime;

		SpawnerTrash.Instance?.FinishRoundServer();
	}

	private void RestartSoloRoundForNewPlayerServer()
	{
		if ( !Networking.IsHost || State != RoundState.Started || !IsSoloRound )
			return;

		State = RoundState.Finished;
		LastWinner = RoundWinner.None;
		SyncedEndTime = Time.Now;

		NpcManager.Instance?.ClearNpcsServer();
		SpawnerTrash.Instance?.FinishRoundServer();
		StartRoundServer();
	}

	private void AssignRolesServer( List<Player> players )
	{
		IsSoloRound = players.Count <= 1;

		NpcManager.Instance?.ClearNpcsServer();

		if ( IsSoloRound )
		{
			AssignSoloRoleServer( players );
			return;
		}

		IsSoloTrashmanRound = false;

		var trashmanCount = Math.Max( 1, (int)MathF.Ceiling( players.Count / 4f ) );
		var trashmen = SelectTrashmenServer( players, trashmanCount );

		foreach ( var player in players )
		{
			var role = trashmen.Contains( player )
				? RoleTrashCompactor.Trashman
				: RoleTrashCompactor.Survival;

			player.RespawnForRoundServer( role );
		}
	}

	private void AssignSoloRoleServer( List<Player> players )
	{
		var wantsTrashmanRound = RoundNumber % 2 == 0;
		var canSpawnNpcs = wantsTrashmanRound && NpcManager.Instance.IsValid() && NpcManager.Instance.CanSpawnNpcsServer();

		if ( wantsTrashmanRound && !canSpawnNpcs )
			Log.Warning( "[RoundManager] Solo Trashman round requested but NPCs cannot be spawned. Falling back to the Survival solo round." );

		IsSoloTrashmanRound = canSpawnNpcs;

		var role = canSpawnNpcs
			? RoleTrashCompactor.Trashman
			: RoleTrashCompactor.Survival;

		foreach ( var player in players )
			player.RespawnForRoundServer( role );

		if ( canSpawnNpcs )
			NpcManager.Instance.SpawnNpcsServer();
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

	private bool HandleActiveRoundStateServer()
	{
		var players = GetPlayersServer();

		if ( players.Count <= 1 && !IsSoloRound )
		{
			RestartAsSoloRoundServer();
			return true;
		}

		if ( IsSoloRound )
		{
			if ( IsSoloTrashmanRound && !HasAliveNpcSurvivorsServer() )
			{
				FinishRoundServer( RoundWinner.Trashman );
				return true;
			}

			return false;
		}

		var hasTrashman = players.Any( player => player.RoleEnum == RoleTrashCompactor.Trashman );
		if ( !hasTrashman )
		{
			FinishRoundServer( RoundWinner.Survival );
			return true;
		}

		var hasSurvival = players.Any( player => player.RoleEnum == RoleTrashCompactor.Survival );
		if ( !hasSurvival )
		{
			FinishRoundServer( RoundWinner.Trashman );
			return true;
		}

		return false;
	}

	private void RestartAsSoloRoundServer()
	{
		if ( !Networking.IsHost || State != RoundState.Started || IsSoloRound )
			return;

		State = RoundState.Finished;
		LastWinner = RoundWinner.None;
		SyncedEndTime = Time.Now;

		NpcManager.Instance?.ClearNpcsServer();
		SpawnerTrash.Instance?.FinishRoundServer();
		StartRoundServer();
	}

	private void ProcessPendingRegistrationsServer()
	{
		if ( _pendingRegistrations.Count == 0 )
			return;

		foreach ( var player in _pendingRegistrations.ToArray() )
		{
			if ( !player.IsValid() )
			{
				_pendingRegistrations.Remove( player );
				continue;
			}

			var role = State == RoundState.Started
				? RoleTrashCompactor.Spectator
				: RoleTrashCompactor.Survival;

			if ( !CanSpawnRoleServer( role ) )
				continue;

			CompletePlayerRegistrationServer( player, role );

			if ( State == RoundState.Started && IsSoloRound && GetPlayersServer().Count > 1 )
				RestartSoloRoundForNewPlayerServer();
		}
	}

	private void CompletePlayerRegistrationServer( Player player, RoleTrashCompactor role )
	{
		_pendingRegistrations.Remove( player );

		if ( !_registeredPlayers.Contains( player ) )
			_registeredPlayers.Add( player );

		player.RespawnForRoundServer( role );
	}

	private bool CanSpawnRoleServer( RoleTrashCompactor role )
	{
		if ( !_isMapReadyServer || !MapInfo.Instance.IsValid() )
			return false;

		var spawns = Role.Create( role ).GetSpawns( MapInfo.Instance );
		return spawns.Any( spawn => spawn.IsValid() );
	}

	private bool IsMapVoteDueServer()
	{
		return MaxRoundsBeforeVote > 0
			&& RoundNumber >= MaxRoundsBeforeVote;
	}

	private void RemoveInvalidPlayersServer()
	{
		_registeredPlayers.RemoveAll( player => !player.IsValid() );
		_pendingRegistrations.RemoveAll( player => !player.IsValid() );
		_trashmanHistory.RemoveAll( player => !player.IsValid() );
	}

	public void PlayPlayerDeathSoundServer( Vector3 position )
	{
		if ( !Networking.IsHost )
			return;

		PlayPlayerDeathSoundRpc( position );
	}

	public void PublishKillFeedServer( Player killer, Player victim, bool isSuicide = false )
	{
		if ( !Networking.IsHost || !victim.IsValid() )
			return;

		var victimName = string.IsNullOrWhiteSpace( victim.Name ) ? "Survivor" : victim.Name;

		if ( isSuicide )
		{
			PublishKillFeedRpc( victimName, victimName, true );
			return;
		}

		if ( !killer.IsValid() || !killer.IsTrashman )
			return;

		var killerName = string.IsNullOrWhiteSpace( killer.Name ) ? "Trashman" : killer.Name;

		PublishKillFeedRpc( killerName, victimName, false );
	}

	public void PublishNpcKillFeedServer( string npcName )
	{
		if ( !Networking.IsHost )
			return;

		var killer = GetPlayersServer().FirstOrDefault( player => player.IsValid() && player.IsTrashman );
		var killerName = killer.IsValid() && !string.IsNullOrWhiteSpace( killer.Name ) ? killer.Name : "Trashman";
		var victimName = string.IsNullOrWhiteSpace( npcName ) ? "Survivor" : npcName;

		PublishKillFeedRpc( killerName, victimName, false );
	}

	[Rpc.Broadcast( NetFlags.Reliable )]
	private void PublishKillFeedRpc( string killerName, string victimName, bool isSuicide )
	{
		KillFeed.AddEntry( killerName, victimName, isSuicide );
	}

	[Rpc.Broadcast( NetFlags.Reliable )]
	private void PlayPlayerDeathSoundRpc( Vector3 position )
	{
		if ( PlayerDeadSound is null )
			return;

		Sound.Play( PlayerDeadSound, position );
	}

	[Rpc.Broadcast( NetFlags.Reliable )]
	private void PlayRoundStartSoundRpc()
	{
		if ( RoundStartSound is null )
			return;

		Sound.Play( RoundStartSound );
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
		SetMapLoadingServer();
	}

	protected override void OnUpdate()
	{
		UpdateRoundAuthorityServer();
	}
}
