// The admin endpoints' JSON (see docs/api-contract.md, "Admin")
using System.ComponentModel.DataAnnotations;

namespace ByteBait.Client.Admin;

public sealed record Species(
    int Id,
    int LaneId,
    string Name,
    string Rarity,
    string Depth,
    double MinWeightKg,
    double MaxWeightKg,
    double MinLengthCm,
    double MaxLengthCm,
    int Price,
    uint Version);

public sealed record AdminLane(int Id, int Number, string Name);

// The species form. The rules are the same as the API's, so most mistakes are shown before anything is sent.
public sealed class SpeciesForm : IValidatableObject
{
    public static readonly string[] Rarities = ["common", "rare", "legendary"];
    public static readonly string[] Depths = ["shallow", "deep"];

    [Range(1, int.MaxValue, ErrorMessage = "Choose a lane.")]
    public int LaneId { get; set; }

    [Required(ErrorMessage = "Enter a name.")]
    [StringLength(50, ErrorMessage = "Name can't be longer than 50 characters.")]
    public string Name { get; set; } = "";

    [Required]
    public string Rarity { get; set; } = "common";

    [Required]
    public string Depth { get; set; } = "shallow";

    [Range(0.01, 1000, ErrorMessage = "Weight must be between 0.01 and 1000 kg.")]
    public double MinWeightKg { get; set; } = 1;

    [Range(0.01, 1000, ErrorMessage = "Weight must be between 0.01 and 1000 kg.")]
    public double MaxWeightKg { get; set; } = 2;

    [Range(0.1, 1000, ErrorMessage = "Length must be between 0.1 and 1000 cm.")]
    public double MinLengthCm { get; set; } = 20;

    [Range(0.1, 1000, ErrorMessage = "Length must be between 0.1 and 1000 cm.")]
    public double MaxLengthCm { get; set; } = 40;

    [Range(0, 100_000, ErrorMessage = "Price must be between 0 and 100000.")]
    public int Price { get; set; } = 10;

    // The version of the species when the form was opened (null for a new one). The API turns down a save
    // from a form opened before someone else saved (409 "Species has changed").
    public uint? Version { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MaxWeightKg < MinWeightKg)
        {
            yield return new ValidationResult("Max weight can't be less than min weight.", [nameof(MaxWeightKg)]);
        }

        if (MaxLengthCm < MinLengthCm)
        {
            yield return new ValidationResult("Max length can't be less than min length.", [nameof(MaxLengthCm)]);
        }
    }

    public static SpeciesForm From(Species species) => new()
    {
        LaneId = species.LaneId,
        Name = species.Name,
        Rarity = species.Rarity,
        Depth = species.Depth,
        MinWeightKg = species.MinWeightKg,
        MaxWeightKg = species.MaxWeightKg,
        MinLengthCm = species.MinLengthCm,
        MaxLengthCm = species.MaxLengthCm,
        Price = species.Price,
        Version = species.Version,
    };
}
