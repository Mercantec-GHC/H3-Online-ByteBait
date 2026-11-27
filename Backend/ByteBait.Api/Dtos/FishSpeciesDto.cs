using ByteBait.Data.Entities;

namespace ByteBait.Api.Dtos;

// A fish species in the admin panel (see docs/api-contract.md, "Admin")
public record FishSpeciesDto(
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
    uint Version)
{
    public static FishSpeciesDto From(FishSpecies species) => new(
        species.Id,
        species.LaneId,
        species.Name,
        species.Rarity.ToString().ToLowerInvariant(),
        species.Depth.ToString().ToLowerInvariant(),
        species.MinWeightKg,
        species.MaxWeightKg,
        species.MinLengthCm,
        species.MaxLengthCm,
        species.Price,
        species.Version);
}

// A lane to choose in the species form
public record AdminLaneDto(int Id, int Number, string Name);
