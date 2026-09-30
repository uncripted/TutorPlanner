using Microsoft.EntityFrameworkCore;
using Tutorplanner.Components;
using Tutorplanner.Data;
using Tutorplanner.Services;
using Tutorplanner.Models;
using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCors(options => options.AddPolicy("public-booking", policy =>
{
    var origins = (Environment.GetEnvironmentVariable("TUTORPLANNER_PUBLIC_ORIGINS") ?? string.Empty)
        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (origins.Length > 0) policy.WithOrigins(origins);
    policy.AllowAnyHeader().AllowAnyMethod();
}));

// Register EF Core with SQLite for local storage
var databasePath = Environment.GetEnvironmentVariable("TUTORPLANNER_DATABASE_PATH")
    ?? Path.Combine(builder.Environment.ContentRootPath, "tutorplanner.db");
builder.Services.AddDbContext<TutorDbContext>(options =>
    options.UseSqlite($"Data Source={databasePath}"));

// Application services
builder.Services.AddScoped<TutorPlannerService>();

var app = builder.Build();

var publicOrigins = (Environment.GetEnvironmentVariable("TUTORPLANNER_PUBLIC_ORIGINS") ?? string.Empty)
    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
var publicApi = "public-booking";
var requestTimes = new ConcurrentDictionary<string, Queue<DateTime>>();

// Ensure database created on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TutorDbContext>();
    await TutorPlannerDatabase.InitializeAsync(db);
    var planner = scope.ServiceProvider.GetRequiredService<TutorPlannerService>();
    await planner.GenerateLessonsAsync(DateTime.Today.AddDays(30));
    await planner.ReallocateAllPaymentsAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseCors();

app.UseAntiforgery();

app.UseStaticFiles();

app.MapGet("/api/public-booking/availability", async (DateTime date, TutorPlannerService planner) =>
{
    if (date.Date < DateTime.Today || date.Date > DateTime.Today.AddDays(30))
    {
        return Results.BadRequest(new { error = "Date must be within the next 30 days." });
    }

    return Results.Ok(new PublicAvailabilityResponse(date.Date, await planner.GetAvailableSlotsAsync(date.Date)));
}).RequireCors(publicApi);

app.MapPost("/api/public-booking/requests", async (HttpContext http, PublicBookingRequest input, TutorPlannerService planner) =>
{
    var origin = http.Request.Headers.Origin.ToString();
    if (publicOrigins.Length > 0 && !publicOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var clientKey = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var now = DateTime.UtcNow;
    var queue = requestTimes.GetOrAdd(clientKey, _ => new Queue<DateTime>());
    lock (queue)
    {
        while (queue.Count > 0 && now - queue.Peek() > TimeSpan.FromHours(1)) queue.Dequeue();
        if (queue.Count >= 10) return Results.StatusCode(StatusCodes.Status429TooManyRequests);
        queue.Enqueue(now);
    }

    if (string.IsNullOrWhiteSpace(input.Name) || string.IsNullOrWhiteSpace(input.Contact) || input.PreferredStart == default)
    {
        return Results.BadRequest(new { error = "Name, contact, and preferred time are required." });
    }

    var validSlots = await planner.GetAvailableSlotsAsync(input.PreferredStart.Date);
    if (!validSlots.Contains(input.PreferredStart))
    {
        return Results.BadRequest(new { error = "The selected time is no longer available." });
    }

    var request = new BookingRequest
    {
        Name = input.Name,
        Contact = input.Contact,
        Subject = string.IsNullOrWhiteSpace(input.Subject) ? "ზოგადი გაკვეთილი" : input.Subject,
        PreferredStart = input.PreferredStart,
        AlternativeStart = input.AlternativeStart,
        Notes = input.Notes
    };
    var id = await planner.SubmitBookingAsync(request);
    return Results.Ok(new PublicBookingResponse(id, "მოთხოვნა წარმატებით გაიგზავნა."));
}).RequireCors(publicApi);

app.MapMethods("/{**path}", new[] { "OPTIONS" }, () => Results.Ok()).RequireCors(publicApi);
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
