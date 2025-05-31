using tgBot.Context;
using tgBot.Entities;

namespace tgBot.Services;

public class LessonService
{
    private readonly TutorDbContext _context;
    public LessonService(TutorDbContext context)
    {
        _context = context;
    }

    public List<Lesson> GetLessonsByStudent(int studentId)
    {
        return _context.Lessons.Where(l => l.StudentId == studentId).ToList();
    }

    public List<Lesson> GetLessonsByTeacherAndSubject(int teacherId, string subject)
    {
        return _context.Lessons.Where(l => l.TeacherId == teacherId && l.Subject == subject).ToList();
    }

    public bool IsSlotAvailable(int teacherId, DateTime start, DateTime end)
    {
        return !_context.Lessons.Any(l => l.TeacherId == teacherId &&
            ((l.StartTime < end && l.EndTime > start)));
    }

    public Lesson? BookLesson(int studentId, int teacherId, string subject, DateTime start, DateTime end)
    {
        if (!IsSlotAvailable(teacherId, start, end))
            return null;
        var lesson = new Lesson
        {
            StudentId = studentId,
            TeacherId = teacherId,
            Subject = subject,
            StartTime = start,
            EndTime = end,
            IsConfirmed = false
        };
        _context.Lessons.Add(lesson);
        _context.SaveChanges();
        return lesson;
    }
}
