using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Status.Api.Data;
using Status.Api.Models;
using WebPush;
using PushSub = WebPush.PushSubscription;

namespace Status.Api.Services;

public class VapidFile
{
    public string PublicKey { get; set; } = "";
    public string PrivateKey { get; set; } = "";
}

public class SignalWorker(IServiceProvider services, IWebHostEnvironment env, ILogger<SignalWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Tick();
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "The cue signal could not be sent.");
            }
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    async Task Tick()
    {
        var keys = LoadKeys();
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var subs = await db.PushSubscriptions.ToListAsync();
        var client = new WebPushClient();
        var vapid = new VapidDetails("mailto:developer@binding.local", keys.PublicKey, keys.PrivateKey);
        foreach (var sub in subs)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == sub.UserId);
            var profile = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == sub.UserId);
            var keystone = await db.Quests.FirstOrDefaultAsync(q => q.UserId == sub.UserId && q.IsKeystone && q.Status == QuestStatus.Active);
            if (user is null || profile is null || keystone?.CueTime is null) continue;
            if (!TimeOnly.TryParse(keystone.CueTime, out var cue)) continue;
            var local = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Timepiece.Zone(profile.TimeZone));
            if (local.Hour != cue.Hour || local.Minute != cue.Minute) continue;
            var today = DateOnly.FromDateTime(local.DateTime);
            if (user.LastSignalDate == today) continue;
            var fed = await db.HabitMarks.AnyAsync(m => m.QuestId == keystone.Id && m.LocalDate == today);
            if (fed) continue;
            var payload = JsonSerializer.Serialize(new { title = "The binding", body = Voice.Signal });
            var push = new PushSub(sub.Endpoint, sub.P256dh, sub.Auth);
            await client.SendNotificationAsync(push, payload, vapid);
            user.LastSignalDate = today;
            await db.SaveChangesAsync();
        }
    }

    VapidFile LoadKeys()
    {
        var path = Path.Combine(env.ContentRootPath, "vapid.json");
        if (File.Exists(path))
            return JsonSerializer.Deserialize<VapidFile>(File.ReadAllText(path)) ?? new VapidFile();
        var made = VapidHelper.GenerateVapidKeys();
        var file = new VapidFile { PublicKey = made.PublicKey, PrivateKey = made.PrivateKey };
        File.WriteAllText(path, JsonSerializer.Serialize(file));
        return file;
    }

    public string PublicKey() => LoadKeys().PublicKey;
}
