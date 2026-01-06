using Sandbox;
using System;

public class RoundManager : Component
{
    public static RoundManager Instance { get; private set; }

    [Property] public bool EnablePreRound { get; set; } = true;
    [Property] public bool EnablePostRound { get; set; } = true;

    [Property] public float PreRoundTime { get; set; } = 5f;
    [Property] public float RoundTime { get; set; } = 120f;
    [Property] public float PostRoundTime { get; set; } = 10f;

    public TimeUntil Timer { get; set; } = 0;

    public RoundState State { get; private set; } = RoundState.None;

    public void Start()
    {
        if (!Networking.IsHost) return;

        Timer = RoundTime;
        State = RoundState.Started;

        SendSyncToClientsRpc(Timer.Relative, State.ToString());
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

        Timer = PostRoundTime;
        State = RoundState.Finished;

        SendSyncToClientsRpc(Timer.Relative, State.ToString());
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
            Log.Info($"Send request to host");

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

    private void ChangeState(string state)
    {
        State = Enum.Parse<RoundState>(state);
    }

    [Rpc.Host(NetFlags.Reliable)]
    private void RequestSyncToHostRpc()
    {
        var time = Timer.Relative;
        Log.Info($"Take request from {Rpc.Caller.DisplayName} and send {time}");

        SendSyncToClientsRpc(time, State.ToString());
    }

    [Rpc.Broadcast(NetFlags.HostOnly | NetFlags.Reliable)]
    private void SendSyncToClientsRpc(float time, string state)
    {
        if (IsProxy) return;

        Timer = time;
        ChangeState(state);

        Log.Info($"Sync from {Rpc.Caller.DisplayName}, Time: {time} State: {State}");
    }

    [Rpc.Broadcast(NetFlags.HostOnly | NetFlags.Reliable)]
    private void StartRpc()
    { 
        if (IsProxy) return;

        Log.Info($"[Round Manager] RPC Start");
    }

    [Rpc.Broadcast(NetFlags.HostOnly | NetFlags.Reliable)]
    private void FinishRpc()
    {
        if (IsProxy) return;

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