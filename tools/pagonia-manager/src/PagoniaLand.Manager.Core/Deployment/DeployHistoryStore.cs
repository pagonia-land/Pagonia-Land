using System.Diagnostics.CodeAnalysis;

namespace PagoniaLand.Manager;

public sealed class DeployHistoryStore
{
    // AOT: pin the deploy-history model types so YamlDotNet survives trimming.
    private const DynamicallyAccessedMemberTypes Shape =
        DynamicallyAccessedMemberTypes.PublicConstructors
        | DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.PublicFields;

    [DynamicDependency(Shape, typeof(DeployHistory))]
    [DynamicDependency(Shape, typeof(DeployHistoryEntry))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(List<DeployHistoryEntry>))]
    public DeployHistoryStore()
    {
    }

    public bool Exists(StoreLayout layout, string fingerprint)
        => File.Exists(layout.DeployHistoryFile(fingerprint));

    /// <summary>
    /// Read the history. Throws InvalidOperationException on empty / invalid
    /// YAML for backwards compatibility with existing tests. Service callers
    /// should prefer <see cref="TryRead"/>, which converts both failure modes
    /// into a diagnostic-friendly error string instead of unwinding the stack.
    /// </summary>
    public DeployHistory Read(StoreLayout layout, string fingerprint)
    {
        var path = layout.DeployHistoryFile(fingerprint);
        if (!File.Exists(path))
        {
            return new DeployHistory
            {
                DeployHistoryVersion = StoreLayoutConstants.CurrentDeployVersion,
                GameFingerprint = fingerprint,
            };
        }

        var yaml = File.ReadAllText(path);
        var history = ManagerYaml.CreateDeserializer().Deserialize<DeployHistory>(yaml);
        if (history is null)
        {
            throw new InvalidOperationException(
                $"[{ManagerDiagnosticCodes.DeployHistoryUnreadable}] " +
                $"Deploy history at '{path}' is empty or invalid.");
        }

        return history;
    }

    /// <summary>
    /// Diagnostic-friendly variant of <see cref="Read"/>: returns true on success
    /// (file missing → empty DeployHistory; file valid → parsed), false with an
    /// `error` message string on empty / malformed YAML. Use this in service
    /// code paths so a corrupt history.yaml surfaces as a
    /// <c>manager.deployHistoryUnreadable</c> diagnostic rather than an
    /// unhandled stack trace.
    /// </summary>
    public bool TryRead(
        StoreLayout layout,
        string fingerprint,
        [NotNullWhen(true)] out DeployHistory? history,
        [NotNullWhen(false)] out string? error)
    {
        var path = layout.DeployHistoryFile(fingerprint);
        if (!File.Exists(path))
        {
            history = new DeployHistory
            {
                DeployHistoryVersion = StoreLayoutConstants.CurrentDeployVersion,
                GameFingerprint = fingerprint,
            };
            error = null;
            return true;
        }

        try
        {
            var yaml = File.ReadAllText(path);
            var parsed = ManagerYaml.CreateDeserializer().Deserialize<DeployHistory>(yaml);
            if (parsed is null)
            {
                history = null;
                error = $"Deploy history at '{path}' is empty or invalid YAML.";
                return false;
            }
            history = parsed;
            error = null;
            return true;
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            history = null;
            error = $"Deploy history at '{path}' could not be parsed: {ex.Message}";
            return false;
        }
    }

    public void Write(StoreLayout layout, string fingerprint, DeployHistory history)
    {
        var path = layout.DeployHistoryFile(fingerprint);
        Directory.CreateDirectory(layout.DeployFingerprintDirectory(fingerprint));
        AtomicFile.WriteAllText(path, ManagerYaml.CreateSerializer().Serialize(history));
    }
}
