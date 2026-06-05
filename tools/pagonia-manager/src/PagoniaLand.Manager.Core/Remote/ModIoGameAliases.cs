namespace PagoniaLand.Manager;

/// <summary>
/// Slug → numeric-id map for mod.io game references. The manager only
/// targets Pioneers of Pagonia today; both the long slug
/// <c>pioneers-of-pagonia</c> and the short alias <c>pop</c> resolve to
/// the numeric mod.io id <c>8242</c>. Anything else is rejected with
/// <c>manager.modIoUnknownGameAlias</c>.
/// </summary>
public static class ModIoGameAliases
{
    public const string PioneersOfPagoniaSlug = "pioneers-of-pagonia";
    public const string PioneersOfPagoniaShortSlug = "pop";
    public const string PioneersOfPagoniaGameId = "8242";

    /// <summary>
    /// Resolve the <c>&lt;game&gt;</c> segment to a numeric mod.io game id.
    /// Numeric input passes through unchanged (mod.io's primary key is the
    /// numeric id; we don't second-guess the user typing one); the
    /// recognised slugs resolve to their numeric form (case-insensitive);
    /// anything else returns false.
    /// </summary>
    public static bool TryResolve(string game, out string numericId)
    {
        if (game.Length > 0 && game.All(char.IsDigit))
        {
            numericId = game;
            return true;
        }
        if (string.Equals(game, PioneersOfPagoniaSlug, StringComparison.OrdinalIgnoreCase)
            || string.Equals(game, PioneersOfPagoniaShortSlug, StringComparison.OrdinalIgnoreCase))
        {
            numericId = PioneersOfPagoniaGameId;
            return true;
        }
        numericId = string.Empty;
        return false;
    }

    /// <summary>
    /// Human-readable description of the accepted forms, used in error
    /// messages so an unknown <c>&lt;game&gt;</c> spec shows the user what
    /// IS accepted.
    /// </summary>
    public static string Describe()
        => $"a numeric mod.io game id (Pioneers of Pagonia is {PioneersOfPagoniaGameId}) or the slug '{PioneersOfPagoniaSlug}' / '{PioneersOfPagoniaShortSlug}'";
}
