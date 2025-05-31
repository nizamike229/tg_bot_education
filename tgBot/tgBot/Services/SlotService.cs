using tgBot.Context;
using tgBot.Entities;

namespace tgBot.Services;

public class SlotService
{
    private readonly TutorDbContext _context;
    public SlotService(TutorDbContext context)
    {
        _context = context;
    }

    public List<AvailableSlot> GetAvailableSlots(int teacherId, string? subject = null)
    {
        var query = _context.AvailableSlots.Where(s => s.TeacherId == teacherId && !s.IsBooked);
        if (!string.IsNullOrEmpty(subject))
            query = query.Where(s => s.Subject == subject);
        return query.OrderBy(s => s.StartTime).ToList();
    }

    public AvailableSlot? GetSlotById(int slotId)
    {
        return _context.AvailableSlots.FirstOrDefault(s => s.Id == slotId);
    }

    public void BookSlot(int slotId)
    {
        var slot = _context.AvailableSlots.FirstOrDefault(s => s.Id == slotId);
        if (slot != null)
        {
            slot.IsBooked = true;
            _context.SaveChanges();
        }
    }
}
