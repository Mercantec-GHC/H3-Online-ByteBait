using ByteBait.Data.Entities;

namespace ByteBait.Data.Seed;

// The four lanes. The id is the same as the number, because GET /api/lanes/{laneId}/weather uses 1-4.
internal static class LaneSeed
{
    public const int Nordborg = 1;
    public const int Silkeborg = 2;
    public const int Ringkobing = 3;
    public const int Vejle = 4;

    public static readonly IReadOnlyList<Lane> All =
    [
        new() { Id = Nordborg, Number = 1, Name = "Nordborg Sø", City = "Nordborg", Latitude = 55.06, Longitude = 9.74 },
        new() { Id = Silkeborg, Number = 2, Name = "Gudenåen", City = "Silkeborg", Latitude = 56.17, Longitude = 9.55 },
        new() { Id = Ringkobing, Number = 3, Name = "Ringkøbing Havn", City = "Ringkøbing", Latitude = 56.09, Longitude = 8.24 },
        new() { Id = Vejle, Number = 4, Name = "Open sea off Vejle", City = "Vejle", Latitude = 55.71, Longitude = 9.54 },
    ];
}
