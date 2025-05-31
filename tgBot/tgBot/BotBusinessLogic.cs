using tgBot.Context;
using tgBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace tgBot;

public static class BotBusinessLogic
{
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddDbContext<TutorDbContext>();
        services.AddScoped<StudentService>();
        services.AddScoped<TeacherService>();
        services.AddScoped<LessonService>();
        services.AddScoped<SubjectService>();
        services.AddScoped<SlotService>();
    }
}
