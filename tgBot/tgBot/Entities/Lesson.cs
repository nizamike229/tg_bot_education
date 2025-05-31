
namespace tgBot.Entities;

public class Lesson
{
    public int Id { get; set; }

    public int TeacherId { get; set; }

    public int StudentId { get; set; }

    public string Subject { get; set; } = null!;

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public bool? IsConfirmed { get; set; }

    public virtual Student Student { get; set; } = null!;

    public virtual Teacher Teacher { get; set; } = null!;
}
