using ByteBait.Data.Entities;
using Microsoft.AspNetCore.Identity;

namespace ByteBait.Data.Seed;

// Ordinary players have no role, so admin is the only one.
// The id and stamp are fixed. Identity makes up a new stamp by default, and then EF would think the row had changed.
internal static class RoleSeed
{
    public static readonly IReadOnlyList<IdentityRole<Guid>> All =
    [
        new()
        {
            Id = new Guid("6f0f3f5e-52a1-4b0e-9d5c-0c1b7c2a8e41"),
            Name = RoleNames.Admin,
            NormalizedName = "ADMIN",
            ConcurrencyStamp = "b7d0c1a4-1f6e-4c55-8a37-d1b4e0f29c63",
        },
    ];
}
