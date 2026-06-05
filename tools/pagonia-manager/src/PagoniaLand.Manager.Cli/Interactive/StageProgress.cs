namespace PagoniaLand.Manager.Cli.Interactive;

/// <summary>
/// Adapts the Core's structured <see cref="IProgress{T}"/> of
/// <see cref="DeployProgress"/> back to the CLI's line-oriented stage printer.
/// Deliberately a synchronous, direct-dispatch sink (not <see cref="Progress{T}"/>,
/// which posts to the threadpool and would reorder / delay the ticks): the deploy
/// already runs on a background <c>Task.Run</c> thread, so invoking the action
/// inline here keeps the stage lines ordered and immediate exactly as before. The
/// CLI ignores <c>Stage</c> / <c>Percent</c> and renders only the message.
/// </summary>
internal sealed class StageProgress : IProgress<DeployProgress>
{
    private readonly Action<string> _onMessage;

    public StageProgress(Action<string> onMessage) => _onMessage = onMessage;

    public void Report(DeployProgress value) => _onMessage(value.Message);
}
