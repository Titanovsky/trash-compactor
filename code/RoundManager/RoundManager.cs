using Sandbox;
using System;

public sealed class RoundManager : Component
{
    public static RoundManager Instance { get; private set; }

    protected override void OnAwake()
    {
        if (Instance != null)
        {
            Log.Error("[RoundManager] Multiple instances detected!");
            Destroy();
            return;
        }

        Instance = this;
        Log.Info("[RoundManager] Initialized");
    }

    protected override void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ========================
    // Settings (Inspector)
    // ========================
    [Property] public bool EnablePreRound { get; set; } = true;
    [Property] public bool EnablePostRound { get; set; } = true;

    [Property] public float PreRoundTime { get; set; } = 5f;
    [Property] public float RoundTime { get; set; } = 120f;
    [Property] public float PostRoundTime { get; set; } = 10f;

    // ========================
    // Networking
    // ========================
    [Sync] public RoundState State { get; private set; } = RoundState.NotStarted;

    [Sync] private float SyncedEndTime { get; set; }
    [Sync] private bool TimerFrozen { get; set; }

    public float TimeLeft =>
        TimerFrozen ? CachedTimeLeft : MathF.Max(SyncedEndTime - Time.Now, 0f);

    private float CachedTimeLeft;

    // ========================
    // Events
    // ========================
    public event Action OnPreStartRound;
    public event Action OnStartRound;
    public event Action OnPostStartRound;

    public event Action OnPauseRound;
    public event Action OnResumeRound;

    public event Action OnFinishRound;
    public event Action OnPostFinishRound;

    // ========================
    // Update
    // ========================
    protected override void OnUpdate()
    {
        if (!Networking.IsHost)
            return;

        if (TimerFrozen)
            return;

        if (TimeLeft <= 0f)
        {
            HandleTimerEnd();
        }
    }

    private void HandleTimerEnd()
    {
        if (EnablePreRound && State == RoundState.NotStarted)
        {
            StartRound_Internal();
            return;
        }

        if (State == RoundState.Running)
        {
            FinishRound_Internal();
            return;
        }

        if (EnablePostRound && State == RoundState.Finished)
        {
            PostFinish_Internal();
        }
    }

    // ========================
    // Host API
    // ========================
    public void StartRound()
    {
        if (!Networking.IsHost)
        {
            Rpc_RequestStart();
            return;
        }

        if (State != RoundState.NotStarted)
        {
            Log.Warning("[RoundManager] Cannot start: round already started");
            return;
        }

        if (EnablePreRound)
            PreStart_Internal();
        else
            StartRound_Internal();
    }

    public void FinishRound()
    {
        if (!Networking.IsHost)
        {
            Rpc_RequestFinish();
            return;
        }

        if (State != RoundState.Running)
        {
            Log.Warning("[RoundManager] Cannot finish: round not running");
            return;
        }

        FinishRound_Internal();
    }

    public void PauseRound()
    {
        if (!Networking.IsHost)
        {
            Rpc_RequestPause();
            return;
        }

        if (State != RoundState.Running)
        {
            Log.Warning("[RoundManager] Cannot pause: invalid state");
            return;
        }

        Rpc_BroadcastPause();
    }

    public void ResumeRound()
    {
        if (!Networking.IsHost)
        {
            Rpc_RequestResume();
            return;
        }

        if (State != RoundState.Paused)
        {
            Log.Warning("[RoundManager] Cannot resume: round not paused");
            return;
        }

        Rpc_BroadcastResume();
    }

    // ========================
    // Internals (Host)
    // ========================
    private void PreStart_Internal()
    {
        Log.Info("[RoundManager] Pre-round started");
        OnPreStartRound?.Invoke();

        SetTimer(PreRoundTime);
    }

    private void StartRound_Internal()
    {
        Log.Info("[RoundManager] Round started");

        State = RoundState.Running;
        OnStartRound?.Invoke();
        OnPostStartRound?.Invoke();

        SetTimer(RoundTime);
    }

    private void FinishRound_Internal()
    {
        Log.Info("[RoundManager] Round finished");

        State = RoundState.Finished;
        OnFinishRound?.Invoke();

        if (EnablePostRound)
            SetTimer(PostRoundTime);
        else
            PostFinish_Internal();
    }

    private void PostFinish_Internal()
    {
        Log.Info("[RoundManager] Post-round finished");
        OnPostFinishRound?.Invoke();
    }

    // ========================
    // Timer
    // ========================
    private void SetTimer(float duration)
    {
        TimerFrozen = false;
        SyncedEndTime = Time.Now + duration;
    }

    // ========================
    // RPCs (Client → Host)
    // ========================
    [Rpc.Host]
    private void Rpc_RequestStart() => StartRound();

    [Rpc.Host]
    private void Rpc_RequestFinish() => FinishRound();

    [Rpc.Host]
    private void Rpc_RequestPause() => PauseRound();

    [Rpc.Host]
    private void Rpc_RequestResume() => ResumeRound();

    // ========================
    // RPCs (Broadcast)
    // ========================
    [Rpc.Broadcast]
    private void Rpc_BroadcastPause()
    {
        Log.Info("[RoundManager] Round paused");

        TimerFrozen = true;
        CachedTimeLeft = TimeLeft;
        State = RoundState.Paused;

        OnPauseRound?.Invoke();
    }

    [Rpc.Broadcast]
    private void Rpc_BroadcastResume()
    {
        Log.Info("[RoundManager] Round resumed");

        TimerFrozen = false;
        SyncedEndTime = Time.Now + CachedTimeLeft;
        State = RoundState.Running;

        OnResumeRound?.Invoke();
    }
}

public enum RoundState
{
    NotStarted,
    Running,
    Paused,
    Finished
}