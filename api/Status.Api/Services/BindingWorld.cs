using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Status.Api.Data;
using Status.Api.Models;

namespace Status.Api.Services;

public partial class BindingWorld(AppDb db, UserManager<AppUser> users)
{
    public async Task<AppUser> Register(string email, string password, string displayName, string? inviteCode)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new BindingException(400, "The host needs a name.");
        var user = new AppUser
        {
            UserName = email.Trim(),
            Email = email.Trim(),
            DisplayName = displayName.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };
        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded)
            throw new BindingException(400, string.Join(" ", created.Errors.Select(e => e.Description)));

        db.Profiles.Add(new Profile { UserId = user.Id, TimeZone = "UTC" });
        var tutorial = new Quest
        {
            UserId = user.Id,
            Kind = QuestKind.Tutorial,
            Name = "This One Attaches",
            Detail = "The fastening.",
            Status = QuestStatus.Active
        };
        tutorial.Milestones =
        [
            new Milestone { Name = "Name a faculty", Order = 0 },
            new Milestone { Name = "Add a challenge", Order = 1 },
            new Milestone { Name = "Save a habit", Order = 2 }
        ];
        db.Quests.Add(tutorial);
        foreach (var hidden in Catalog.Hidden)
        {
            db.Achievements.Add(new Achievement
            {
                UserId = user.Id,
                Key = hidden.Key,
                Name = hidden.Name,
                Hint = hidden.Hint,
                Detail = hidden.Hint,
                IsHidden = true,
                IsSystem = true,
                FameReward = 10
            });
        }
        Speak(user.Id, Voice.FasteningBegun);

        if (!string.IsNullOrWhiteSpace(inviteCode))
            await ClaimInvite(user, inviteCode.Trim());

        await db.SaveChangesAsync();
        return user;
    }

    public async Task<StatusView> Status(AppUser user)
    {
        await EnsureDailies(user);
        await ResolveDueDuels(user);
        await PassiveUnlocks(user);
        await db.SaveChangesAsync();
        return await BuildStatus(user);
    }

    public async Task UpdateProfile(AppUser user, string? displayName, string? conditionLabel, string? conditionNote, string? timeZone, string? arcName, string? arcNote)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
            user.DisplayName = displayName.Trim();
        var profile = await ProfileOf(user);
        if (conditionLabel is not null) profile.ConditionLabel = conditionLabel.Trim();
        if (conditionNote is not null) profile.ConditionNote = conditionNote.Trim();
        if (!string.IsNullOrWhiteSpace(timeZone))
        {
            if (!TimeZoneInfo.TryFindSystemTimeZoneById(timeZone, out _))
                throw new BindingException(400, "That sky is not a known zone.");
            profile.TimeZone = timeZone;
        }
        if (!string.IsNullOrWhiteSpace(arcName))
        {
            var current = await db.Arcs.Where(a => a.UserId == user.Id && a.IsActive).ToListAsync();
            foreach (var arc in current)
            {
                arc.IsActive = false;
                arc.EndedAt = DateTimeOffset.UtcNow;
            }
            db.Arcs.Add(new Arc { UserId = user.Id, Name = arcName.Trim(), Note = arcNote?.Trim() ?? "", IsActive = true });
            Speak(user.Id, $"The arc is now {arcName.Trim()}.");
        }
        Speak(user.Id, "The host rewrote the reading.");
        await db.SaveChangesAsync();
    }

    public async Task WearTitle(AppUser user, int? titleId, string? newText)
    {
        if (!string.IsNullOrWhiteSpace(newText))
        {
            var made = new Title { UserId = user.Id, Text = newText.Trim(), IsSystem = false, IsActive = false };
            db.Titles.Add(made);
            await db.SaveChangesAsync();
            titleId = made.Id;
        }
        var title = await db.Titles.FirstOrDefaultAsync(t => t.Id == titleId && t.UserId == user.Id)
            ?? throw new BindingException(404, "That name is not yours.");
        var worn = await db.Titles.Where(t => t.UserId == user.Id && t.IsActive).ToListAsync();
        foreach (var old in worn)
        {
            old.IsActive = false;
            old.EndedAt = DateTimeOffset.UtcNow;
        }
        title.IsActive = true;
        title.EndedAt = null;
        Speak(user.Id, $"The name worn is {title.Text}.");
        await db.SaveChangesAsync();
    }

    async Task<Profile> ProfileOf(AppUser user) =>
        await db.Profiles.FirstAsync(p => p.UserId == user.Id);

    async Task<DateOnly> Today(AppUser user)
    {
        var profile = await db.Profiles.AsNoTracking().FirstAsync(p => p.UserId == user.Id);
        return Timepiece.Today(profile.TimeZone);
    }

    void Speak(string userId, string body) =>
        db.Logs.Add(new LogEntry { UserId = userId, Body = body });

    async Task GrantFame(AppUser user, int amount, string reason)
    {
        if (amount == 0) return;
        user.Fame = Math.Max(0, user.Fame + amount);
        db.FameEvents.Add(new FameEvent { UserId = user.Id, Amount = amount, Reason = reason });
        await Task.CompletedTask;
    }

    async Task GrantFameId(string userId, int amount, string reason)
    {
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        await GrantFame(user, amount, reason);
    }

    void GrantTitle(string userId, string text)
    {
        if (db.Titles.Any(t => t.UserId == userId && t.Text == text)) return;
        var any = db.Titles.Any(t => t.UserId == userId && t.IsActive);
        db.Titles.Add(new Title { UserId = userId, Text = text, IsSystem = true, IsActive = !any });
        Speak(userId, $"A name was given. {text}.");
    }

    void GrantItem(string userId, string key, string name, string description, bool wearable, bool stack)
    {
        var existing = db.Inventory.FirstOrDefault(i => i.UserId == userId && i.ItemKey == key);
        if (existing is not null)
        {
            if (stack) existing.Quantity += 1;
            return;
        }
        db.Inventory.Add(new InventoryItem
        {
            UserId = userId,
            ItemKey = key,
            Name = name,
            Description = description,
            Wearable = wearable,
            Quantity = 1
        });
        Speak(userId, $"Something grew. {name}.");
    }

    async Task UnlockHidden(string userId, string key)
    {
        var row = await db.Achievements.FirstOrDefaultAsync(a => a.UserId == userId && a.Key == key && a.IsHidden);
        if (row is null || row.Earned) return;
        row.Earned = true;
        row.EarnedAt = DateTimeOffset.UtcNow;
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        await GrantFame(user, row.FameReward, row.Name);
        GrantItem(userId, "hidden-" + key, row.Name, row.Hint, true, false);
        Speak(userId, $"A hidden name opened. {row.Name}.");
    }

    async Task PassiveUnlocks(AppUser user)
    {
        var today = await Today(user);
        var created = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(user.CreatedAt, Timepiece.Zone((await ProfileOf(user)).TimeZone)).DateTime);
        var day = today.DayNumber - created.DayNumber + 1;
        if (day >= 7) await UnlockHidden(user.Id, "day7");
        if (day >= 30) GrantTitle(user.Id, "The Binding Holds");
        var marks = await db.HabitMarks.CountAsync(m => m.UserId == user.Id);
        if (marks >= 7) await UnlockHidden(user.Id, "mornings");
        await UnlockHouseHunger(user);
    }

    async Task UnlockHouseHunger(AppUser user)
    {
        var member = await db.GuildMembers.AsNoTracking().FirstOrDefaultAsync(m => m.UserId == user.Id);
        if (member is null) return;
        var ids = await db.GuildMembers.Where(m => m.GuildId == member.GuildId).Select(m => m.UserId).ToListAsync();
        var dates = await db.HabitMarks.Where(m => ids.Contains(m.UserId) && m.Quest.IsKeystone)
            .Select(m => m.LocalDate).Distinct().CountAsync();
        if (dates >= 7)
        {
            foreach (var id in ids)
                await UnlockHidden(id, "house");
        }
    }

    async Task FinishTutorialIfReady(AppUser user)
    {
        if (user.TutorialComplete) return;
        var tutorial = await db.Quests.Include(q => q.Milestones)
            .FirstOrDefaultAsync(q => q.UserId == user.Id && q.Kind == QuestKind.Tutorial && q.Status == QuestStatus.Active);
        if (tutorial is null || tutorial.Milestones.Any(m => !m.Done)) return;
        tutorial.Status = QuestStatus.Completed;
        tutorial.CompletedAt = DateTimeOffset.UtcNow;
        user.TutorialComplete = true;
        GrantTitle(user.Id, "The Bound");
        GrantItem(user.Id, "first-feeding", "First Feeding", "The fastening.", true, false);
        await GrantFame(user, 10, "The fastening");
        Speak(user.Id, Voice.Attached);
        if (!string.IsNullOrEmpty(user.InvitedByUserId))
        {
            var parent = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == user.InvitedByUserId);
            if (parent?.InvitedByUserId is not null)
                await UnlockHidden(parent.InvitedByUserId, "further");
        }
    }

    async Task MarkTutorial(AppUser user, int order)
    {
        var tutorial = await db.Quests.Include(q => q.Milestones)
            .FirstOrDefaultAsync(q => q.UserId == user.Id && q.Kind == QuestKind.Tutorial && q.Status == QuestStatus.Active);
        var step = tutorial?.Milestones.FirstOrDefault(m => m.Order == order && !m.Done);
        if (step is null) return;
        step.Done = true;
        step.DoneAt = DateTimeOffset.UtcNow;
        step.DoneByUserId = user.Id;
        await FinishTutorialIfReady(user);
    }

    async Task PayDaily(AppUser user, Daily daily)
    {
        if (daily.Done) return;
        daily.Done = true;
        await GrantFame(user, 3, "A feeding");
        var today = daily.LocalDate;
        var all = await db.Dailies.Where(d => d.UserId == user.Id && d.LocalDate == today).ToListAsync();
        if (all.Count > 0 && all.All(d => d.Done || d.Id == daily.Id))
        {
            var pulseReason = "The three feedings " + today.ToString("yyyy-MM-dd");
            var already = await db.FameEvents.AnyAsync(f => f.UserId == user.Id && f.Reason == pulseReason);
            if (!already)
            {
                await GrantFame(user, 5, pulseReason);
                GrantItem(user.Id, "pulse", "A Pulse", "All three feedings in a day.", false, true);
            }
        }
    }

    async Task EnsureDailies(AppUser user)
    {
        var today = await Today(user);
        if (await db.Dailies.AnyAsync(d => d.UserId == user.Id && d.LocalDate == today))
        {
            await PromoteHabits(user, today);
            return;
        }
        var habits = await db.Quests.Where(q => q.UserId == user.Id && q.Kind == QuestKind.Habit && q.Status == QuestStatus.Active)
            .OrderByDescending(q => q.IsKeystone).ThenBy(q => q.Id).ToListAsync();
        var chosen = habits.Where(h => Timepiece.Scheduled(h.Weekdays, today)).Take(3).ToList();
        foreach (var habit in chosen)
        {
            db.Dailies.Add(new Daily
            {
                UserId = user.Id,
                LocalDate = today,
                Kind = DailyKind.Habit,
                Text = habit.IdentityLine ?? habit.Name,
                SourceQuestId = habit.Id
            });
        }
        var slots = 3 - chosen.Count;
        if (slots <= 0) return;
        var main = await db.Quests.Include(q => q.Milestones)
            .FirstOrDefaultAsync(q => q.UserId == user.Id && q.Kind == QuestKind.Milestone && q.IsMain && q.Status == QuestStatus.Active);
        var openStep = main?.Milestones.Any(m => !m.Done) == true;
        db.Dailies.Add(new Daily
        {
            UserId = user.Id,
            LocalDate = today,
            Kind = DailyKind.Milestone,
            Text = openStep ? "Advance the main quest." : "Create a quest step.",
            SourceQuestId = main?.Id
        });
        slots--;
        if (slots <= 0) return;
        var hasOpen = await db.Challenges.AnyAsync(c => !c.Passed && c.Skill.UserId == user.Id && c.Rank == c.Skill.Rank);
        db.Dailies.Add(new Daily
        {
            UserId = user.Id,
            LocalDate = today,
            Kind = DailyKind.Challenge,
            Text = hasOpen ? "Work an open faculty challenge." : "Add a challenge to a faculty."
        });
        slots--;
        if (slots <= 0) return;
        db.Dailies.Add(new Daily
        {
            UserId = user.Id,
            LocalDate = today,
            Kind = DailyKind.Line,
            Text = "Write the day's line."
        });
    }

    async Task PromoteHabits(AppUser user, DateOnly today)
    {
        var dailies = await db.Dailies.Where(d => d.UserId == user.Id && d.LocalDate == today).ToListAsync();
        var taken = dailies.Where(d => d.Kind == DailyKind.Habit && d.SourceQuestId is not null).Select(d => d.SourceQuestId!.Value).ToHashSet();
        var habits = await db.Quests.Where(q => q.UserId == user.Id && q.Kind == QuestKind.Habit && q.Status == QuestStatus.Active)
            .OrderByDescending(q => q.IsKeystone).ThenBy(q => q.Id).ToListAsync();
        var missing = habits.Where(h => Timepiece.Scheduled(h.Weekdays, today) && !taken.Contains(h.Id)).ToList();
        var slots = dailies.Where(d => !d.Done && d.Kind != DailyKind.Habit)
            .OrderBy(d => d.Kind == DailyKind.Line ? 0 : d.Kind == DailyKind.Challenge ? 1 : 2)
            .ToList();
        foreach (var habit in missing)
        {
            if (slots.Count == 0) break;
            var slot = slots[0];
            slots.RemoveAt(0);
            slot.Kind = DailyKind.Habit;
            slot.Text = habit.IdentityLine ?? habit.Name;
            slot.SourceQuestId = habit.Id;
        }
    }

    async Task CompleteKind(AppUser user, DailyKind kind)
    {
        var today = await Today(user);
        await EnsureDailies(user);
        await db.SaveChangesAsync();
        var daily = await db.Dailies.FirstOrDefaultAsync(d => d.UserId == user.Id && d.LocalDate == today && d.Kind == kind && !d.Done);
        if (daily is not null) await PayDaily(user, daily);
    }

    async Task<StatusView> BuildStatus(AppUser user)
    {
        var profile = await ProfileOf(user);
        var today = Timepiece.Today(profile.TimeZone);
        var zone = Timepiece.Zone(profile.TimeZone);
        var createdLocal = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(user.CreatedAt, zone).DateTime);
        var day = today.DayNumber - createdLocal.DayNumber + 1;
        var marks = await db.HabitMarks.Where(m => m.UserId == user.Id).Select(m => m.LocalDate).ToListAsync();
        var lifetime = marks.Count;
        var fedToday = marks.Contains(today);
        var yesterday = today.AddDays(-1);
        var before = today.AddDays(-2);
        var keystone = await db.Quests.FirstOrDefaultAsync(q => q.UserId == user.Id && q.Kind == QuestKind.Habit && q.IsKeystone && q.Status == QuestStatus.Active);
        var keyFed = keystone is not null && await db.HabitMarks.AnyAsync(m => m.QuestId == keystone.Id && m.LocalDate == today);
        string state;
        if (keyFed || (keystone is null && fedToday)) state = "Fed";
        else if (marks.Count > 0 && !marks.Contains(yesterday) && !marks.Contains(before)) state = "Faint";
        else if (marks.Count > 0 && !marks.Contains(yesterday)) state = "Thin";
        else state = "Waiting";

        var run = 0;
        for (var cursor = keyFed || fedToday ? today : yesterday; marks.Contains(cursor); cursor = cursor.AddDays(-1)) run++;
        var runLine = run > 0 ? $"Fed {run} days running." : "The run is quiet.";

        var skills = await db.Skills.Include(s => s.Challenges).Where(s => s.UserId == user.Id).OrderBy(s => s.Name).ToListAsync();
        var main = await db.Quests.Include(q => q.Milestones)
            .FirstOrDefaultAsync(q => q.UserId == user.Id && q.Kind == QuestKind.Milestone && q.IsMain && q.Status == QuestStatus.Active);
        var dailies = await db.Dailies.Where(d => d.UserId == user.Id && d.LocalDate == today).ToListAsync();
        var names = await db.Achievements.Where(a => a.UserId == user.Id && a.Earned).OrderByDescending(a => a.Id).Take(5).Select(a => a.Name).ToListAsync();
        var worn = await db.Inventory.FirstOrDefaultAsync(i => i.UserId == user.Id && i.Equipped);
        var lines = await db.Logs.Where(l => l.UserId == user.Id).OrderByDescending(l => l.Id).Take(3).Select(l => l.Body).ToListAsync();
        var title = await db.Titles.Where(t => t.UserId == user.Id && t.IsActive).Select(t => t.Text).FirstOrDefaultAsync();
        var arc = await db.Arcs.FirstOrDefaultAsync(a => a.UserId == user.Id && a.IsActive);
        var tutorial = await db.Quests.Include(q => q.Milestones)
            .FirstOrDefaultAsync(q => q.UserId == user.Id && q.Kind == QuestKind.Tutorial && q.Status == QuestStatus.Active);
        var pending = await PendingSeat(user);

        KeystoneView? keyView = null;
        if (keystone is not null)
        {
            var useTiny = state is "Thin" or "Faint";
            var keyDates = await db.HabitMarks.Where(m => m.QuestId == keystone.Id).Select(m => m.LocalDate).ToListAsync();
            var strip = Enumerable.Range(0, 7).Select(i => keyDates.Contains(today.AddDays(i - 6))).ToList();
            keyView = new KeystoneView(keystone.Id, keystone.IdentityLine ?? "", keystone.Cue ?? "", keystone.CueTime ?? "",
                keystone.TwoMinute ?? "", keystone.FullVersion ?? "", useTiny, strip, keyFed);
        }

        return new StatusView(
            state,
            user.DisplayName,
            title,
            profile.ConditionLabel,
            profile.ConditionNote,
            arc?.Name,
            arc?.Note,
            user.Fame,
            day,
            lifetime,
            run,
            runLine,
            keyView,
            user.TutorialComplete && !user.SignalOffered && keystone is not null,
            tutorial is not null,
            tutorial?.Milestones.OrderBy(m => m.Order).Select(m => new StepView(m.Id, m.Name, m.Done)).ToList() ?? [],
            skills.Select(s => new SkillView(s.Id, s.Name, s.Meaning, s.Rank.ToString(),
                Catalog.Faculty(s.Rank, s.Name),
                s.Challenges.Count(c => c.Required && c.Rank == s.Rank && !c.Passed))).ToList(),
            main is null ? null : new QuestView(main.Id, main.Name, main.Detail,
                main.Milestones.OrderBy(m => m.Order).Select(m => new StepView(m.Id, m.Name, m.Done)).ToList()),
            dailies.Select(d => new DailyView(d.Id, d.Text, d.Kind.ToString(), d.Done)).ToList(),
            names,
            worn?.Name,
            lines,
            keyFed ? Voice.Leave : null,
            pending,
            keyFed);
    }
}

public record StatusView(
    string BindingState, string DisplayName, string? Title, string? ConditionLabel, string? ConditionNote,
    string? ArcName, string? ArcNote, int Sustenance, int DayOfBinding, int LifetimeFeedings, int RunDays, string RunLine,
    KeystoneView? Keystone, bool AskForSignal, bool FasteningOpen, List<StepView> FasteningSteps,
    List<SkillView> Faculties, QuestView? MainQuest, List<DailyView> Dailies, List<string> RecentNames,
    string? WornThing, List<string> Lines, string? LeaveLine, PendingSeatView? PendingSeat, bool FedToday);

public record KeystoneView(int Id, string Identity, string Cue, string CueTime, string TwoMinute, string FullVersion, bool UseTwoMinute, List<bool> Marks, bool FedToday);
public record SkillView(int Id, string Name, string Meaning, string Rank, string Label, int ChallengesRemaining);
public record QuestView(int Id, string Name, string Detail, List<StepView> Steps);
public record StepView(int Id, string Name, bool Done);
public record DailyView(int Id, string Text, string Kind, bool Done);
public record PendingSeatView(string Code, string Kind, string FromName, bool CanSeat);
