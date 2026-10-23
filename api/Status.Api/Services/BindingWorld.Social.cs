using Microsoft.EntityFrameworkCore;
using Status.Api.Models;

namespace Status.Api.Services;

public partial class BindingWorld
{
    async Task ClaimInvite(AppUser user, string code)
    {
        var invite = await db.Invites.FirstOrDefaultAsync(i => i.Code == code)
            ?? throw new BindingException(400, "That call does not exist.");
        if (invite.UsedByUserId is not null)
            throw new BindingException(400, "That call was already answered.");
        invite.UsedByUserId = user.Id;
        user.InvitedByUserId = invite.InviterId;
        Speak(user.Id, "You were called.");
    }

    async Task<PendingSeatView?> PendingSeat(AppUser user)
    {
        if (!user.TutorialComplete) return null;
        var invite = await db.Invites.FirstOrDefaultAsync(i => i.UsedByUserId == user.Id && !i.SeatAccepted && !i.Rewarded);
        if (invite is null) return null;
        var from = await db.Users.AsNoTracking().Where(u => u.Id == invite.InviterId).Select(u => u.DisplayName).FirstAsync();
        var can = invite.Kind == InviteKind.Party
            ? await db.PartyMembers.CountAsync(m => m.PartyId == invite.PartyId) < 4 && !await db.PartyMembers.AnyAsync(m => m.UserId == user.Id)
            : await db.GuildMembers.CountAsync(m => m.GuildId == invite.GuildId) < 30 && !await db.GuildMembers.AnyAsync(m => m.UserId == user.Id);
        return new PendingSeatView(invite.Code, invite.Kind.ToString(), from, can);
    }

    public async Task<Party> OpenParty(AppUser user, string? name)
    {
        if (await db.PartyMembers.AnyAsync(m => m.UserId == user.Id))
            throw new BindingException(400, "You already sit in a gathering.");
        var party = new Party { Name = string.IsNullOrWhiteSpace(name) ? "A gathering" : name.Trim(), LeaderId = user.Id };
        party.Members.Add(new PartyMember { UserId = user.Id });
        db.Parties.Add(party);
        Speak(user.Id, "A gathering was opened. The seats are empty.");
        await db.SaveChangesAsync();
        return party;
    }

    public async Task<string> MintPartyInvite(AppUser user)
    {
        var member = await db.PartyMembers.Include(m => m.Party).FirstOrDefaultAsync(m => m.UserId == user.Id)
            ?? throw new BindingException(400, "Open a gathering before you call.");
        var code = NewCode();
        db.Invites.Add(new Invite { Code = code, InviterId = user.Id, Kind = InviteKind.Party, PartyId = member.PartyId });
        Speak(user.Id, "Call another host. A binding among hosts lasts longer.");
        await db.SaveChangesAsync();
        return code;
    }

    public async Task<Guild> OpenGuild(AppUser user, string name, string creed)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(creed))
            throw new BindingException(400, "A house needs a name and a creed.");
        if (await db.GuildMembers.AnyAsync(m => m.UserId == user.Id))
            throw new BindingException(400, "You already sit in a house.");
        var guild = new Guild { Name = name.Trim(), Creed = creed.Trim(), FounderId = user.Id, KeeperId = user.Id };
        guild.Members.Add(new GuildMember { UserId = user.Id });
        db.Guilds.Add(guild);
        Speak(user.Id, Voice.HouseWider);
        await db.SaveChangesAsync();
        return guild;
    }

    public async Task<string> MintGuildInvite(AppUser user, int guildId)
    {
        var member = await db.GuildMembers.FirstOrDefaultAsync(m => m.GuildId == guildId && m.UserId == user.Id)
            ?? throw new BindingException(403, "You are not in that house.");
        var code = NewCode();
        db.Invites.Add(new Invite { Code = code, InviterId = user.Id, Kind = InviteKind.Guild, GuildId = guildId });
        Speak(user.Id, "A seat in the house is open.");
        await db.SaveChangesAsync();
        return code;
    }

    public async Task AcceptInvite(AppUser user, string code)
    {
        var invite = await db.Invites.FirstOrDefaultAsync(i => i.Code == code)
            ?? throw new BindingException(404, "That call does not exist.");
        if (invite.UsedByUserId is null)
        {
            invite.UsedByUserId = user.Id;
            user.InvitedByUserId ??= invite.InviterId;
        }
        else if (invite.UsedByUserId != user.Id)
            throw new BindingException(400, "That call was already answered.");
        if (!user.TutorialComplete)
            throw new BindingException(400, "The fastening is unfinished.");
        if (invite.Rewarded || invite.SeatAccepted) return;

        if (invite.Kind == InviteKind.Party)
        {
            if (await db.PartyMembers.AnyAsync(m => m.UserId == user.Id))
                throw new BindingException(400, "You already sit in a gathering.");
            var count = await db.PartyMembers.CountAsync(m => m.PartyId == invite.PartyId);
            if (count >= 4)
            {
                Speak(user.Id, Voice.SeatsFull);
                Speak(invite.InviterId, Voice.SeatsFull);
                invite.Rewarded = true;
                await db.SaveChangesAsync();
                return;
            }
            db.PartyMembers.Add(new PartyMember { PartyId = invite.PartyId!.Value, UserId = user.Id });
            invite.SeatAccepted = true;
            invite.Rewarded = true;
            await GrantFame(user, 10, "A seat was taken");
            await GrantFameId(invite.InviterId, 10, "A host was brought");
            GrantTitle(invite.InviterId, "The One Who Gathered");
            Speak(invite.InviterId, Voice.Brought);
            Speak(user.Id, Voice.Called);
        }
        else
        {
            if (await db.GuildMembers.AnyAsync(m => m.UserId == user.Id))
                throw new BindingException(400, "Leave your house before you enter another.");
            var count = await db.GuildMembers.CountAsync(m => m.GuildId == invite.GuildId);
            if (count >= 30)
            {
                Speak(user.Id, "The house is full. You are bound, and you can raise your own.");
                invite.Rewarded = true;
                await db.SaveChangesAsync();
                return;
            }
            db.GuildMembers.Add(new GuildMember { GuildId = invite.GuildId!.Value, UserId = user.Id });
            invite.SeatAccepted = true;
            invite.Rewarded = true;
            var guild = await db.Guilds.FirstAsync(g => g.Id == invite.GuildId);
            if (count + 1 == 2)
                GrantTitle(guild.FounderId, "The One Who Raised a House");
            Speak(invite.InviterId, Voice.Brought);
            Speak(user.Id, "You entered the house. Call another.");
        }
        await db.SaveChangesAsync();
    }

    public async Task<Party?> MyParty(AppUser user)
    {
        var member = await db.PartyMembers.Include(m => m.Party).ThenInclude(p => p.Members)
            .FirstOrDefaultAsync(m => m.UserId == user.Id);
        return member?.Party;
    }

    public async Task<List<SeatWitness>> PartyWitness(AppUser user)
    {
        var party = await MyParty(user);
        if (party is null) return [];
        var list = new List<SeatWitness>();
        foreach (var member in party.Members)
        {
            var host = await db.Users.AsNoTracking().FirstAsync(u => u.Id == member.UserId);
            var profile = await db.Profiles.AsNoTracking().FirstAsync(p => p.UserId == member.UserId);
            var today = Timepiece.Today(profile.TimeZone);
            var fed = await db.HabitMarks.AnyAsync(m => m.UserId == member.UserId && m.LocalDate == today && m.Quest.IsKeystone);
            list.Add(new SeatWitness(host.Id, host.DisplayName, fed, member.UserId == party.LeaderId));
        }
        return list;
    }

    public async Task<Quest?> PartyQuest(AppUser user)
    {
        var party = await MyParty(user);
        if (party is null) return null;
        return await db.Quests.Include(q => q.Milestones)
            .Where(q => q.PartyId == party.Id && q.Kind == QuestKind.Party)
            .OrderByDescending(q => q.Id).FirstOrDefaultAsync();
    }

    public async Task CreateSharedQuest(AppUser user, bool guild, int? guildId, string name, List<string> steps)
    {
        var clean = steps.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList();
        if (string.IsNullOrWhiteSpace(name) || clean.Count == 0)
            throw new BindingException(400, "A quest needs a name and a step.");
        int? partyId = null;
        if (!guild)
        {
            var party = await MyParty(user) ?? throw new BindingException(400, "You have no gathering.");
            if (party.LeaderId != user.Id) throw new BindingException(403, "The gathering's leader opens the quest.");
            var open = await db.Quests.AnyAsync(q => q.PartyId == party.Id && q.Status == QuestStatus.Active);
            if (open) throw new BindingException(400, "A gathered quest is already open.");
            partyId = party.Id;
        }
        else
        {
            var member = await db.GuildMembers.Include(m => m.Guild).FirstOrDefaultAsync(m => m.UserId == user.Id && m.GuildId == guildId)
                ?? throw new BindingException(403, "You are not in that house.");
            if (member.Guild.KeeperId != user.Id) throw new BindingException(403, "The keeper opens the house quest.");
            var open = await db.Quests.AnyAsync(q => q.GuildId == guildId && q.Status == QuestStatus.Active);
            if (open) throw new BindingException(400, "A house quest is already open.");
        }
        var quest = new Quest
        {
            UserId = user.Id,
            Kind = guild ? QuestKind.Guild : QuestKind.Party,
            Name = name.Trim(),
            PartyId = partyId,
            GuildId = guild ? guildId : null
        };
        quest.Milestones = clean.Select((step, i) => new Milestone { Name = step, Order = i }).ToList();
        db.Quests.Add(quest);
        Speak(user.Id, guild ? $"A house quest was opened. {quest.Name}." : $"A gathered quest was opened. {quest.Name}.");
        await db.SaveChangesAsync();
    }

    public async Task LeaveParty(AppUser user)
    {
        var member = await db.PartyMembers.Include(m => m.Party).ThenInclude(p => p.Members).FirstOrDefaultAsync(m => m.UserId == user.Id)
            ?? throw new BindingException(400, "You have no gathering.");
        var party = member.Party;
        db.PartyMembers.Remove(member);
        var rest = party.Members.Where(m => m.UserId != user.Id).OrderBy(m => m.JoinedAt).ToList();
        if (rest.Count == 0)
            db.Parties.Remove(party);
        else if (party.LeaderId == user.Id)
            party.LeaderId = rest[0].UserId;
        Speak(user.Id, "You left the gathering.");
        await db.SaveChangesAsync();
    }

    public async Task LeaveGuild(AppUser user)
    {
        var member = await db.GuildMembers.Include(m => m.Guild).ThenInclude(g => g.Members).FirstOrDefaultAsync(m => m.UserId == user.Id)
            ?? throw new BindingException(400, "You have no house.");
        var guild = member.Guild;
        db.GuildMembers.Remove(member);
        var rest = guild.Members.Where(m => m.UserId != user.Id).OrderBy(m => m.JoinedAt).ToList();
        if (rest.Count == 0)
            db.Guilds.Remove(guild);
        else if (guild.KeeperId == user.Id)
            guild.KeeperId = rest[0].UserId;
        Speak(user.Id, "You left the house. What you earned stays.");
        await db.SaveChangesAsync();
    }

    public async Task PassKeeper(AppUser user, string targetId)
    {
        var guild = await db.Guilds.FirstOrDefaultAsync(g => g.KeeperId == user.Id)
            ?? throw new BindingException(403, "You do not keep a house.");
        if (!await db.GuildMembers.AnyAsync(m => m.GuildId == guild.Id && m.UserId == targetId))
            throw new BindingException(400, "That host is not in the house.");
        guild.KeeperId = targetId;
        Speak(user.Id, "The keeper's mark was passed.");
        await db.SaveChangesAsync();
    }

    public async Task RemoveMember(AppUser user, string targetId)
    {
        var guild = await db.Guilds.Include(g => g.Members).FirstOrDefaultAsync(g => g.KeeperId == user.Id)
            ?? throw new BindingException(403, "You do not keep a house.");
        var member = guild.Members.FirstOrDefault(m => m.UserId == targetId)
            ?? throw new BindingException(404, "That host is not in the house.");
        if (targetId == user.Id) throw new BindingException(400, "Pass the mark before you leave.");
        db.GuildMembers.Remove(member);
        Speak(targetId, "You were set outside the house. What you earned stays.");
        await db.SaveChangesAsync();
    }

    public async Task<Guild?> MyGuild(AppUser user) =>
        await db.Guilds.Include(g => g.Members).FirstOrDefaultAsync(g => g.Members.Any(m => m.UserId == user.Id));

    public async Task<List<SeatWitness>> GuildMarks(AppUser user)
    {
        var guild = await MyGuild(user);
        if (guild is null) return [];
        var list = new List<SeatWitness>();
        foreach (var member in guild.Members)
        {
            var host = await db.Users.AsNoTracking().FirstAsync(u => u.Id == member.UserId);
            var profile = await db.Profiles.AsNoTracking().FirstAsync(p => p.UserId == member.UserId);
            var today = Timepiece.Today(profile.TimeZone);
            var fed = await db.HabitMarks.AnyAsync(m => m.UserId == member.UserId && m.LocalDate == today && m.Quest.IsKeystone);
            list.Add(new SeatWitness(host.Id, host.DisplayName, fed, member.UserId == guild.KeeperId));
        }
        return list;
    }

    public async Task<Quest?> GuildQuest(int guildId) =>
        await db.Quests.Include(q => q.Milestones).Where(q => q.GuildId == guildId && q.Kind == QuestKind.Guild)
            .OrderByDescending(q => q.Id).FirstOrDefaultAsync();

    public async Task<List<HouseRow>> Houses()
    {
        var (start, end) = Timepiece.UtcWeek();
        var guilds = await db.Guilds.Include(g => g.Members).OrderBy(g => g.Name).ToListAsync();
        var rows = new List<HouseRow>();
        foreach (var guild in guilds)
        {
            var ids = guild.Members.Select(m => m.UserId).ToList();
            var feeds = await db.HabitMarks.CountAsync(m => ids.Contains(m.UserId) && m.Quest.IsKeystone && m.LocalDate >= start && m.LocalDate < end);
            rows.Add(new HouseRow(guild.Id, guild.Name, guild.Creed, guild.Members.Count, feeds));
        }
        return rows.OrderByDescending(r => r.WeeklyFeedings).ThenBy(r => r.Name).ToList();
    }

    public async Task<PublicCard> PublicCard(string id)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id)
            ?? throw new BindingException(404, "That binding is not here.");
        var profile = await db.Profiles.AsNoTracking().FirstAsync(p => p.UserId == id);
        var zone = Timepiece.Zone(profile.TimeZone);
        var created = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(user.CreatedAt, zone).DateTime);
        var day = Timepiece.Today(profile.TimeZone).DayNumber - created.DayNumber + 1;
        var title = await db.Titles.Where(t => t.UserId == id && t.IsActive).Select(t => t.Text).FirstOrDefaultAsync();
        var skills = await db.Skills.Where(s => s.UserId == id).OrderBy(s => s.Name).ToListAsync();
        var names = await db.Achievements.Where(a => a.UserId == id && a.Earned).OrderBy(a => a.Name).Select(a => a.Name).ToListAsync();
        var worn = await db.Inventory.Where(i => i.UserId == id && i.Equipped).Select(i => i.Name).FirstOrDefaultAsync();
        var party = await db.PartyMembers.Include(m => m.Party).Where(m => m.UserId == id).Select(m => m.Party.Name).FirstOrDefaultAsync();
        var house = await db.GuildMembers.Include(m => m.Guild).Where(m => m.UserId == id).Select(m => m.Guild.Name).FirstOrDefaultAsync();
        return new PublicCard(user.Id, user.DisplayName, day, title, user.Fame,
            skills.Select(s => Catalog.Faculty(s.Rank, s.Name)).ToList(), names, worn, party, house);
    }

    public async Task<List<PublicCard>> Register()
    {
        var ids = await db.Users.AsNoTracking().OrderByDescending(u => u.Fame).ThenBy(u => u.DisplayName).Select(u => u.Id).ToListAsync();
        var cards = new List<PublicCard>();
        foreach (var id in ids) cards.Add(await PublicCard(id));
        return cards;
    }

    public async Task CreateDuel(AppUser user, string opponentId, string terms, DateOnly deadline)
    {
        if (opponentId == user.Id) throw new BindingException(400, "You cannot call yourself.");
        if (string.IsNullOrWhiteSpace(terms)) throw new BindingException(400, "A call needs terms.");
        if (!await db.Users.AnyAsync(u => u.Id == opponentId))
            throw new BindingException(404, "That binding is not here.");
        var open = await db.Duels.AnyAsync(d => d.Status != DuelStatus.Resolved &&
            ((d.ChallengerId == user.Id && d.OpponentId == opponentId) || (d.ChallengerId == opponentId && d.OpponentId == user.Id)));
        if (open) throw new BindingException(400, "A call between you is already open.");
        db.Duels.Add(new Duel { ChallengerId = user.Id, OpponentId = opponentId, Terms = terms.Trim(), Deadline = deadline });
        Speak(user.Id, Voice.CallLine);
        await db.SaveChangesAsync();
    }

    public async Task AcceptDuel(AppUser user, int id)
    {
        var duel = await db.Duels.FirstOrDefaultAsync(d => d.Id == id && d.OpponentId == user.Id && d.Status == DuelStatus.Pending)
            ?? throw new BindingException(404, "That call is not waiting for you.");
        duel.Status = DuelStatus.Active;
        await UnlockHidden(user.Id, "answered");
        Speak(user.Id, "You answered the call.");
        Speak(duel.ChallengerId, "Another binding answered.");
        await db.SaveChangesAsync();
    }

    public async Task DeclineDuel(AppUser user, int id)
    {
        var duel = await db.Duels.FirstOrDefaultAsync(d => d.Id == id && d.OpponentId == user.Id && d.Status == DuelStatus.Pending)
            ?? throw new BindingException(404, "That call is not waiting for you.");
        duel.Status = DuelStatus.Resolved;
        Speak(user.Id, "The call was refused.");
        await db.SaveChangesAsync();
    }

    public async Task ClearDuel(AppUser user, int id, string note)
    {
        if (string.IsNullOrWhiteSpace(note)) throw new BindingException(400, "The binding keeps a note with the clear.");
        var duel = await db.Duels.FirstOrDefaultAsync(d => d.Id == id && d.Status == DuelStatus.Active && (d.ChallengerId == user.Id || d.OpponentId == user.Id))
            ?? throw new BindingException(404, "That call is not active.");
        if (duel.ChallengerId == user.Id) { duel.ChallengerDone = true; duel.ChallengerNote = note.Trim(); }
        else { duel.OpponentDone = true; duel.OpponentNote = note.Trim(); }
        Speak(user.Id, Voice.NoteKept);
        await ResolveDuel(duel, force: false);
        await db.SaveChangesAsync();
    }

    public async Task<List<Duel>> MyDuels(AppUser user)
    {
        var rows = await db.Duels.Where(d => d.ChallengerId == user.Id || d.OpponentId == user.Id).OrderByDescending(d => d.Id).ToListAsync();
        foreach (var duel in rows.Where(d => d.Status == DuelStatus.Active))
            await ResolveDuel(duel, force: false);
        await db.SaveChangesAsync();
        return rows;
    }

    async Task ResolveDueDuels(AppUser user)
    {
        var rows = await db.Duels.Where(d => d.Status == DuelStatus.Active && (d.ChallengerId == user.Id || d.OpponentId == user.Id)).ToListAsync();
        foreach (var duel in rows) await ResolveDuel(duel, force: false);
    }

    async Task ResolveDuel(Duel duel, bool force)
    {
        if (duel.Status == DuelStatus.Resolved) return;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var due = today > duel.Deadline;
        if (duel.ChallengerDone && duel.OpponentDone)
        {
            await GrantFameId(duel.ChallengerId, 5, "A call was evenly met");
            await GrantFameId(duel.OpponentId, 5, "A call was evenly met");
            Speak(duel.ChallengerId, Voice.Even);
            Speak(duel.OpponentId, Voice.Even);
            duel.Status = DuelStatus.Resolved;
            return;
        }
        if (!due && !force) return;
        if (duel.ChallengerDone ^ duel.OpponentDone)
        {
            var winner = duel.ChallengerDone ? duel.ChallengerId : duel.OpponentId;
            duel.WinnerId = winner;
            await GrantFameId(winner, 10, "A call was answered");
            GrantItem(winner, "answered-call", "An Answered Call", "A call won alone.", false, true);
            GrantTitle(winner, "The One Who Called");
            Speak(winner, "The call was answered. You alone cleared it.");
        }
        duel.Status = DuelStatus.Resolved;
    }

    static string NewCode() => Guid.NewGuid().ToString("N")[..8];
}

public record SeatWitness(string UserId, string DisplayName, bool FedToday, bool Leader);
public record HouseRow(int Id, string Name, string Creed, int Members, int WeeklyFeedings);
public record PublicCard(string Id, string DisplayName, int DayOfBinding, string? Title, int Sustenance, List<string> Faculties, List<string> Names, string? Worn, string? Gathering, string? House);
