
namespace tgBot.Entities;

public class Teacher
{
    public int Id { get; set; }

    public string FullName { get; set; } = null!;

    public string Subjects { get; set; } = null!;

    public string WorkingHours { get; set; } = null!;

    public int? TelegramId { get; set; }

    public virtual ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();
}
