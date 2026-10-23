using Microsoft.EntityFrameworkCore;
using Status.Api.Models;

namespace Status.Api.Services;

public record HostRow(string Id, string DisplayName, string? Email, int Sustenance, DateTimeOffset CreatedAt);

public partial class BindingWorld
{
    public async Task<object> AdminSummary()
    {
        var hosts = await db.Users.AsNoTracking().ToListAsync();
        var fed = 0; var thin = 0; var faint = 0;
        foreach (var host in hosts)
        {
            var state = await BindingState(host);
            if (state == "Fed") fed++;
            else if (state == "Thin") thin++;
            else if (state == "Faint") faint++;
        }
        return new
        {
            hosts = hosts.Count,
            fed,
            thin,
            faint,
            calls = await db.Duels.CountAsync(d => d.Status != DuelStatus.Resolved),
            houses = await db.Guilds.CountAsync(),
            gatherings = await db.Parties.CountAsync()
        };
    }

    public async Task<List<HostRow>> AdminHosts(string? query)
    {
        var rows = db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            rows = rows.Where(u => u.DisplayName.Contains(q) || (u.Email != null && u.Email.Contains(q)));
        }
        return await rows.OrderBy(u => u.DisplayName)
            .Select(u => new HostRow(u.Id, u.DisplayName, u.Email, u.Fame, u.CreatedAt)).ToListAsync();
    }

    public async Task<object> AdminHost(string id)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id) ?? throw new BindingException(404, "Host not found.");
        var status = await BuildStatus(user);
        var profile = await ProfileOf(user);
        var notes = await db.Challenges.Where(c => c.Skill.UserId == id && c.Note != null).Select(c => new { c.Name, c.Note }).ToListAsync();
        var duelNotes = await db.Duels.Where(d => d.ChallengerId == id || d.OpponentId == id)
            .Select(d => new { d.Id, d.Terms, d.ChallengerNote, d.OpponentNote }).ToListAsync();
        var ledger = await FameOf(user);
        var marks = await db.HabitMarks.Where(m => m.UserId == id).OrderByDescending(m => m.Id).Take(30).ToListAsync();
        var log = await LogOf(user);
        var locked = user.LockoutEnd > DateTimeOffset.UtcNow;
        return new { status, profile.ConditionLabel, profile.ConditionNote, profile.TimeZone, notes, duelNotes, ledger, marks, log, locked };
    }

    public async Task SetLock(AppUser actor, string targetId, bool locked, string reason)
    {
        RequireReason(reason);
        var target = await db.Users.FirstAsync(u => u.Id == targetId);
        var before = (target.LockoutEnd > DateTimeOffset.UtcNow).ToString();
        target.LockoutEnabled = true;
        target.LockoutEnd = locked ? DateTimeOffset.UtcNow.AddYears(100) : null;
        Audit(actor, target, "lock", reason, before, locked.ToString());
        if (locked) Speak(target.Id, Voice.Outside);
        await db.SaveChangesAsync();
    }

    public async Task AdjustFame(AppUser actor, string targetId, int amount, string reason)
    {
        RequireReason(reason);
        var target = await db.Users.FirstAsync(u => u.Id == targetId);
        var before = target.Fame.ToString();
        await GrantFame(target, amount, "A hand outside the binding");
        Audit(actor, target, "sustenance", reason, before, target.Fame.ToString());
        Speak(target.Id, Voice.Outside);
        await db.SaveChangesAsync();
    }

    public async Task GrantName(AppUser actor, string targetId, string text, string reason)
    {
        RequireReason(reason);
        if (string.IsNullOrWhiteSpace(text)) throw new BindingException(400, "A name is required.");
        var target = await db.Users.FirstAsync(u => u.Id == targetId);
        GrantTitle(target.Id, text.Trim());
        Audit(actor, target, "title", reason, "", text.Trim());
        Speak(target.Id, Voice.Outside);
        await db.SaveChangesAsync();
    }

    public async Task GrantThing(AppUser actor, string targetId, string itemKey, string reason)
    {
        RequireReason(reason);
        var target = await db.Users.FirstAsync(u => u.Id == targetId);
        var (name, wearable, stack) = itemKey switch
        {
            "first-feeding" => ("First Feeding", true, false),
            "pulse" => ("A Pulse", false, true),
            "shared-tether" => ("A Shared Tether", true, false),
            "house-sigil" => ("A House Sigil", true, false),
            "answered-call" => ("An Answered Call", false, true),
            _ => throw new BindingException(400, "Unknown grown thing.")
        };
        GrantItem(target.Id, itemKey, name, "Given from outside.", wearable, stack);
        Audit(actor, target, "item", reason, "", itemKey);
        Speak(target.Id, Voice.Outside);
        await db.SaveChangesAsync();
    }

    public async Task<List<Party>> AdminParties() =>
        await db.Parties.Include(p => p.Members).OrderByDescending(p => p.Id).ToListAsync();

    public async Task<List<Guild>> AdminGuilds() =>
        await db.Guilds.Include(g => g.Members).OrderBy(g => g.Name).ToListAsync();

    public async Task<List<Duel>> AdminCalls() =>
        await db.Duels.OrderByDescending(d => d.Id).Take(100).ToListAsync();

    public async Task<List<AdminAction>> AuditLog() =>
        await db.AdminActions.OrderByDescending(a => a.Id).Take(100).ToListAsync();

    async Task<string> BindingState(AppUser user)
    {
        var profile = await db.Profiles.AsNoTracking().FirstAsync(p => p.UserId == user.Id);
        var today = Timepiece.Today(profile.TimeZone);
        var marks = await db.HabitMarks.Where(m => m.UserId == user.Id).Select(m => m.LocalDate).ToListAsync();
        var keystoneId = await db.Quests.Where(q => q.UserId == user.Id && q.IsKeystone && q.Status == QuestStatus.Active).Select(q => (int?)q.Id).FirstOrDefaultAsync();
        var keyFed = keystoneId is not null && marks.Contains(today) && await db.HabitMarks.AnyAsync(m => m.QuestId == keystoneId && m.LocalDate == today);
        if (keyFed || (keystoneId is null && marks.Contains(today))) return "Fed";
        if (marks.Count > 0 && !marks.Contains(today.AddDays(-1)) && !marks.Contains(today.AddDays(-2))) return "Faint";
        if (marks.Count > 0 && !marks.Contains(today.AddDays(-1))) return "Thin";
        return "Waiting";
    }

    static void RequireReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new BindingException(400, "A reason is required.");
    }

    void Audit(AppUser actor, AppUser target, string kind, string reason, string before, string after) =>
        db.AdminActions.Add(new AdminAction
        {
            DeveloperId = actor.Id,
            TargetUserId = target.Id,
            Kind = kind,
            Reason = reason.Trim(),
            Before = before,
            After = after
        });
}
