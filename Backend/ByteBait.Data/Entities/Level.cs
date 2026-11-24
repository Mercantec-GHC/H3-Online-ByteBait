namespace ByteBait.Data.Entities;

// A lane has two levels. The player clears a level by catching RequiredFish fish on it.
// Progress isn't stored here, it is counted from the player's catches on the level.
public class Level
{
    public int Id { get; set; }

    public int LaneId { get; set; }

    public Lane Lane { get; set; } = null!;

    // 1 or 2 within the lane
    public int Number { get; set; }

    public int RequiredFish { get; set; }
}
