using tgBot.Context;
using tgBot.Entities;

namespace tgBot.Services;

public class StudentService
{
    private readonly TutorDbContext _context;
    public StudentService(TutorDbContext context)
    {
        _context = context;
    }

    public Student GetOrCreateStudent(int telegramId, string fullName, string? phoneNumber = null)
    {
        var student = _context.Students.FirstOrDefault(s => s.TelegramId == telegramId);
        if (student == null)
        {
            student = new Student
            {
                TelegramId = telegramId,
                FullName = fullName,
                PhoneNumber = phoneNumber
            };
            _context.Students.Add(student);
            _context.SaveChanges();
        }
        return student;
    }

    public Student? GetStudentByTelegramId(int telegramId)
    {
        return _context.Students.FirstOrDefault(s => s.TelegramId == telegramId);
    }
}
