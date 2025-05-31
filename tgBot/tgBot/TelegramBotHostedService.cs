using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using tgBot.Services;
using System.Text.RegularExpressions;
using tgBot.Context;

namespace tgBot;

public class TelegramBotHostedService : BackgroundService
{
    private readonly ITelegramBotClient _botClient;
    private readonly IServiceProvider _serviceProvider;
    private static readonly Dictionary<long, UserState> UserStates = new();
    private static readonly Dictionary<long, string> VerifiedUsers = new();
    private static readonly Dictionary<long, string> PendingCodes = new();
    private static readonly HttpClient httpClient = new();
    private const string CallMeBotApiKey = "6181691";

    public TelegramBotHostedService(ITelegramBotClient botClient, IServiceProvider serviceProvider)
    {
        _botClient = botClient;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _botClient.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            cancellationToken: stoppingToken
        );
        await Task.Delay(-1, stoppingToken);
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var studentService = scope.ServiceProvider.GetRequiredService<StudentService>();
        var teacherService = scope.ServiceProvider.GetRequiredService<TeacherService>();
        var lessonService = scope.ServiceProvider.GetRequiredService<LessonService>();
        var subjectService = scope.ServiceProvider.GetRequiredService<SubjectService>();
        var slotService = scope.ServiceProvider.GetRequiredService<SlotService>();
        
        if (update.Type == UpdateType.CallbackQuery)
        {
            var callback = update.CallbackQuery;
            var callbackChatId = callback.Message?.Chat.Id ?? throw new InvalidOperationException("Message or Chat ID is null");
            using var callbackScope = _serviceProvider.CreateScope();
            var callbackStudentService = callbackScope.ServiceProvider.GetRequiredService<StudentService>();
            var callbackTeacherService = callbackScope.ServiceProvider.GetRequiredService<TeacherService>();
            var callbackLessonService = callbackScope.ServiceProvider.GetRequiredService<LessonService>();

            if (callback.Data!.StartsWith("lesson_"))
            {
                var studentId = int.Parse(VerifiedUsers[callbackChatId]);
                if (!int.TryParse(callback.Data.Replace("lesson_", ""), out var lessonId))
                {
                    await botClient.SendMessage(callbackChatId, "Некорректный идентификатор записи.");
                    return;
                }

                var lesson = callbackLessonService.GetLessonsByStudent(studentId).FirstOrDefault(l => l.Id == lessonId);
                if (lesson == null)
                {
                    await botClient.SendMessage(callbackChatId, "Запись не найдена.");
                    return;
                }

                var teacher = callbackTeacherService.GetTeacher();
                var detail = $"📚 <b>Предмет:</b> {lesson.Subject}\n" +
                           $"👨‍🏫 <b>Преподаватель:</b> {teacher?.FullName ?? "-"}\n" +
                           $"📅 <b>Дата:</b> {lesson.StartTime:dd.MM.yyyy}\n" +
                           $"⏰ <b>Время:</b> {lesson.StartTime:HH:mm} - {lesson.EndTime:HH:mm}";

                var detailKeyboard = new InlineKeyboardMarkup(new[]
                {
                    InlineKeyboardButton.WithCallbackData("❌ Отменить запись", $"cancel_{lesson.Id}")
                });

                await botClient.SendMessage(callbackChatId, detail, parseMode: ParseMode.Html, replyMarkup: detailKeyboard);
                await botClient.AnswerCallbackQuery(callback.Id);
                return;
            }

            if (callback.Data!.StartsWith("cancel_"))
            {
                var studentId = int.Parse(VerifiedUsers[callbackChatId]);
                if (!int.TryParse(callback.Data.Replace("cancel_", ""), out var lessonId))
                {
                    await botClient.SendMessage(callbackChatId, "Некорректный идентификатор записи.");
                    return;
                }

                var lesson = callbackLessonService.GetLessonsByStudent(studentId).FirstOrDefault(l => l.Id == lessonId);
                if (lesson == null)
                {
                    await botClient.SendMessage(callbackChatId, "Запись не найдена.");
                    return;
                }

                var slot = slotService.GetSlotById(lesson.Id);
                if (slot != null)
                {
                    slot.IsBooked = false;
                    callbackScope.ServiceProvider.GetRequiredService<TutorDbContext>().SaveChanges();
                }

                callbackScope.ServiceProvider.GetRequiredService<TutorDbContext>().Lessons.Remove(lesson);
                callbackScope.ServiceProvider.GetRequiredService<TutorDbContext>().SaveChanges();

                await botClient.SendMessage(callbackChatId, "✅ Запись успешно отменена!");
                await botClient.AnswerCallbackQuery(callback.Id);

                var lessons = callbackLessonService.GetLessonsByStudent(studentId);
                if (lessons.Count > 0)
                {
                    var inlineKeyboard = new InlineKeyboardMarkup(
                        lessons.Select(l => new[] {
                            InlineKeyboardButton.WithCallbackData($"📚 {l.Subject} {l.StartTime:dd.MM HH:mm}", $"lesson_{l.Id}")
                        })
                    );
                    await botClient.SendMessage(callbackChatId, "Ваши текущие записи:", replyMarkup: inlineKeyboard);
                }
                return;
            }
            return;
        }

        if (update.Type != UpdateType.Message || update.Message!.Type != MessageType.Text)
            return;
        var message = update.Message;
        var chatId = message.Chat.Id;
        
        if (!UserStates.ContainsKey(chatId))
            UserStates[chatId] = new UserState();
        var state = UserStates[chatId];

        if (VerifiedUsers.ContainsKey(chatId))
        {
            state.StudentId = int.Parse(VerifiedUsers[chatId]);
        }

        if (message.Text == "/start")
        {
            var student = studentService.GetStudentByTelegramId((int)chatId);
            if (student != null)
            {
                state.StudentId = student.Id;
                VerifiedUsers[chatId] = student.Id.ToString();
                var kb = new ReplyKeyboardMarkup(new[]
                {
                    new[] { new KeyboardButton("📝 Записаться") },
                    new[] { new KeyboardButton("📋 Мои записи") }
                })
                {
                    ResizeKeyboard = true,
                    OneTimeKeyboard = false
                };
                await botClient.SendMessage(chatId, "Личный кабинет. Выберите действие:", replyMarkup: kb);
                return;
            }
            state.Reset();
            await botClient.SendMessage(chatId, "Добро пожаловать! Введите ваше ФИО:");
            state.Step = 1;
            return;
        }

        if (VerifiedUsers.ContainsKey(chatId) &&
            (message.Text == "Личный кабинет" ||
             message.Text == "Мои записи" || message.Text == "📋 Мои записи" ||
             message.Text == "Записаться" || message.Text == "📝 Записаться"))
        {
            var studentId = int.Parse(VerifiedUsers[chatId]);
            if (message.Text == "📋 Мои записи" || message.Text == "Мои записи")
            {
                var lessons = lessonService.GetLessonsByStudent(studentId);
                if (lessons.Count == 0)
                {
                    await botClient.SendMessage(chatId, "У вас нет записей на занятия.");
                }
                else
                {
                    var inlineKeyboard = new InlineKeyboardMarkup(
                        lessons.Select(l => new[] {
                            InlineKeyboardButton.WithCallbackData($"📚 {l.Subject} {l.StartTime:dd.MM HH:mm}", $"lesson_{l.Id}")
                        })
                    );
                    await botClient.SendMessage(chatId, "Ваши записи:", replyMarkup: inlineKeyboard);
                }
                return;
            }
            if (message.Text == "📝 Записаться" || message.Text == "Записаться")
            {
                state.Reset();
                state.StudentId = studentId;
                var subjects = subjectService.GetSubjects();
                state.Subjects = subjects;
                var keyboard = new ReplyKeyboardMarkup(
                    subjects.Select(s => new List<KeyboardButton> { new KeyboardButton(s) })
                )
                {
                    ResizeKeyboard = true,
                    OneTimeKeyboard = true
                };
                await botClient.SendMessage(chatId, "Выберите предмет:", replyMarkup: keyboard);
                state.Step = 2;
                return;
            }
        }

        switch (state.Step)
        {
            case 1:
                state.FullName = message.Text!;
                var subjects = subjectService.GetSubjects();
                state.Subjects = subjects;
                if (subjects.Count == 0)
                {
                    await botClient.SendMessage(chatId, "Нет доступных предметов для записи. Обратитесь к администратору.");
                    state.Reset();
                    break;
                }
                var keyboard = new ReplyKeyboardMarkup(
                    subjects.Select(s => new List<KeyboardButton> { new KeyboardButton(s) })
                )
                {
                    ResizeKeyboard = true,
                    OneTimeKeyboard = true
                };
                await botClient.SendMessage(chatId, "Выберите предмет:", replyMarkup: keyboard);
                state.Step = 2;
                break;
            case 2:
                if (!state.Subjects.Contains(message.Text!))
                {
                    await botClient.SendMessage(chatId, "Пожалуйста, выберите предмет из списка.");
                    return;
                }
                state.Subject = message.Text!;
                var teacher = teacherService.GetTeacher();
                if (teacher == null)
                {
                    await botClient.SendMessage(chatId, "Репетитор не найден.");
                    state.Reset();
                    return;
                }
                var slots = slotService.GetAvailableSlots(teacher.Id, state.Subject);
                if (slots.Count == 0)
                {
                    await botClient.SendMessage(chatId, "Нет доступных слотов по этому предмету. Попробуйте выбрать другой предмет.");
                    state.Step = 1;
                    return;
                }
                state.Step = 3;
                state.SlotIds = slots.Select(s => s.Id).ToList();
                var slotKeyboard = new ReplyKeyboardMarkup(
                    slots.Select(s => new List<KeyboardButton> { new KeyboardButton($"{s.StartTime:yyyy-MM-dd HH:mm} - {s.EndTime:HH:mm}") }).ToList()
                )
                {
                    ResizeKeyboard = true,
                    OneTimeKeyboard = true
                };
                state.SlotMap = slots.ToDictionary(
                    s => $"{s.StartTime:yyyy-MM-dd HH:mm} - {s.EndTime:HH:mm}",
                    s => s.Id
                );
                await botClient.SendMessage(chatId, "Выберите удобный слот:", replyMarkup: slotKeyboard);
                break;
            case 3:
                if (state.SlotMap == null || !state.SlotMap.ContainsKey(message.Text!))
                {
                    await botClient.SendMessage(chatId, "Пожалуйста, выберите слот из предложенных.");
                    return;
                }
                state.SelectedSlotId = state.SlotMap[message.Text!];
                await botClient.SendMessage(chatId, "Введите ваш номер телефона для подтверждения (в формате +77XXXXXXXXX или 87XXXXXXXXX):");
                state.Step = 4;
                break;
            case 4:
                state.Phone = message.Text!;
                var phonePattern = @"^(\+7|8)7\d{9}$";
                if (!Regex.IsMatch(state.Phone, phonePattern))
                {
                    await botClient.SendMessage(chatId, "Введите корректный казахстанский номер в формате +77XXXXXXXXX или 87XXXXXXXXX:");
                    return;
                }
                var code = new Random().Next(1000, 9999).ToString();
                PendingCodes[chatId] = code;
                var phoneForApi = state.Phone.Replace("+", "");
                var text = Uri.EscapeDataString($"Ваш код: {code}");
                var url = $"https://api.callmebot.com/whatsapp.php?phone={phoneForApi}&text={text}&apikey={CallMeBotApiKey}";
                try
                {
                    var resp = await httpClient.GetAsync(url);
                    var respText = await resp.Content.ReadAsStringAsync();
                    if (!resp.IsSuccessStatusCode || respText.Contains("Message not sent") || respText.Contains("error"))
                    {
                        await botClient.SendMessage(chatId, $"Ошибка отправки кода: {respText}\nПопробуйте позже или проверьте номер (должен быть зарегистрирован в WhatsApp).");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    await botClient.SendMessage(chatId, $"Ошибка отправки кода: {ex.Message}");
                    return;
                }
                await botClient.SendMessage(chatId, "Введите код, который пришёл вам в WhatsApp:");
                state.Step = 5;
                break;
            case 5:
                if (!PendingCodes.ContainsKey(chatId) || message.Text != PendingCodes[chatId])
                {
                    await botClient.SendMessage(chatId, "Неверный код. Попробуйте ещё раз:");
                    return;
                }
                // Сначала создаём студента в БД
                var student = studentService.GetOrCreateStudent((int)chatId, state.FullName!, state.Phone);
                state.StudentId = student.Id;
                VerifiedUsers[chatId] = student.Id.ToString(); // сохраняем studentId как признак верификации
                PendingCodes.Remove(chatId);
                var slotToBook = slotService.GetSlotById(state.SelectedSlotId);
                if (slotToBook == null || slotToBook.IsBooked)
                {
                    await botClient.SendMessage(chatId, "Этот слот уже занят. Попробуйте выбрать другой.");
                    state.Step = 2;
                    return;
                }
                var allSlotsThisTime = slotService.GetAvailableSlots(teacherService.GetTeacher().Id, null)
                    .Where(s => s.StartTime == slotToBook.StartTime && s.EndTime == slotToBook.EndTime && !s.IsBooked)
                    .ToList();
                foreach (var slot in allSlotsThisTime)
                {
                    slotService.BookSlot(slot.Id);
                }
                var lessonBooked = lessonService.BookLesson(state.StudentId, slotToBook.TeacherId, slotToBook.Subject, slotToBook.StartTime, slotToBook.EndTime);
                await botClient.SendMessage(chatId, $"Вы успешно записаны на занятие по предмету '{slotToBook.Subject}' {slotToBook.StartTime:yyyy-MM-dd HH:mm}!");
                state.Reset();
                break;
            default:
                if (message.Text == "/mylessons")
                {
                    if (!VerifiedUsers.ContainsKey(chatId))
                    {
                        await botClient.SendMessage(chatId, "Сначала подтвердите номер телефона через WhatsApp. Введите /start.");
                        return;
                    }
                    var studentId = int.Parse(VerifiedUsers[chatId]);
                    var lessons = lessonService.GetLessonsByStudent(studentId);
                    if (lessons.Count == 0)
                    {
                        await botClient.SendMessage(chatId, "У вас нет записей на занятия.");
                        return;
                    }
                    var lessonsText = string.Join("\n", lessons.Select(l => $"{l.Id}: {l.Subject} {l.StartTime:yyyy-MM-dd HH:mm}"));
                    await botClient.SendMessage(chatId, $"Ваши записи:\n{lessonsText}\nЧтобы отменить запись, отправьте: отмена <id>");
                    return;
                }
                if (message.Text != null && message.Text.StartsWith("отмена "))
                {
                    if (!VerifiedUsers.ContainsKey(chatId))
                    {
                        await botClient.SendMessage(chatId, "Сначала подтвердите номер телефона через WhatsApp. Введите /start.");
                        return;
                    }
                    var studentId = int.Parse(VerifiedUsers[chatId]);
                    var parts = message.Text.Split(' ');
                    if (parts.Length != 2 || !int.TryParse(parts[1], out var lessonId))
                    {
                        await botClient.SendMessage(chatId, "Неверный формат. Используйте: отмена <id>");
                        return;
                    }
                    var lessons = lessonService.GetLessonsByStudent(studentId);
                    var lesson = lessons.FirstOrDefault(l => l.Id == lessonId);
                    if (lesson == null)
                    {
                        await botClient.SendMessage(chatId, "Запись не найдена.");
                        return;
                    }
                    var slot = slotService.GetSlotById(lesson.Id);
                    if (slot != null)
                    {
                        slot.IsBooked = false;
                        scope.ServiceProvider.GetRequiredService<TutorDbContext>().SaveChanges();
                    }
                    scope.ServiceProvider.GetRequiredService<TutorDbContext>().Lessons.Remove(lesson);
                    scope.ServiceProvider.GetRequiredService<TutorDbContext>().SaveChanges();
                    await botClient.SendMessage(chatId, "Запись отменена.");
                    return;
                }
                await botClient.SendMessage(chatId, "Для начала записи введите /start. Для просмотра записей — /mylessons");
                break;
        }
    }

    private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    private class UserState
    {
        public int Step { get; set; } = 0;
        public string? FullName { get; set; }
        public string? Phone { get; set; }
        public int StudentId { get; set; }
        public List<string> Subjects { get; set; } = new();
        public string? Subject { get; set; }
        public DateTime? StartTime { get; set; }
        public List<int> SlotIds { get; set; } = new();
        public Dictionary<string, int>? SlotMap { get; set; }
        public int SelectedSlotId { get; set; }
        public void Reset()
        {
            Step = 0;
            FullName = null;
            Phone = null;
            StudentId = 0;
            Subjects = new();
            Subject = null;
            StartTime = null;
            SlotIds = new();
            SlotMap = null;
            SelectedSlotId = 0;
        }
    }
}