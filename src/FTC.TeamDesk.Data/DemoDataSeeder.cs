using FTC.TeamDesk.Core.Entities;
using FTC.TeamDesk.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace FTC.TeamDesk.Data;

/// <summary>DEMO ONLY. Fictional data so a fresh install can be explored. Triggered explicitly from Settings > Data.</summary>
internal static class DemoDataSeeder
{
    public static async Task SeedAsync(TeamDeskDbContext db, CancellationToken ct)
    {
        var today = DateTime.Today;
        var statuses = await db.MemberStatuses.ToDictionaryAsync(s => s.Key, ct).ConfigureAwait(false);
        var areas = await db.Areas.ToDictionaryAsync(a => a.Key, ct).ConfigureAwait(false);
        var cats = await db.BudgetCategories.ToDictionaryAsync(c => c.Key, ct).ConfigureAwait(false);

        (string Name, string Status, string[] Areas, string[] Skills, int MonthsAgo, string Resp)[] people =
        {
            ("Ada Şahin", "accepted", new[]{"software","cad","leadership"}, new[]{"Java","Android Studio","Onshape","Git"}, 12, "Lead programmer"),
            ("Can Demir", "accepted", new[]{"mechanical","cad"}, new[]{"Onshape","Machining","Assembly"}, 12, "Drivetrain design"),
            ("Zeynep Kaya", "accepted", new[]{"electronics"}, new[]{"Soldering","Wiring","Sensors"}, 10, "Electronics lead"),
            ("Mert Aydın", "accepted", new[]{"software"}, new[]{"Java","Computer vision"}, 8, "Autonomous routines"),
            ("Deniz Arslan", "accepted", new[]{"communication","design"}, new[]{"Public speaking","Graphic design"}, 9, "Outreach and sponsors"),
            ("Elif Yılmaz", "accepted", new[]{"mechanical"}, new[]{"Assembly","Machining"}, 6, "Lift mechanism"),
            ("Burak Çelik", "accepted", new[]{"cad","design"}, new[]{"Onshape","3D printing"}, 6, "CAD release"),
            ("Selin Öztürk", "accepted", new[]{"leadership","communication"}, new[]{"Project management","Writing"}, 12, "Team captain"),
            ("Kaan Polat", "pending", new[]{"software"}, new[]{"Java"}, 1, "Trial period"),
            ("Ece Türk", "candidate", new[]{"electronics"}, new[]{"Wiring"}, 0, "Under review"),
            ("Emre Koç", "former", new[]{"mechanical"}, new[]{"Machining"}, 18, "Graduated"),
        };
        var skillCache = new Dictionary<string, Skill>(StringComparer.OrdinalIgnoreCase);
        var rng = new Random(42);
        var members = new List<Member>();
        foreach (var p in people)
        {
            var m = new Member
            {
                FullName = p.Name, StatusId = statuses[p.Status].Id, JoinDate = today.AddMonths(-p.MonthsAgo),
                Responsibilities = p.Resp, Notes = null
            };
            db.Members.Add(m);
            foreach (var a in p.Areas) db.MemberAreas.Add(new MemberArea { MemberId = m.Id, AreaId = areas[a].Id });
            foreach (var s in p.Skills)
            {
                if (!skillCache.TryGetValue(s, out var skill)) { skill = new Skill { Name = s }; skillCache[s] = skill; db.Skills.Add(skill); }
                db.MemberSkills.Add(new MemberSkill { MemberId = m.Id, SkillId = skill.Id });
            }
            if (p.Status == "accepted")
            {
                foreach (var a in p.Areas.Take(2))
                {
                    var score = 55 + rng.Next(0, 15);
                    for (var i = 5; i >= 0; i--)
                    {
                        score = Math.Min(98, score + rng.Next(1, 8));
                        db.PerformanceRecords.Add(new PerformanceRecord
                        { MemberId = m.Id, AreaId = areas[a].Id, RecordedOn = new DateTime(today.Year, today.Month, 1).AddMonths(-i), Score = score });
                    }
                }
            }
            members.Add(m);
        }

        (string Name, string Email, int DaysAgo, ApplicationStatus Status, string Skills, string Exp, string Why)[] apps =
        {
            ("Ahmet Yıldız", "ahmet@example.com", 2, ApplicationStatus.Pending, "Java, CAD", "Two seasons on a school robotics club.", "I want to build the autonomous code."),
            ("Beyza Korkmaz", "beyza@example.com", 3, ApplicationStatus.Reviewing, "Onshape, 3D printing", "Designed parts for a FRC team.", "I enjoy mechanical design and prototyping."),
            ("Cem Aksoy", "cem@example.com", 5, ApplicationStatus.Accepted, "Electronics, Soldering", "Built Arduino projects for 3 years.", "I would like to own robot wiring."),
            ("Derya Erdem", "derya@example.com", 6, ApplicationStatus.Maybe, "Writing, Social media", "Ran a school newspaper.", "I can help with outreach and sponsors."),
            ("Efe Güneş", "efe@example.com", 8, ApplicationStatus.Rejected, "Python", "Self-taught, no team experience.", "I like robots."),
            ("Fatma Sönmez", "fatma@example.com", 9, ApplicationStatus.Reviewing, "Java, Computer vision", "OpenCV hobby projects.", "Vision-based autonomous is my goal."),
            ("Gökhan Bulut", "gokhan@example.com", 12, ApplicationStatus.Pending, "Machining, CAD", "Vocational school, CNC training.", "I can machine custom parts."),
            ("Hilal Kurt", "hilal@example.com", 15, ApplicationStatus.Accepted, "Project management", "Led a science fair team.", "I can help coordinate the schedule."),
        };
        foreach (var a in apps)
        {
            var app = new TeamApplication
            {
                ApplicantName = a.Name, Email = a.Email, SubmittedAt = today.AddDays(-a.DaysAgo).AddHours(14), Status = a.Status,
                Skills = a.Skills, Experience = a.Exp, ExternalKey = "demo-" + a.Email
            };
            app.Answers.Add(new ApplicationAnswer { ApplicationId = app.Id, Question = "Why do you want to join?", Answer = a.Why, SortOrder = 0 });
            app.Answers.Add(new ApplicationAnswer { ApplicationId = app.Id, Question = "Which area interests you most?", Answer = a.Skills, SortOrder = 1 });
            app.Answers.Add(new ApplicationAnswer { ApplicationId = app.Id, Question = "Weekly availability (hours)", Answer = (6 + a.DaysAgo % 5).ToString(), SortOrder = 2 });
            db.Applications.Add(app);
        }

        var budget = await db.Budgets.FirstAsync(ct).ConfigureAwait(false);
        budget.TotalAmount = 10000m;
        budget.Currency = "USD";
        budget.UpdatedAt = DateTime.UtcNow;

        (string Product, string Cat, decimal Price, int Qty, int DaysAgo, PurchaseStatus Status, string Seller)[] buys =
        {
            ("Strafer chassis kit", "robot", 389.99m, 1, 40, PurchaseStatus.Received, "goBILDA"),
            ("Control Hub", "electronics", 299.95m, 1, 35, PurchaseStatus.Received, "REV Robotics"),
            ("Expansion Hub", "electronics", 269.95m, 1, 35, PurchaseStatus.Received, "REV Robotics"),
            ("Regional registration", "registration", 450m, 1, 30, PurchaseStatus.Purchased, "FIRST"),
            ("Aluminum extrusion 20x20", "materials", 84.50m, 6, 12, PurchaseStatus.Ordered, "McMaster-Carr"),
            ("Servo", "electronics", 30m, 4, 3, PurchaseStatus.Planned, "goBILDA"),
            ("Battery 12V", "electronics", 59.99m, 2, 9, PurchaseStatus.Purchased, "REV Robotics"),
            ("Hand tool set", "tools", 240m, 1, 25, PurchaseStatus.Purchased, "Local store"),
            ("Team banner", "marketing", 160m, 1, 20, PurchaseStatus.Purchased, "PrintShop"),
            ("Polycarbonate sheet", "materials", 110m, 2, 7, PurchaseStatus.Planned, "Local store"),
            ("Travel to regional", "travel", 600m, 1, -20, PurchaseStatus.Planned, "Bus company"),
            ("Linear slide kit", "mechanical", 720m, 1, 18, PurchaseStatus.Received, "goBILDA"),
            ("Cancelled camera", "electronics", 89m, 1, 15, PurchaseStatus.Cancelled, "Amazon"),
        };
        foreach (var b in buys)
            db.Purchases.Add(new Purchase
            {
                Product = b.Product, CategoryId = cats[b.Cat].Id, UnitPrice = b.Price, Quantity = b.Qty,
                PurchaseDate = today.AddDays(-b.DaysAgo), Status = b.Status, Seller = b.Seller
            });

        (string Title, int DueIn, bool Done)[] tasks =
        {
            ("Submit regional registration", 4, false), ("Order aluminum extrusion", 7, false),
            ("Robot design review meeting", 9, false), ("Sponsor letter draft", 15, false), ("Confirm travel booking", 22, false),
            ("Update team website", -3, true),
        };
        foreach (var t in tasks) db.Tasks.Add(new TeamTask { Title = t.Title, DueDate = today.AddDays(t.DueIn), IsCompleted = t.Done });

        db.ActivityLog.Add(new ActivityLogEntry { ActionType = "SettingsChanged", Description = "Demo data loaded", Actor = "System" });
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
