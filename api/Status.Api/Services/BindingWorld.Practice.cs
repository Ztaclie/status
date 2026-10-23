using Microsoft.EntityFrameworkCore;
using Status.Api.Models;

namespace Status.Api.Services;

public partial class BindingWorld
{
    public async Task CreateHabit(AppUser user, string identity, string cue, string cueTime, string twoMinute, string? fullVersion, int weekdays, bool keystone)
    {
        if (string.IsNullOrWhiteSpace(identity) || string.IsNullOrWhiteSpace(cue) || string.IsNullOrWhiteSpace(cueTime) || string.IsNullOrWhiteSpace(twoMinute))
            throw new BindingException(400, "This one needs an identity, a cue, a cue time, and a two-minute act.");
        if (!TimeOnly.TryParse(cueTime, out _))
            throw new BindingException(400, "The cue time is not a time.");
        if (keystone)
        {
            var others = await db.Quests.Where(q => q.UserId == user.Id && q.IsKeystone).ToListAsync();
            foreach (var other in others) other.IsKeystone = false;
        }
        var habit = new Quest
        {
            UserId = user.Id,
            Kind = QuestKind.Habit,
            Name = identity.Trim(),
            IdentityLine = identity.Trim(),
            Cue = cue.Trim(),
            CueTime = cueTime.Trim(),
            TwoMinute = twoMinute.Trim(),
            FullVersion = fullVersion?.Trim() ?? "",
            Weekdays = weekdays == 0 ? 127 : weekdays,
            IsKeystone = keystone || !await db.Quests.AnyAsync(q => q.UserId == user.Id && q.Kind == QuestKind.Habit && q.Status == QuestStatus.Active && q.IsKeystone)
        };
        db.Quests.Add(habit);
        Speak(user.Id, $"A habit was fastened. A vote for {habit.IdentityLine}.");
        await MarkTutorial(user, 2);
        await db.SaveChangesAsync();
    }

    public async Task ShowUp(AppUser user, int habitId)
    {
        var habit = await db.Quests.FirstOrDefaultAsync(q => q.Id == habitId && q.UserId == user.Id && q.Kind == QuestKind.Habit && q.Status == QuestStatus.Active)
            ?? throw new BindingException(404, "That habit is not fastened.");
        var today = await Today(user);
        if (await db.HabitMarks.AnyAsync(m => m.QuestId == habit.Id && m.LocalDate == today))
            return;
        var yesterday = today.AddDays(-1);
        var anyYesterday = await db.HabitMarks.AnyAsync(m => m.UserId == user.Id && m.LocalDate == yesterday);
        var returned = !anyYesterday && await db.HabitMarks.AnyAsync(m => m.UserId == user.Id && m.LocalDate < yesterday);
        if (!await db.HabitMarks.AnyAsync(m => m.UserId == user.Id)) returned = false;
        var useTiny = !anyYesterday;
        db.HabitMarks.Add(new HabitMark { QuestId = habit.Id, UserId = user.Id, LocalDate = today, UsedTwoMinute = useTiny });
        Speak(user.Id, Voice.Feed(habit.IdentityLine ?? habit.Name, returned));
        if (returned)
        {
            GrantTitle(user.Id, "The One Who Returned");
            await UnlockHidden(user.Id, "return");
        }
        await EnsureDailies(user);
        await db.SaveChangesAsync();
        var daily = await db.Dailies.FirstOrDefaultAsync(d => d.UserId == user.Id && d.LocalDate == today && d.Kind == DailyKind.Habit && d.SourceQuestId == habit.Id && !d.Done);
        if (daily is not null) await PayDaily(user, daily);
        var marks = await db.HabitMarks.CountAsync(m => m.UserId == user.Id);
        if (marks >= 7) await UnlockHidden(user.Id, "mornings");
        await db.SaveChangesAsync();
    }

    public async Task RetireHabit(AppUser user, int habitId)
    {
        var habit = await db.Quests.FirstOrDefaultAsync(q => q.Id == habitId && q.UserId == user.Id && q.Kind == QuestKind.Habit)
            ?? throw new BindingException(404, "That habit is not fastened.");
        habit.Status = QuestStatus.Retired;
        habit.IsKeystone = false;
        Speak(user.Id, $"The vote for {habit.IdentityLine} was set down.");
        await db.SaveChangesAsync();
    }

    public async Task CreateMilestoneQuest(AppUser user, string name, string? detail, int? habitId, bool isMain, List<string> steps)
    {
        var clean = steps.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList();
        if (string.IsNullOrWhiteSpace(name) || clean.Count == 0)
            throw new BindingException(400, "A quest needs a name and a step.");
        if (isMain)
        {
            var mains = await db.Quests.Where(q => q.UserId == user.Id && q.IsMain && q.Status == QuestStatus.Active).ToListAsync();
            foreach (var main in mains) main.IsMain = false;
        }
        var quest = new Quest
        {
            UserId = user.Id,
            Kind = QuestKind.Milestone,
            Name = name.Trim(),
            Detail = detail?.Trim() ?? "",
            IsMain = isMain || !await db.Quests.AnyAsync(q => q.UserId == user.Id && q.Kind == QuestKind.Milestone && q.IsMain && q.Status == QuestStatus.Active),
            FeedsQuestId = habitId
        };
        quest.Milestones = clean.Select((step, i) => new Milestone { Name = step, Order = i }).ToList();
        db.Quests.Add(quest);
        Speak(user.Id, $"A quest was opened. {quest.Name}.");
        await CompleteKind(user, DailyKind.Milestone);
        await db.SaveChangesAsync();
    }

    public async Task AddMilestone(AppUser user, int questId, string name)
    {
        var quest = await OwnActiveQuest(user, questId);
        if (quest.Kind is QuestKind.Habit or QuestKind.Tutorial)
            throw new BindingException(400, "That record does not take steps.");
        await EnsureCanStep(user, quest);
        var order = await db.Milestones.Where(m => m.QuestId == quest.Id).Select(m => (int?)m.Order).MaxAsync() ?? -1;
        db.Milestones.Add(new Milestone { QuestId = quest.Id, Name = name.Trim(), Order = order + 1 });
        if (quest.Kind == QuestKind.Milestone) await CompleteKind(user, DailyKind.Milestone);
        Speak(user.Id, $"A step was added to {quest.Name}.");
        await db.SaveChangesAsync();
    }

    public async Task CompleteMilestone(AppUser user, int milestoneId)
    {
        var step = await db.Milestones.Include(m => m.Quest).FirstOrDefaultAsync(m => m.Id == milestoneId)
            ?? throw new BindingException(404, "That step is not here.");
        if (step.Done) return;
        if (step.Quest.Kind == QuestKind.Tutorial)
            throw new BindingException(400, "The fastening completes itself.");
        await EnsureCanStep(user, step.Quest);
        step.Done = true;
        step.DoneAt = DateTimeOffset.UtcNow;
        step.DoneByUserId = user.Id;
        Speak(user.Id, $"A step was fed. {step.Name}.");
        if (step.Quest.Kind == QuestKind.Milestone && step.Quest.IsMain)
            await CompleteKind(user, DailyKind.Milestone);
        var steps = await db.Milestones.Where(m => m.QuestId == step.QuestId).ToListAsync();
        if (steps.All(s => s.Done || s.Id == step.Id))
            await FinishQuest(step.Quest);
        await db.SaveChangesAsync();
    }

    async Task FinishQuest(Quest quest)
    {
        if (quest.Status == QuestStatus.Completed) return;
        quest.Status = QuestStatus.Completed;
        quest.CompletedAt = DateTimeOffset.UtcNow;
        if (quest.Kind == QuestKind.Milestone)
        {
            var dropped = await db.Quests.AnyAsync(q => q.UserId == quest.UserId && q.Status == QuestStatus.Dropped);
            await GrantFameId(quest.UserId, 15, "A quest finished");
            Speak(quest.UserId, $"The quest {quest.Name} is finished.");
            if (dropped)
            {
                GrantTitle(quest.UserId, "The One Who Continued");
                await UnlockHidden(quest.UserId, "continued");
            }
        }
        else if (quest.Kind == QuestKind.Party && quest.PartyId is int partyId)
        {
            var party = await db.Parties.Include(p => p.Members).FirstAsync(p => p.Id == partyId);
            foreach (var member in party.Members)
            {
                await GrantFameId(member.UserId, 15, "A gathering was fed");
                Speak(member.UserId, $"The gathering fed {quest.Name}.");
                await UnlockHidden(member.UserId, "gathering");
                if (!party.TetherGranted)
                    GrantItem(member.UserId, "shared-tether", "A Shared Tether", "The first gathered quest.", true, false);
            }
            party.TetherGranted = true;
        }
        else if (quest.Kind == QuestKind.Guild && quest.GuildId is int guildId)
        {
            var guild = await db.Guilds.Include(g => g.Members).FirstAsync(g => g.Id == guildId);
            var steppers = await db.Milestones.Where(m => m.QuestId == quest.Id && m.DoneByUserId != null)
                .Select(m => m.DoneByUserId!).Distinct().ToListAsync();
            foreach (var id in steppers)
            {
                await GrantFameId(id, 15, "A house quest finished");
                Speak(id, $"The house fed {quest.Name}.");
            }
            if (!guild.SigilGranted)
            {
                foreach (var member in guild.Members)
                    GrantItem(member.UserId, "house-sigil", "A House Sigil", "The first house quest.", true, false);
                guild.SigilGranted = true;
            }
        }
    }

    public async Task DropQuest(AppUser user, int questId)
    {
        var quest = await OwnActiveQuest(user, questId);
        if (quest.Kind != QuestKind.Milestone)
            throw new BindingException(400, "Only a personal quest can be set down.");
        quest.Status = QuestStatus.Dropped;
        quest.IsMain = false;
        Speak(user.Id, $"The quest {quest.Name} was set down.");
        await db.SaveChangesAsync();
    }

    public async Task MakeMain(AppUser user, int questId)
    {
        var quest = await OwnActiveQuest(user, questId);
        if (quest.Kind != QuestKind.Milestone) throw new BindingException(400, "That is not a personal quest.");
        var mains = await db.Quests.Where(q => q.UserId == user.Id && q.IsMain).ToListAsync();
        foreach (var main in mains) main.IsMain = false;
        quest.IsMain = true;
        Speak(user.Id, $"{quest.Name} is the main quest.");
        await db.SaveChangesAsync();
    }

    async Task<Quest> OwnActiveQuest(AppUser user, int questId)
    {
        var quest = await db.Quests.FirstOrDefaultAsync(q => q.Id == questId && q.Status == QuestStatus.Active)
            ?? throw new BindingException(404, "That quest is not open.");
        if (quest.Kind == QuestKind.Milestone && quest.UserId != user.Id)
            throw new BindingException(404, "That quest is not open.");
        return quest;
    }

    async Task EnsureCanStep(AppUser user, Quest quest)
    {
        if (quest.Kind == QuestKind.Milestone && quest.UserId != user.Id)
            throw new BindingException(403, "That quest is not yours.");
        if (quest.Kind == QuestKind.Party && !await db.PartyMembers.AnyAsync(m => m.PartyId == quest.PartyId && m.UserId == user.Id))
            throw new BindingException(403, "You are not in that gathering.");
        if (quest.Kind == QuestKind.Guild && !await db.GuildMembers.AnyAsync(m => m.GuildId == quest.GuildId && m.UserId == user.Id))
            throw new BindingException(403, "You are not in that house.");
        if (quest.Status != QuestStatus.Active)
            throw new BindingException(400, "That quest is closed.");
    }

    public async Task CreateSkill(AppUser user, string name, string? meaning)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new BindingException(400, "A faculty needs a name.");
        db.Skills.Add(new Skill { UserId = user.Id, Name = name.Trim(), Meaning = meaning?.Trim() ?? "" });
        Speak(user.Id, $"A faculty was named. {name.Trim()}.");
        await MarkTutorial(user, 0);
        await db.SaveChangesAsync();
    }

    public async Task UpdateSkill(AppUser user, int skillId, string? name, string? meaning, SkillRank? rank)
    {
        var skill = await db.Skills.FirstOrDefaultAsync(s => s.Id == skillId && s.UserId == user.Id)
            ?? throw new BindingException(404, "That faculty is not yours.");
        if (!string.IsNullOrWhiteSpace(name)) skill.Name = name.Trim();
        if (meaning is not null) skill.Meaning = meaning.Trim();
        if (rank is not null && rank != skill.Rank)
        {
            skill.Rank = rank.Value;
            Speak(user.Id, Voice.HandRank(skill.Name));
        }
        await db.SaveChangesAsync();
    }

    public async Task AddChallenge(AppUser user, int skillId, string name, string? detail, bool required)
    {
        var skill = await db.Skills.FirstOrDefaultAsync(s => s.Id == skillId && s.UserId == user.Id)
            ?? throw new BindingException(404, "That faculty is not yours.");
        if (string.IsNullOrWhiteSpace(name)) throw new BindingException(400, "A challenge needs a name.");
        db.Challenges.Add(new Challenge
        {
            SkillId = skill.Id,
            Rank = skill.Rank,
            Name = name.Trim(),
            Detail = detail?.Trim() ?? "",
            Required = required
        });
        Speak(user.Id, $"A challenge was written for {Catalog.Faculty(skill.Rank, skill.Name)}.");
        await MarkTutorial(user, 1);
        await CompleteKind(user, DailyKind.Challenge);
        await db.SaveChangesAsync();
    }

    public async Task PassChallenge(AppUser user, int challengeId, string note)
    {
        if (string.IsNullOrWhiteSpace(note))
            throw new BindingException(400, "The binding keeps a note with the pass.");
        var challenge = await db.Challenges.Include(c => c.Skill).FirstOrDefaultAsync(c => c.Id == challengeId && c.Skill.UserId == user.Id)
            ?? throw new BindingException(404, "That challenge is not yours.");
        if (challenge.Passed) return;
        if (challenge.Rank != challenge.Skill.Rank)
            throw new BindingException(400, "That challenge belongs to a rank already left.");
        challenge.Passed = true;
        challenge.Note = note.Trim();
        challenge.PassedAt = DateTimeOffset.UtcNow;
        await GrantFame(user, 5, "A challenge passed");
        Speak(user.Id, Voice.NoteKept);
        await CompleteKind(user, DailyKind.Challenge);
        var required = await db.Challenges.Where(c => c.SkillId == challenge.SkillId && c.Rank == challenge.Skill.Rank && c.Required).ToListAsync();
        if (required.Count > 0 && required.All(c => c.Passed || c.Id == challenge.Id) && challenge.Skill.Rank < SkillRank.Master)
        {
            challenge.Skill.Rank += 1;
            await GrantFame(user, 25, "A faculty deepened");
            var rankName = challenge.Skill.Rank.ToString();
            GrantItem(user.Id, "faculty-seal-" + rankName.ToLowerInvariant(), $"A Deepened Faculty — {rankName}", "A rank risen.", true, false);
            Speak(user.Id, Voice.Deepened);
            if (challenge.Skill.Rank == SkillRank.Apprentice)
            {
                GrantTitle(user.Id, "The One Who Advanced");
                await UnlockHidden(user.Id, "faculty");
            }
        }
        await db.SaveChangesAsync();
    }

    public async Task CreateAchievement(AppUser user, string name, string? detail, int? reward)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new BindingException(400, "A goal needs a name.");
        db.Achievements.Add(new Achievement
        {
            UserId = user.Id,
            Key = "personal-" + Guid.NewGuid().ToString("N")[..8],
            Name = name.Trim(),
            Detail = detail?.Trim() ?? "",
            FameReward = reward is null or < 1 ? 10 : reward.Value,
            IsSystem = false,
            IsHidden = false
        });
        Speak(user.Id, $"A goal was named. {name.Trim()}.");
        await db.SaveChangesAsync();
    }

    public async Task ClaimAchievement(AppUser user, int id)
    {
        var row = await db.Achievements.FirstOrDefaultAsync(a => a.Id == id && a.UserId == user.Id && !a.IsHidden)
            ?? throw new BindingException(404, "That goal is not yours to claim.");
        if (row.Earned) return;
        row.Earned = true;
        row.EarnedAt = DateTimeOffset.UtcNow;
        await GrantFame(user, row.FameReward, row.Name);
        Speak(user.Id, $"The goal was claimed. {row.Name}.");
        await db.SaveChangesAsync();
    }

    public async Task Equip(AppUser user, int itemId)
    {
        var item = await db.Inventory.FirstOrDefaultAsync(i => i.Id == itemId && i.UserId == user.Id)
            ?? throw new BindingException(404, "That thing is not yours.");
        if (!item.Wearable) throw new BindingException(400, "That thing is kept, not worn.");
        var worn = await db.Inventory.Where(i => i.UserId == user.Id && i.Equipped).ToListAsync();
        foreach (var old in worn) old.Equipped = false;
        item.Equipped = true;
        Speak(user.Id, $"{item.Name} is worn.");
        await db.SaveChangesAsync();
    }

    public async Task WriteLine(AppUser user, int dailyId, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new BindingException(400, "The line is empty.");
        var today = await Today(user);
        var daily = await db.Dailies.FirstOrDefaultAsync(d => d.Id == dailyId && d.UserId == user.Id && d.LocalDate == today && d.Kind == DailyKind.Line)
            ?? throw new BindingException(404, "That line is not today's.");
        daily.Note = text.Trim();
        Speak(user.Id, "The day's line was given. This one keeps it.");
        await PayDaily(user, daily);
        await db.SaveChangesAsync();
    }

    public async Task<List<Quest>> Habits(AppUser user) =>
        await db.Quests.Where(q => q.UserId == user.Id && q.Kind == QuestKind.Habit).OrderByDescending(q => q.IsKeystone).ToListAsync();

    public async Task<List<Quest>> MilestoneQuests(AppUser user) =>
        await db.Quests.Include(q => q.Milestones).Where(q => q.UserId == user.Id && q.Kind == QuestKind.Milestone).OrderByDescending(q => q.Id).ToListAsync();

    public async Task<List<Skill>> SkillsOf(AppUser user) =>
        await db.Skills.Include(s => s.Challenges).Where(s => s.UserId == user.Id).OrderBy(s => s.Name).ToListAsync();

    public async Task<Skill> SkillOf(AppUser user, int id) =>
        await db.Skills.Include(s => s.Challenges).FirstOrDefaultAsync(s => s.Id == id && s.UserId == user.Id)
        ?? throw new BindingException(404, "That faculty is not yours.");

    public async Task<List<Achievement>> AchievementsOf(AppUser user) =>
        await db.Achievements.Where(a => a.UserId == user.Id).OrderBy(a => a.IsHidden).ThenBy(a => a.Name).ToListAsync();

    public async Task<List<InventoryItem>> ItemsOf(AppUser user) =>
        await db.Inventory.Where(i => i.UserId == user.Id).OrderByDescending(i => i.Id).ToListAsync();

    public async Task<List<Title>> TitlesOf(AppUser user) =>
        await db.Titles.Where(t => t.UserId == user.Id).OrderByDescending(t => t.IsActive).ThenByDescending(t => t.Id).ToListAsync();

    public async Task<List<FameEvent>> FameOf(AppUser user) =>
        await db.FameEvents.Where(f => f.UserId == user.Id).OrderByDescending(f => f.Id).Take(100).ToListAsync();

    public async Task<List<LogEntry>> LogOf(AppUser user) =>
        await db.Logs.Where(l => l.UserId == user.Id).OrderByDescending(l => l.Id).Take(200).ToListAsync();

    public async Task DeclineSignal(AppUser user)
    {
        user.SignalOffered = true;
        await db.SaveChangesAsync();
    }

    public async Task Subscribe(AppUser user, string endpoint, string p256dh, string auth)
    {
        var existing = await db.PushSubscriptions.FirstOrDefaultAsync(p => p.UserId == user.Id && p.Endpoint == endpoint);
        if (existing is null)
            db.PushSubscriptions.Add(new PushSubscription { UserId = user.Id, Endpoint = endpoint, P256dh = p256dh, Auth = auth });
        else
        {
            existing.P256dh = p256dh;
            existing.Auth = auth;
        }
        user.SignalOffered = true;
        Speak(user.Id, "This one may call you at the cue. Once.");
        await db.SaveChangesAsync();
    }
}
