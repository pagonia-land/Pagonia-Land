namespace PagoniaLand.Catalog.Domain;

/// <summary>
/// A resolved reference to another entity: its <see cref="Name"/> and <see cref="Guid"/>
/// (so the UI can both display the name and navigate to the target), with an optional
/// <see cref="Amount"/> for costs/recipes.
/// </summary>
public sealed record Reference(string Name, string Guid, string Amount = "")
{
    /// <summary>Display text — "amount name", or just the name when the amount means "one"
    /// (absent or a value reading as 1), so a count shows only for real multiplicity (≥2). The
    /// "don't print a leading 1" rule lives once in <see cref="DomainText.IsImpliedSingle"/>.</summary>
    public string Display => DomainText.IsImpliedSingle(Amount) ? Name : $"{Amount.Trim()} {Name}";
}
