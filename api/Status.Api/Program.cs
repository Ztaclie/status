using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Status.Api.Data;
using Status.Api.Models;
using Status.Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AppDb>(o => o.UseSqlite(builder.Configuration.GetConnectionString("Binding")));
builder.Services.AddIdentityCore<AppUser>(o =>
{
    o.Password.RequiredLength = 8;
    o.Password.RequireNonAlphanumeric = false;
    o.Password.RequireUppercase = false;
    o.User.RequireUniqueEmail = true;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<AppDb>()
.AddSignInManager();

var jwtKey = builder.Configuration["Jwt:Key"] ?? "a-long-dev-key-at-least-32-chars-binding-window";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "binding",
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "binding",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        NameClaimType = ClaimTypes.NameIdentifier,
        RoleClaimType = ClaimTypes.Role
    };
});
builder.Services.AddAuthorization();
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});
builder.Services.AddScoped<BindingWorld>();
builder.Services.AddSingleton<SignalWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<SignalWorker>());

var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    await db.Database.EnsureCreatedAsync();
    var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    if (!await roles.RoleExistsAsync("Developer"))
        await roles.CreateAsync(new IdentityRole("Developer"));
}

app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/auth/register", async (RegisterBody body, BindingWorld world, UserManager<AppUser> users, IConfiguration config) =>
{
    try
    {
        var user = await world.Register(body.Email, body.Password, body.DisplayName, body.InviteCode);
        await SyncDeveloper(user, users, config);
        return Results.Ok(await Token(user, users, config, jwtKey));
    }
    catch (BindingException ex) { return Fail(ex); }
});

app.MapPost("/auth/login", async (LoginBody body, UserManager<AppUser> users, IConfiguration config) =>
{
    var user = await users.FindByEmailAsync(body.Email);
    if (user is null) return Results.Json(new { error = "The binding does not know that host." }, statusCode: 401);
    if (await users.IsLockedOutAsync(user)) return Results.Json(new { error = "This host cannot enter." }, statusCode: 403);
    if (!await users.CheckPasswordAsync(user, body.Password))
        return Results.Json(new { error = "The binding does not know that host." }, statusCode: 401);
    await SyncDeveloper(user, users, config);
    return Results.Ok(await Token(user, users, config, jwtKey));
});

var authed = app.MapGroup("/").RequireAuthorization();

authed.MapGet("/auth/me", async (ClaimsPrincipal principal, UserManager<AppUser> users, SignalWorker signal) =>
{
    var user = await Current(principal, users);
    var dev = await users.IsInRoleAsync(user, "Developer");
    return Results.Ok(new
    {
        user.Id,
        user.DisplayName,
        user.Email,
        isDeveloper = dev,
        user.TutorialComplete,
        user.SignalOffered,
        vapidPublicKey = signal.PublicKey()
    });
});

authed.MapGet("/status", async (ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    await Run(async () => await world.Status(await Current(p, users))));

authed.MapPut("/profile", async (ProfileBody body, ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    await Run(async () => { await world.UpdateProfile(await Current(p, users), body.DisplayName, body.ConditionLabel, body.ConditionNote, body.TimeZone, body.ArcName, body.ArcNote); return new { ok = true }; }));

authed.MapGet("/titles", async (ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    Results.Ok(await world.TitlesOf(await Current(p, users))));

authed.MapPost("/titles", async (TitleBody body, ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    await Run(async () => { await world.WearTitle(await Current(p, users), body.TitleId, body.Text); return new { ok = true }; }));

authed.MapPost("/habits", async (HabitBody body, ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    await Run(async () => { await world.CreateHabit(await Current(p, users), body.Identity, body.Cue, body.CueTime, body.TwoMinute, body.FullVersion, body.Weekdays, body.Keystone); return new { ok = true }; }));

authed.MapPost("/habits/{id:int}/show-up", async (int id, ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    await Run(async () => { await world.ShowUp(await Current(p, users), id); return new { ok = true }; }));

authed.MapPost("/habits/{id:int}/retire", async (int id, ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    await Run(async () => { await world.RetireHabit(await Current(p, users), id); return new { ok = true }; }));

authed.MapGet("/habits", async (ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    Results.Ok(await world.Habits(await Current(p, users))));

authed.MapGet("/quests", async (ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    Results.Ok(await world.MilestoneQuests(await Current(p, users))));

authed.MapPost("/quests", async (QuestBody body, ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    await Run(async () => { await world.CreateMilestoneQuest(await Current(p, users), body.Name, body.Detail, body.HabitId, body.IsMain, body.Steps ?? []); return new { ok = true }; }));

authed.MapPost("/quests/{id:int}/milestones", async (int id, StepBody body, ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    await Run(async () => { await world.AddMilestone(await Current(p, users), id, body.Name); return new { ok = true }; }));

authed.MapPost("/milestones/{id:int}/complete", async (int id, ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    await Run(async () => { await world.CompleteMilestone(await Current(p, users), id); return new { ok = true }; }));

authed.MapPost("/quests/{id:int}/drop", async (int id, ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    await Run(async () => { await world.DropQuest(await Current(p, users), id); return new { ok = true }; }));

authed.MapPost("/quests/{id:int}/make-main", async (int id, ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    await Run(async () => { await world.MakeMain(await Current(p, users), id); return new { ok = true }; }));

authed.MapGet("/skills", async (ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    Results.Ok(await world.SkillsOf(await Current(p, users))));

authed.MapGet("/skills/{id:int}", async (int id, ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    await Run(async () => await world.SkillOf(await Current(p, users), id)));

authed.MapPost("/skills", async (SkillBody body, ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    await Run(async () => { await world.CreateSkill(await Current(p, users), body.Name, body.Meaning); return new { ok = true }; }));

authed.MapPut("/skills/{id:int}", async (int id, SkillEdit body, ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    await Run(async () => { await world.UpdateSkill(await Current(p, users), id, body.Name, body.Meaning, body.Rank); return new { ok = true }; }));

authed.MapPost("/skills/{id:int}/challenges", async (int id, ChallengeBody body, ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    await Run(async () => { await world.AddChallenge(await Current(p, users), id, body.Name, body.Detail, body.Required); return new { ok = true }; }));

authed.MapPost("/challenges/{id:int}/pass", async (int id, NoteBody body, ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    await Run(async () => { await world.PassChallenge(await Current(p, users), id, body.Note); return new { ok = true }; }));

authed.MapGet("/achievements", async (ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
{
    var rows = await world.AchievementsOf(await Current(p, users));
    return Results.Ok(rows.Select(a => new
    {
        a.Id,
        name = a.IsHidden && !a.Earned ? "????" : a.Name,
        detail = a.IsHidden && !a.Earned ? a.Hint : a.Detail,
        a.Hint,
        a.Earned,
        a.IsHidden,
        a.FameReward
    }));
});

authed.MapPost("/achievements", async (AchievementBody body, ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    await Run(async () => { await world.CreateAchievement(await Current(p, users), body.Name, body.Detail, body.Reward); return new { ok = true }; }));

authed.MapPost("/achievements/{id:int}/claim", async (int id, ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    await Run(async () => { await world.ClaimAchievement(await Current(p, users), id); return new { ok = true }; }));

authed.MapGet("/inventory", async (ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    Results.Ok(await world.ItemsOf(await Current(p, users))));

authed.MapPost("/inventory/{id:int}/equip", async (int id, ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    await Run(async () => { await world.Equip(await Current(p, users), id); return new { ok = true }; }));

authed.MapPost("/dailies/{id:int}/line", async (int id, NoteBody body, ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    await Run(async () => { await world.WriteLine(await Current(p, users), id, body.Note); return new { ok = true }; }));

authed.MapGet("/fame", async (ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    Results.Ok(await world.FameOf(await Current(p, users))));

authed.MapGet("/log", async (ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    Results.Ok(await world.LogOf(await Current(p, users))));

authed.MapPost("/push/subscribe", async (PushBody body, ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    await Run(async () => { await world.Subscribe(await Current(p, users), body.Endpoint, body.P256dh, body.Auth); return new { ok = true }; }));

authed.MapPost("/push/decline", async (ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    await Run(async () => { await world.DeclineSignal(await Current(p, users)); return new { ok = true }; }));

authed.MapGet("/players/{id}", async (string id, BindingWorld world) =>
    await Run(() => world.PublicCard(id)));

authed.MapGet("/leaderboard", async (BindingWorld world) => Results.Ok(await world.Register()));

authed.MapPost("/parties", async (NameBody body, ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    await Run(async () => await world.OpenParty(await Current(p, users), body.Name)));

authed.MapGet("/parties/mine", async (ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
{
    var user = await Current(p, users);
    var party = await world.MyParty(user);
    var quest = await world.PartyQuest(user);
    var members = await world.PartyWitness(user);
    return Results.Ok(new { party, quest, members });
});

authed.MapPost("/invites", async (ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    await Run(async () => new { code = await world.MintPartyInvite(await Current(p, users)) }));

authed.MapPost("/invites/{code}/accept", async (string code, ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    await Run(async () => { await world.AcceptInvite(await Current(p, users), code); return new { ok = true }; }));

authed.MapPost("/parties/quests", async (QuestBody body, ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    await Run(async () => { await world.CreateSharedQuest(await Current(p, users), false, null, body.Name, body.Steps ?? []); return new { ok = true }; }));

authed.MapPost("/parties/leave", async (ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    await Run(async () => { await world.LeaveParty(await Current(p, users)); return new { ok = true }; }));

authed.MapGet("/guilds", async (BindingWorld world) => Results.Ok(await world.Houses()));

authed.MapPost("/guilds", async (GuildBody body, ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    await Run(async () => await world.OpenGuild(await Current(p, users), body.Name, body.Creed)));

authed.MapGet("/guilds/mine", async (ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
{
    var user = await Current(p, users);
    var guild = await world.MyGuild(user);
    var quest = guild is null ? null : await world.GuildQuest(guild.Id);
    var members = await world.GuildMarks(user);
    var fed = members.Count(m => m.FedToday);
    return Results.Ok(new { guild, quest, members, line = guild is null ? null : Voice.HouseFed(fed) });
});

authed.MapPost("/guilds/{id:int}/invites", async (int id, ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    await Run(async () => new { code = await world.MintGuildInvite(await Current(p, users), id) }));

authed.MapPost("/guilds/quests", async (GuildQuestBody body, ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    await Run(async () => { await world.CreateSharedQuest(await Current(p, users), true, body.GuildId, body.Name, body.Steps ?? []); return new { ok = true }; }));

authed.MapPost("/guilds/leave", async (ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    await Run(async () => { await world.LeaveGuild(await Current(p, users)); return new { ok = true }; }));

authed.MapPost("/guilds/pass", async (TargetBody body, ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    await Run(async () => { await world.PassKeeper(await Current(p, users), body.UserId); return new { ok = true }; }));

authed.MapPost("/guilds/remove", async (TargetBody body, ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    await Run(async () => { await world.RemoveMember(await Current(p, users), body.UserId); return new { ok = true }; }));

authed.MapGet("/duels", async (ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    Results.Ok(await world.MyDuels(await Current(p, users))));

authed.MapPost("/duels", async (DuelBody body, ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    await Run(async () => { await world.CreateDuel(await Current(p, users), body.OpponentId, body.Terms, body.Deadline); return new { ok = true }; }));

authed.MapPost("/duels/{id:int}/accept", async (int id, ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    await Run(async () => { await world.AcceptDuel(await Current(p, users), id); return new { ok = true }; }));

authed.MapPost("/duels/{id:int}/decline", async (int id, ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    await Run(async () => { await world.DeclineDuel(await Current(p, users), id); return new { ok = true }; }));

authed.MapPost("/duels/{id:int}/clear", async (int id, NoteBody body, ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    await Run(async () => { await world.ClearDuel(await Current(p, users), id, body.Note); return new { ok = true }; }));

var admin = authed.MapGroup("/admin");
admin.AddEndpointFilter(async (context, next) =>
{
    if (context.HttpContext.User.IsInRole("Developer")) return await next(context);
    return Results.NotFound();
});

admin.MapGet("/", async (BindingWorld world) => Results.Ok(await world.AdminSummary()));
admin.MapGet("/hosts", async (string? q, BindingWorld world) => Results.Ok(await world.AdminHosts(q)));
admin.MapGet("/hosts/{id}", async (string id, BindingWorld world) => await Run(() => world.AdminHost(id)));
admin.MapPost("/hosts/{id}/lock", async (string id, LockBody body, ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    await Run(async () => { await world.SetLock(await Current(p, users), id, body.Locked, body.Reason); return new { ok = true }; }));
admin.MapPost("/hosts/{id}/sustenance", async (string id, AmountBody body, ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    await Run(async () => { await world.AdjustFame(await Current(p, users), id, body.Amount, body.Reason); return new { ok = true }; }));
admin.MapPost("/hosts/{id}/titles", async (string id, ReasonText body, ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    await Run(async () => { await world.GrantName(await Current(p, users), id, body.Text, body.Reason); return new { ok = true }; }));
admin.MapPost("/hosts/{id}/items", async (string id, ItemGrant body, ClaimsPrincipal p, UserManager<AppUser> users, BindingWorld world) =>
    await Run(async () => { await world.GrantThing(await Current(p, users), id, body.ItemKey, body.Reason); return new { ok = true }; }));
admin.MapGet("/gatherings", async (BindingWorld world) => Results.Ok(await world.AdminParties()));
admin.MapGet("/houses", async (BindingWorld world) => Results.Ok(await world.AdminGuilds()));
admin.MapGet("/calls", async (BindingWorld world) => Results.Ok(await world.AdminCalls()));
admin.MapGet("/audit", async (BindingWorld world) => Results.Ok(await world.AuditLog()));

app.Run();

static async Task<AppUser> Current(ClaimsPrincipal principal, UserManager<AppUser> users)
{
    var id = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
    var user = id is null ? null : await users.FindByIdAsync(id);
    if (user is null) throw new BindingException(401, "The binding does not know you.");
    if (await users.IsLockedOutAsync(user)) throw new BindingException(403, "This host cannot enter.");
    return user;
}

static async Task SyncDeveloper(AppUser user, UserManager<AppUser> users, IConfiguration config)
{
    var emails = config.GetSection("Developer:Emails").Get<string[]>() ?? [];
    var should = emails.Contains(user.Email ?? "", StringComparer.OrdinalIgnoreCase);
    var isDev = await users.IsInRoleAsync(user, "Developer");
    if (should && !isDev) await users.AddToRoleAsync(user, "Developer");
    if (!should && isDev) await users.RemoveFromRoleAsync(user, "Developer");
}

static async Task<AuthResponse> Token(AppUser user, UserManager<AppUser> users, IConfiguration config, string jwtKey)
{
    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, user.Id),
        new(JwtRegisteredClaimNames.Sub, user.Id),
        new(ClaimTypes.Email, user.Email ?? "")
    };
    if (await users.IsInRoleAsync(user, "Developer"))
        claims.Add(new Claim(ClaimTypes.Role, "Developer"));
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
    var token = new JwtSecurityToken(
        issuer: config["Jwt:Issuer"] ?? "binding",
        audience: config["Jwt:Audience"] ?? "binding",
        claims: claims,
        expires: DateTime.UtcNow.AddDays(14),
        signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
    return new AuthResponse(new JwtSecurityTokenHandler().WriteToken(token), user.DisplayName, await users.IsInRoleAsync(user, "Developer"));
}

static async Task<IResult> Run<T>(Func<Task<T>> work)
{
    try { return Results.Ok(await work()); }
    catch (BindingException ex) { return Fail(ex); }
}

static IResult Fail(BindingException ex) =>
    Results.Json(new { error = ex.Message }, statusCode: ex.Status);

record RegisterBody(string Email, string Password, string DisplayName, string? InviteCode);
record LoginBody(string Email, string Password);
record AuthResponse(string Token, string DisplayName, bool IsDeveloper);
record ProfileBody(string? DisplayName, string? ConditionLabel, string? ConditionNote, string? TimeZone, string? ArcName, string? ArcNote);
record TitleBody(int? TitleId, string? Text);
record HabitBody(string Identity, string Cue, string CueTime, string TwoMinute, string? FullVersion, int Weekdays, bool Keystone);
record QuestBody(string Name, string? Detail, int? HabitId, bool IsMain, List<string>? Steps);
record StepBody(string Name);
record SkillBody(string Name, string? Meaning);
record SkillEdit(string? Name, string? Meaning, SkillRank? Rank);
record ChallengeBody(string Name, string? Detail, bool Required);
record NoteBody(string Note);
record AchievementBody(string Name, string? Detail, int? Reward);
record PushBody(string Endpoint, string P256dh, string Auth);
record NameBody(string? Name);
record GuildBody(string Name, string Creed);
record GuildQuestBody(int GuildId, string Name, List<string>? Steps);
record TargetBody(string UserId);
record DuelBody(string OpponentId, string Terms, DateOnly Deadline);
record LockBody(bool Locked, string Reason);
record AmountBody(int Amount, string Reason);
record ReasonText(string Text, string Reason);
record ItemGrant(string ItemKey, string Reason);
