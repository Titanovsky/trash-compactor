using System;

public class RoundManager : Component
{
    public static RoundManager Instance { get; private set; }

    [Property] public bool EnablePreRound { get; set; } = true;
    [Property] public bool EnablePostRound { get; set; } = true;

    [Property] public float PreRoundTime { get; set; } = 5f;
    [Property] public float RoundTime { get; set; } = 120f;
    [Property] public float PostRoundTime { get; set; } = 10f;

    public TimeUntil Timer { get; private set; } = 0;

    [Sync(SyncFlags.FromHost)] public RoundState State { get; private set; } = RoundState.None;

    public void Start()
    {
        if (!Networking.IsHost) return;

        State = RoundState.Started;

        Timer = RoundTime;

        SendSyncToClientsRpc(Timer.Relative);
        StartRpc();

        Log.Info($"[Round Manager] Started");
    }

    public void Pause()
    {
        if (!Networking.IsHost) return;

        State = RoundState.Paused;

        Log.Info($"[Round Manager] Paused");
    }

    public void Resume()
    {
        if (!Networking.IsHost) return;

        State = RoundState.Started;

        Log.Info($"[Round Manager] Resume");
    }

    public void Finish()
    {
        if (!Networking.IsHost) return;

        State = RoundState.Finished;
        Timer = PostRoundTime;

        SendSyncToClientsRpc(Timer.Relative);
        FinishRpc();

        Log.Info($"[Round Manager] Finished");
    }

    private void HandleStart()
    {
        if (!Networking.IsHost) return;

        Start();
    }

    private void HandleStartClient()
    {
        if (!Networking.IsHost)
        {
            Log.Info($"[Round Manager] Send request to host: {Connection.Host.DisplayName}");

            RequestSyncToHostRpc();
        }
    }

    private void HandleUpdate()
    {
        if (!Networking.IsHost) return;
        if (!Timer) return;

        if (State == RoundState.Started)
            Finish();
        else if (State == RoundState.Finished)
            Start();
    }

    [Rpc.Host(NetFlags.Reliable)]
    private void RequestSyncToHostRpc()
    {
        var time = Timer.Relative;
        Log.Info($"[Round Manager] Take request from {Rpc.Caller.DisplayName} and send {time}");

        SendSyncToClientsRpc(time);
    }

    [Rpc.Broadcast(NetFlags.HostOnly | NetFlags.Reliable)]
    private void SendSyncToClientsRpc(float time)
    {
        Timer = time;

        Log.Info($"[Round Manager] Sync from {Rpc.Caller.DisplayName}, Time: {time}");
    }

    [Rpc.Broadcast(NetFlags.HostOnly | NetFlags.Reliable)]
    private void StartRpc()
    { 
        Log.Info($"[Round Manager] RPC Start");
    }

    [Rpc.Broadcast(NetFlags.HostOnly | NetFlags.Reliable)]
    private void FinishRpc()
    {
        Player.Local.Spawn();

        Log.Info($"[Round Manager] RPC Finished");
    }

    private void CreateSingleton()
    {
        if (Instance != null) return;

        Instance = this;
    }

    private void RemoveSingleton()
    {
        if (Instance == null) return;

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
        HandleStart();
        HandleStartClient();
    }

    protected override void OnUpdate()
    {
        HandleUpdate();
    }
}