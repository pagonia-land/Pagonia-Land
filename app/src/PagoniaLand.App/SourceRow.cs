namespace PagoniaLand.App;

/// <summary>A pre-formatted row for the Overview "Sources" table (one pak read from the install).</summary>
public sealed record SourceRow(string Name, string Size, string Entries, string GameDatabase, string Assets);
