namespace PagoniaLand.Manager.Cli.Interactive;

/// <summary>
/// Plain-Console progress printer used by long-running wizard stages
/// (extract / plan / apply / backup / rebuild). One stage at a time: print
/// "  → label" on its own line, then append a "." every 500 ms while the
/// stage runs, then newline when the caller starts a new stage or disposes.
/// <para>Reason this exists alongside Spectre.Console's
/// <c>AnsiConsole.Status()</c>: Status renders via ANSI cursor-up + line-clear
/// escapes, which not every Windows console / terminal renders as an animated
/// single line. Users on those terminals see a frozen spinner glyph with no
/// movement, which looks identical to a hung program. The dot-ticker uses
/// only plain ASCII writes — works on every terminal that can print a
/// character.</para>
/// </summary>
internal sealed class StagePrinter : IDisposable
{
    private System.Threading.Timer? _timer;
    private bool _stageOpen;
    private readonly object _lock = new();

    /// <summary>
    /// End any in-progress stage (newline) and start a new one with
    /// <paramref name="label"/>. Subsequent dots from the internal timer
    /// append to this stage's line.
    /// </summary>
    public void Start(string label)
    {
        lock (_lock)
        {
            FinishCurrentStageLine();
            Console.Write($"  -> {label}");
            Console.Out.Flush();
            _stageOpen = true;
            // First dot fires after 500 ms so very fast stages (cache hit,
            // already-staged apply) don't get spurious dots. Repeat at 500 ms.
            _timer = new System.Threading.Timer(_ => Tick(), null, 500, 500);
        }
    }

    /// <summary>Stop dots and finish the current line. Safe to call repeatedly;
    /// no-op once the printer is idle.</summary>
    public void Stop()
    {
        lock (_lock)
        {
            FinishCurrentStageLine();
        }
    }

    public void Dispose() => Stop();

    private void Tick()
    {
        // Timer callbacks run on the threadpool; guard against a Stop()
        // happening between the timer fire and this method body.
        lock (_lock)
        {
            if (!_stageOpen) return;
            Console.Write('.');
            Console.Out.Flush();
        }
    }

    private void FinishCurrentStageLine()
    {
        if (_timer is not null)
        {
            _timer.Dispose();
            _timer = null;
        }
        if (_stageOpen)
        {
            Console.WriteLine();
            Console.Out.Flush();
            _stageOpen = false;
        }
    }
}
