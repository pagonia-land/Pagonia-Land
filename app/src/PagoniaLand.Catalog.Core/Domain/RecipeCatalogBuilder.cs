using PagoniaLand.Catalog.Model;

namespace PagoniaLand.Catalog.Domain;

/// <summary>
/// Projects the recipe catalog from a <see cref="GameDatabase"/>: every entity with a
/// <c>ProductionRecipe</c> component, walking its production steps to collect input/output
/// resources and work steps. A faithful slice of <c>scripts/generate_catalog.ps1</c>.
/// </summary>
public static class RecipeCatalogBuilder
{
    public static IReadOnlyList<RecipeEntry> Build(GameDatabase database)
    {
        var rows = new List<RecipeEntry>();

        foreach (var entity in database.Entities)
        {
            var recipe = entity.Component("ProductionRecipe");
            if (recipe is null)
            {
                continue;
            }

            var inputs = new List<Reference>();
            var outputs = new List<Reference>();
            var stepTypes = new List<string>();
            var workSteps = 0;

            foreach (var step in DomainText.Contents(recipe, "ProductionSteps"))
            {
                var type = DomainText.Text(step, "Type");
                if (!string.IsNullOrWhiteSpace(type))
                {
                    stepTypes.Add(type);
                }

                var io = step.Element("InputOutput");
                var resource = DomainText.Text(io, "Resource");
                if (!DomainText.IsAbsentGuid(resource))
                {
                    var reference = new Reference(database.ResolveName(resource), resource, DomainText.Text(io, "Amount"));
                    if (type == "Input")
                    {
                        inputs.Add(reference);
                    }
                    else if (type == "Output")
                    {
                        outputs.Add(reference);
                    }
                }

                if (type == "Work")
                {
                    workSteps++;
                }
            }

            rows.Add(new RecipeEntry(
                Package: entity.Package,
                Name: entity.Name,
                Guid: entity.Guid,
                File: entity.File,
                Identifier: DomainText.Text(recipe, "RecipeIdentifier"),
                DefaultState: DomainText.Text(recipe, "DefaultState"),
                Inputs: DomainText.Aggregate(inputs),
                Outputs: DomainText.Aggregate(outputs),
                WorkSteps: workSteps,
                StepTypes: stepTypes.Distinct().ToList(),
                Components: entity.ValueTypes));
        }

        return rows;
    }
}
