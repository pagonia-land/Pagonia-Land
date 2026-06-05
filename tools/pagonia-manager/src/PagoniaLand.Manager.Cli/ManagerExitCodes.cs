namespace PagoniaLand.Manager.Cli;

/// <summary>
/// Process exit codes the CLI binary returns. Exit codes are a shell-process
/// concept — the Core stays neutral about how a caller (CLI vs a future GUI that
/// shows a dialog) translates a failure — so this type lives in the CLI project,
/// not in <c>PagoniaLand.Manager.Core</c>. Mirrors the patcher / paker exit codes
/// for downstream-tool compatibility.
/// </summary>
public static class ManagerExitCodes
{
    public const int Success = 0;
    public const int Error = 1;
    public const int Conflict = 2;
    public const int Usage = 64;
}
