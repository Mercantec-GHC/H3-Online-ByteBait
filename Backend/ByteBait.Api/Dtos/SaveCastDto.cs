using System.ComponentModel.DataAnnotations;

namespace ByteBait.Api.Dtos;

// Request body for POST /api/casts (see docs/api-contract.md).
// The fields are nullable so a missing field gives a 400 instead of a default value.
public class SaveCastDto
{
    [Required]
    [AllowedValues("red", "yellow", "green", ErrorMessage = "Zone must be 'red', 'yellow' or 'green'.")]
    public string? Zone { get; set; }

    [Required]
    [Range(0.0, 1.0, ErrorMessage = "Strength must be between 0.0 and 1.0.")]
    public double? Strength { get; set; }

    [Required]
    public DateTimeOffset? CastAt { get; set; }
}
