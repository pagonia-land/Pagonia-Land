namespace PagoniaLand.Manager;

/// <summary>
/// One progress tick from a long-running orchestration call (deploy / rollback /
/// pak-cache extract). Flows one way — emitted, rendered, discarded — so it stays
/// a flat three-field record rather than a subclass hierarchy: a richer tree would
/// invite Core consumers to leak presentation concerns back into the engine.
/// </summary>
/// <param name="Stage">Stable machine identifier for the phase this tick belongs
/// to (<c>"extract"</c>, <c>"plan"</c>, <c>"apply"</c>, <c>"repack"</c>,
/// <c>"restore"</c>, …). A GUI can switch on it; a CLI ignores it.</param>
/// <param name="Percent">0–100 completion within the stage, or <c>null</c> when a
/// percentage isn't meaningful or knowable (e.g. a single indeterminate step). A
/// GUI binds a progress bar to it; the CLI ignores it.</param>
/// <param name="Message">The human-readable line — identical to what the previous
/// <c>Action&lt;string&gt;</c> callback emitted, so the CLI's one-line rendering is
/// unchanged.</param>
public sealed record DeployProgress(string Stage, int? Percent, string Message);
