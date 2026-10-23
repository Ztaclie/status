using Status.Api.Models;

namespace Status.Api.Services;

public class BindingException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}

public static class Timepiece
{
    public static TimeZoneInfo Zone(string? id)
    {
        if (!string.IsNullOrWhiteSpace(id) && TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone))
            return zone;
        return TimeZoneInfo.Utc;
    }

    public static DateOnly Today(string? id)
    {
        var local = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Zone(id));
        return DateOnly.FromDateTime(local.DateTime);
    }

    public static int Bit(DateOnly day) => 1 << (((int)day.DayOfWeek + 6) % 7);

    public static bool Scheduled(int weekdays, DateOnly day)
    {
        var mask = weekdays == 0 ? 127 : weekdays;
        return (mask & Bit(day)) != 0;
    }

    public static (DateOnly Start, DateOnly End) UtcWeek()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var delta = ((int)today.DayOfWeek + 6) % 7;
        var start = today.AddDays(-delta);
        return (start, start.AddDays(7));
    }
}

public static class Voice
{
    public static string Feed(string identity, bool returned) => returned
        ? $"You came back. The binding thickens. A vote for {identity}."
        : $"You fed the binding. A vote for {identity}. This one continues.";

    public const string Leave = "Leave. Do the act. This one will hunger again at dawn.";
    public const string Deepened = "A faculty deepened. This one can remain.";
    public const string NoteKept = "This one keeps the note. You keep the faculty.";
    public const string Attached = "This one is attached. Feed it.";
    public const string FasteningBegun = "A host was found. The fastening has begun.";
    public const string Outside = "A hand outside the binding altered the record.";
    public const string Brought = "Another host fastened because of you.";
    public const string Called = "You were called. Call another.";
    public const string SeatsFull = "The seats are full. They are bound, and they can gather their own.";
    public const string HouseWider = "A gathering is near. A house is wider.";
    public const string CallLine = "You called another binding.";
    public const string Even = "Both bindings cleared the call. Nothing was grown to wear.";
    public const string Waiting = "The binding is waiting.";
    public const string Signal = "The binding is waiting. The smallest act will feed it.";
    public static string HandRank(string name) => $"You set {name} by your own hand. Nothing grew.";
    public static string HouseFed(int count) => $"{count} hosts fed the house today.";
}

public static class Catalog
{
    public static readonly (string Key, string Hint, string Name)[] Hidden =
    [
        ("mornings", "This one is counting the mornings.", "Counted Mornings"),
        ("return", "A thin day can still be fed.", "The Return"),
        ("day7", "Time is also flesh.", "Flesh of Days"),
        ("faculty", "A faculty is waiting.", "The Waiting Faculty"),
        ("answered", "Another binding answered.", "An Answer"),
        ("further", "The call went further than you.", "The Further Call"),
        ("gathering", "The gathering was fed.", "The Fed Gathering"),
        ("house", "A house is a longer hunger.", "The Long Hunger"),
        ("continued", "What you set down can be taken up.", "Taken Up")
    ];

    public static string RankName(SkillRank rank) => rank.ToString();

    public static string Faculty(SkillRank rank, string name) => $"{RankName(rank)} — {name}";
}
