namespace tgBot.Entities;

public class AvailableSlot
{
    public int Id { get; set; }
    public int TeacherId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool IsBooked { get; set; } = false;
    public string Subject { get; set; } = null!;
    public virtual Teacher Teacher { get; set; } = null!;
}
