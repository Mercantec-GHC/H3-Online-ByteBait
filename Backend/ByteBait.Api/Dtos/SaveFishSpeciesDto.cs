using System.ComponentModel.DataAnnotations;
using ByteBait.Data.Entities;

namespace ByteBait.Api.Dtos;

// Request body for creating and editing a fish species (see docs/api-contract.md, "Admin").
// The database has the same rules as check constraints, but this way the admin gets a message per field.
public class SaveFishSpeciesDto : IValidatableObject
{
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "LaneId must be a positive number.")]
    public int? LaneId { get; set; }

    [Required]
    [StringLength(50, ErrorMessage = "Name can't be longer than 50 characters.")]
    public string? Name { get; set; }

    [Required]
    [AllowedValues("common", "rare", "legendary", ErrorMessage = "Rarity must be 'common', 'rare' or 'legendary'.")]
    public string? Rarity { get; set; }

    [Required]
    [AllowedValues("shallow", "deep", ErrorMessage = "Depth must be 'shallow' or 'deep'.")]
    public string? Depth { get; set; }

    [Required]
    [Range(0.01, 1000, ErrorMessage = "Weight must be between 0.01 and 1000 kg.")]
    public double? MinWeightKg { get; set; }

    [Required]
    [Range(0.01, 1000, ErrorMessage = "Weight must be between 0.01 and 1000 kg.")]
    public double? MaxWeightKg { get; set; }

    [Required]
    [Range(0.1, 1000, ErrorMessage = "Length must be between 0.1 and 1000 cm.")]
    public double? MinLengthCm { get; set; }

    [Required]
    [Range(0.1, 1000, ErrorMessage = "Length must be between 0.1 and 1000 cm.")]
    public double? MaxLengthCm { get; set; }

    [Required]
    [Range(0, 100_000, ErrorMessage = "Price must be between 0 and 100000.")]
    public int? Price { get; set; }

    // Runs after the checks above, so the values are there. The field names are the JSON names, like the other errors.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MaxWeightKg < MinWeightKg)
        {
            yield return new ValidationResult("Max weight can't be less than min weight.", ["maxWeightKg"]);
        }

        if (MaxLengthCm < MinLengthCm)
        {
            yield return new ValidationResult("Max length can't be less than min length.", ["maxLengthCm"]);
        }
    }

    public void CopyTo(FishSpecies species)
    {
        species.LaneId = LaneId!.Value;
        species.Name = Name!.Trim();
        species.Rarity = Enum.Parse<Rarity>(Rarity!, ignoreCase: true);
        species.Depth = Enum.Parse<WaterDepth>(Depth!, ignoreCase: true);
        species.MinWeightKg = MinWeightKg!.Value;
        species.MaxWeightKg = MaxWeightKg!.Value;
        species.MinLengthCm = MinLengthCm!.Value;
        species.MaxLengthCm = MaxLengthCm!.Value;
        species.Price = Price!.Value;
    }
}
