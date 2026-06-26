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

    // Last update-check result counts, cached when the Update wizard runs a check
    // (and kept in step as updates are applied). null = no check run this session.
    // The Status dashboard reads these so it can surface "N update(s) available"
    // without itself hitting the network — an offline screen stays offline.
    public int? OutdatedModCount { get; set; }
    public int? OutdatedCollectionCount { get; set; }

    public StoreLayout GetLayout()
    {
        var root = StoreRootResolver.Resolve(StoreOverride).Root;
        return new StoreLayout(root);
    }
}
