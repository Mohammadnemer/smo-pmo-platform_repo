namespace SmoPmo.Worker;

/// <summary>
/// Bound from configuration section "RollupWorker". Defaults are tuned for a real deploy
/// (debounce a burst of edits over a couple of seconds); tests override both to near-zero, or
/// bypass the timer entirely via <see cref="IRollupHealthProcessor.ProcessDueAsync"/> with
/// <see cref="TimeSpan.Zero"/>, so the "Done when" line doesn't depend on real wall-clock time.
/// </summary>
public sealed class RollupWorkerOptions
{
    public int DebounceMilliseconds { get; set; } = 2000;
    public int PollIntervalMilliseconds { get; set; } = 500;

    public TimeSpan DebounceWindow => TimeSpan.FromMilliseconds(DebounceMilliseconds);
    public TimeSpan PollInterval => TimeSpan.FromMilliseconds(PollIntervalMilliseconds);
}
