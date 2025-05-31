
namespace tgBot.Services;

public class SubjectService
{
    private readonly TeacherService _teacherService;
    public SubjectService(TeacherService teacherService)
    {
        _teacherService = teacherService;
    }

    public List<string> GetSubjects()
    {
        return _teacherService.GetSubjects();
    }
}
