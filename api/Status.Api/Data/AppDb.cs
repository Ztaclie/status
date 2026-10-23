using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Status.Api.Models;

namespace Status.Api.Data;

public class AppDb(DbContextOptions<AppDb> options) : IdentityDbContext<AppUser>(options)
{
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<Title> Titles => Set<Title>();
    public DbSet<Arc> Arcs => Set<Arc>();
    public DbSet<Quest> Quests => Set<Quest>();
    public DbSet<HabitMark> HabitMarks => Set<HabitMark>();
    public DbSet<Milestone> Milestones => Set<Milestone>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<Challenge> Challenges => Set<Challenge>();
    public DbSet<Achievement> Achievements => Set<Achievement>();
    public DbSet<Daily> Dailies => Set<Daily>();
    public DbSet<InventoryItem> Inventory => Set<InventoryItem>();
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();
    public DbSet<Party> Parties => Set<Party>();
    public DbSet<PartyMember> PartyMembers => Set<PartyMember>();
    public DbSet<Invite> Invites => Set<Invite>();
    public DbSet<Guild> Guilds => Set<Guild>();
    public DbSet<GuildMember> GuildMembers => Set<GuildMember>();
    public DbSet<Duel> Duels => Set<Duel>();
    public DbSet<FameEvent> FameEvents => Set<FameEvent>();
    public DbSet<LogEntry> Logs => Set<LogEntry>();
    public DbSet<AdminAction> AdminActions => Set<AdminAction>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<Profile>().HasKey(p => p.UserId);
        builder.Entity<Profile>().HasOne(p => p.User).WithOne().HasForeignKey<Profile>(p => p.UserId);
        builder.Entity<HabitMark>().HasIndex(m => new { m.QuestId, m.LocalDate }).IsUnique();
        builder.Entity<Invite>().HasIndex(i => i.Code).IsUnique();
        builder.Entity<Achievement>().HasIndex(a => new { a.UserId, a.Key });
        builder.Entity<Daily>().HasIndex(d => new { d.UserId, d.LocalDate });
    }
}
