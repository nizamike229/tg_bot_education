
namespace tgBot.Entities;

public class Student
{
    public int Id { get; set; }

    public string FullName { get; set; } = null!;

    public int TelegramId { get; set; }

    public string? PhoneNumber { get; set; }

    public virtual ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();
}
