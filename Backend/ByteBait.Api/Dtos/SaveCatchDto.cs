using System.ComponentModel.DataAnnotations;

namespace ByteBait.Api.Dtos;

// Request body for POST /api/catches (see docs/api-contract.md).
// Only the result is sent. The fish itself was picked by the server when the cast was saved.
public class SaveCatchDto
{
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "CastId must be a positive number.")]
    public int? CastId { get; set; }

    [Required]
    [AllowedValues("caught", "lost", ErrorMessage = "Result must be 'caught' or 'lost'.")]
    public string? Result { get; set; }
}
