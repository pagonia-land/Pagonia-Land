namespace PagoniaLand.Paker;

/// <summary>
/// Process exit codes. Unlike the patcher and manager, the paker deliberately has
/// no dedicated Conflict code: pak-level conflicts (e.g. an add that collides with
/// an existing entry) are reported as diagnostics and map to the generic Error (1).
/// </summary>
public static class PakerExitCodes
{
    public const int Success = 0;
    public const int Error = 1;
    public const int Usage = 64;
}
