using Microsoft.AspNetCore.Identity;

namespace Status.Api.Models;

public class AppUser : IdentityUser
{
    public string DisplayName { get; set; } = "";
    public int Fame { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateOnly? LastSignalDate { get; set; }
    public string? InvitedByUserId { get; set; }
    public bool TutorialComplete { get; set; }
    public bool SignalOffered { get; set; }
}

public class Profile
{
    public string UserId { get; set; } = "";
    public AppUser User { get; set; } = null!;
    public string ConditionLabel { get; set; } = "";
    public string ConditionNote { get; set; } = "";
    public string TimeZone { get; set; } = "UTC";
}

public class Title
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public string Text { get; set; } = "";
    public bool IsActive { get; set; }
    public bool IsSystem { get; set; }
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndedAt { get; set; }
}

public class Arc
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Note { get; set; } = "";
    public bool IsActive { get; set; }
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndedAt { get; set; }
}

public enum QuestKind { Habit, Milestone, Tutorial, Party, Guild }
public enum QuestStatus { Active, Completed, Dropped, Retired }
public enum SkillRank { Novice, Apprentice, Adept, Expert, Master }
public enum InviteKind { Party, Guild }
public enum DuelStatus { Pending, Active, Resolved }
public enum DailyKind { Habit, Milestone, Challenge, Line }

public class Quest
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public QuestKind Kind { get; set; }
    public string Name { get; set; } = "";
    public string Detail { get; set; } = "";
    public bool IsMain { get; set; }
    public QuestStatus Status { get; set; } = QuestStatus.Active;
    public string? IdentityLine { get; set; }
    public string? Cue { get; set; }
    public string? CueTime { get; set; }
    public string? TwoMinute { get; set; }
    public string? FullVersion { get; set; }
    public int Weekdays { get; set; } = 127;
    public bool IsKeystone { get; set; }
    public int? FeedsQuestId { get; set; }
    public int? PartyId { get; set; }
    public int? GuildId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public List<Milestone> Milestones { get; set; } = [];
    public List<HabitMark> Marks { get; set; } = [];
}

public class HabitMark
{
    public int Id { get; set; }
    public int QuestId { get; set; }
    public Quest Quest { get; set; } = null!;
    public string UserId { get; set; } = "";
    public DateOnly LocalDate { get; set; }
    public bool UsedTwoMinute { get; set; }
}

public class Milestone
{
    public int Id { get; set; }
    public int QuestId { get; set; }
    public Quest Quest { get; set; } = null!;
    public string Name { get; set; } = "";
    public int Order { get; set; }
    public bool Done { get; set; }
    public string? DoneByUserId { get; set; }
    public DateTimeOffset? DoneAt { get; set; }
}

public class Skill
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Meaning { get; set; } = "";
    public SkillRank Rank { get; set; } = SkillRank.Novice;
    public List<Challenge> Challenges { get; set; } = [];
}

public class Challenge
{
    public int Id { get; set; }
    public int SkillId { get; set; }
    public Skill Skill { get; set; } = null!;
    public SkillRank Rank { get; set; }
    public string Name { get; set; } = "";
    public string Detail { get; set; } = "";
    public bool Required { get; set; } = true;
    public bool Passed { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset? PassedAt { get; set; }
}

public class Achievement
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string Detail { get; set; } = "";
    public string Hint { get; set; } = "";
    public int FameReward { get; set; } = 10;
    public bool Earned { get; set; }
    public bool IsSystem { get; set; }
    public bool IsHidden { get; set; }
    public DateTimeOffset? EarnedAt { get; set; }
}

public class Daily
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public DateOnly LocalDate { get; set; }
    public DailyKind Kind { get; set; }
    public string Text { get; set; } = "";
    public int? SourceQuestId { get; set; }
    public string? Note { get; set; }
    public bool Done { get; set; }
}

public class InventoryItem
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public string ItemKey { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public bool Wearable { get; set; }
    public bool Equipped { get; set; }
    public DateTimeOffset AcquiredAt { get; set; } = DateTimeOffset.UtcNow;
}

public class PushSubscription
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public string P256dh { get; set; } = "";
    public string Auth { get; set; } = "";
}

public class Party
{
    public int Id { get; set; }
    public string Name { get; set; } = "A gathering";
    public string LeaderId { get; set; } = "";
    public bool TetherGranted { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<PartyMember> Members { get; set; } = [];
}

public class PartyMember
{
    public int Id { get; set; }
    public int PartyId { get; set; }
    public Party Party { get; set; } = null!;
    public string UserId { get; set; } = "";
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class Invite
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string InviterId { get; set; } = "";
    public InviteKind Kind { get; set; }
    public int? PartyId { get; set; }
    public int? GuildId { get; set; }
    public string? UsedByUserId { get; set; }
    public bool SeatAccepted { get; set; }
    public bool Rewarded { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class Guild
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Creed { get; set; } = "";
    public string FounderId { get; set; } = "";
    public string KeeperId { get; set; } = "";
    public bool SigilGranted { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<GuildMember> Members { get; set; } = [];
}

public class GuildMember
{
    public int Id { get; set; }
    public int GuildId { get; set; }
    public Guild Guild { get; set; } = null!;
    public string UserId { get; set; } = "";
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class Duel
{
    public int Id { get; set; }
    public string ChallengerId { get; set; } = "";
    public string OpponentId { get; set; } = "";
    public string Terms { get; set; } = "";
    public DateOnly Deadline { get; set; }
    public bool ChallengerDone { get; set; }
    public bool OpponentDone { get; set; }
    public string? ChallengerNote { get; set; }
    public string? OpponentNote { get; set; }
    public DuelStatus Status { get; set; } = DuelStatus.Pending;
    public string? WinnerId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class FameEvent
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public int Amount { get; set; }
    public string Reason { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class LogEntry
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class AdminAction
{
    public int Id { get; set; }
    public string DeveloperId { get; set; } = "";
    public string TargetUserId { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Reason { get; set; } = "";
    public string Before { get; set; } = "";
    public string After { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
