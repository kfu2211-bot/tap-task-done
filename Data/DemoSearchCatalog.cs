namespace tts.Data;

// Search-only examples. Negative IDs never identify real database accounts/tasks.
public static class DemoSearchCatalog
{
    public record Provider(int Id, string Name, string Service, string Location, int Rate, string Availability);
    public static readonly Provider[] Providers =
    [
        new(-1, "[DEMO] Ana Reyes", "Cleaning", "San Fernando, Pampanga", 500, "Available"),
        new(-2, "[DEMO] Marco Cruz", "Cleaning", "San Fernando, Pampanga", 650, "Available"),
        new(-3, "[DEMO] Bea Santos", "Cleaning", "Angeles, Pampanga", 900, "Busy"),
        new(-4, "[DEMO] Carlo Garcia", "Plumbing repairs", "San Fernando, Pampanga", 800, "Available"),
        new(-5, "[DEMO] Dina Ramos", "Electrical repairs", "Angeles, Pampanga", 1000, "Available"),
        new(-6, "[DEMO] Eli Mendoza", "Gardening", "Bacolor, Pampanga", 450, "Available"),
        new(-7, "[DEMO] Faye Flores", "Gardening", "San Fernando, Pampanga", 700, "Day off"),
        new(-8, "[DEMO] Gio Torres", "Moving and hauling", "Angeles, Pampanga", 1500, "Available"),
        new(-9, "[DEMO] Hana Lim", "Painting", "Mexico, Pampanga", 1200, "Available"),
        new(-10, "[DEMO] Ivan Aquino", "Math tutoring", "San Fernando, Pampanga", 350, "Available"),
        new(-11, "[DEMO] Jessa Tan", "Pet care", "Bacolor, Pampanga", 400, "Busy"),
        new(-12, "[DEMO] Ken Navarro", "Errands and delivery", "Mexico, Pampanga", 300, "Available"),
        new(-13, "[DEMO] Lia David", "Balloon artist", "San Fernando, Pampanga", 900, "Available")
    ];

    public static IEnumerable<MarketplaceTask> Tasks() => Providers.Take(8).Select(p => new MarketplaceTask
    {
        Id = p.Id, ClientId = -100, Title = "[DEMO] " + p.Service + " request", Category = p.Service,
        Description = "Sample task for classroom search and matching. No real booking or payment.",
        Location = p.Location, Budget = p.Rate + 200, ScheduleText = "Demo schedule — to be arranged", Status = "open"
    });
}
