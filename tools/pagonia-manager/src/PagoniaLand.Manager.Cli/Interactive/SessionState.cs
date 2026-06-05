using PagoniaLand.Manager;

namespace PagoniaLand.Manager.Cli.Interactive;

// Mutable per-session config — wizards read these and prompt for what's still null.
// Lifetime is one shell invocation; nothing persists across runs.
internal sealed class SessionState
{
    // null = use the resolved platform/env default via StoreRootResolver.
    public string? StoreOverride { get; set; }

    // null = prompt the user before any operation that touches a game install.
    public string? GameRoot { get; set; }

    public StoreLayout GetLayout()
    {
        var root = StoreRootResolver.Resolve(StoreOverride).Root;
        return new StoreLayout(root);
    }
}
