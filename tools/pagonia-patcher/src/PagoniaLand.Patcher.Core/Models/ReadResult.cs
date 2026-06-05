namespace PagoniaLand.Patcher;

public sealed class ReadResult<T>
{
    private ReadResult(T? value, IReadOnlyList<PatchDiagnostic> diagnostics)
    {
        Value = value;
        Diagnostics = diagnostics;
    }

    public T? Value { get; }

    public IReadOnlyList<PatchDiagnostic> Diagnostics { get; }

    public bool Success => Diagnostics.All(diagnostic => diagnostic.Severity != PatchDiagnosticSeverity.Error);

    public static ReadResult<T> Ok(T value, params PatchDiagnostic[] diagnostics)
        => new(value, diagnostics);

    public static ReadResult<T> Failed(params PatchDiagnostic[] diagnostics)
        => new(default, diagnostics);
}
