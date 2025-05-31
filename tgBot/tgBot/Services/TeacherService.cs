using tgBot.Context;
using tgBot.Entities;

namespace tgBot.Services;

public class TeacherService
{
    private readonly TutorDbContext _context;
    public TeacherService(TutorDbContext context)
    {
        _context = context;
    }

    public Teacher? GetTeacher()
    {
        return _context.Teachers.FirstOrDefault();
    }

    public List<string> GetSubjects()
    {
        var teacher = GetTeacher();
        if (teacher == null) return new List<string>();
        return teacher.Subjects.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim()).ToList();
    }
}
