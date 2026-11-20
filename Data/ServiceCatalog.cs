using System.Text.Json;

namespace tts.Data;

public static class ServiceCatalog
{
    public const string OtherCategory = "Other/General";
    public const string CustomOption = "Other";
    private const string CustomPrefix = "Other: ";

    public static IReadOnlyDictionary<string, string[]> Options { get; } = new Dictionary<string, string[]>
    {
        ["Cleaning"] = ["House cleaning", "Deep cleaning", "Bathroom cleaning", "Kitchen cleaning", "Office cleaning", "Move-in / move-out cleaning"],
        ["Repairs"] = ["Plumbing", "Electrical repair", "Appliance repair", "Aircon repair", "Door and lock repair", "Furniture repair"],
        ["Errands"] = ["Grocery shopping", "Parcel pickup and delivery", "Document pickup and drop-off", "Bill payment", "Queue assistance"],
        ["Moving"] = ["Packing and unpacking", "Loading and unloading", "Furniture moving", "Small-item hauling", "Furniture assembly and disassembly"],
        ["Gardening"] = ["Lawn mowing", "Grass cutting and weeding", "Hedge trimming", "Planting and repotting", "Garden cleanup"],
        ["Painting"] = ["Interior wall painting", "Exterior wall painting", "Ceiling painting", "Door and cabinet painting", "Wall preparation and touch-ups"],
        ["Tutoring"] = ["Reading and writing", "Math", "English", "Filipino", "Science", "Homework support", "Exam review"],
        ["Pet Care"] = ["Dog walking", "Pet sitting", "Feeding and check-ins", "Pet bathing and brushing", "Pet transport"]
    };

    public static Dictionary<string, string[]> Read(string? json)
    {
        if (string.IsNullOrEmpty(json)) return [];
        try { return JsonSerializer.Deserialize<Dictionary<string, string[]>>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    public static string? CustomName(IEnumerable<string> choices) =>
        choices.FirstOrDefault(choice => choice.StartsWith(CustomPrefix, StringComparison.Ordinal))?[CustomPrefix.Length..];

    public static bool TryBuild(IEnumerable<string> categories, string[] choices,
        IReadOnlyDictionary<string, string?> customNames, out string? json)
    {
        json = null;
        var selected = categories.ToHashSet();
        var groups = new Dictionary<string, List<string>>();
        foreach (var choice in choices.Distinct())
        {
            var parts = choice.Split('|', 2);
            if (parts.Length != 2 || !Options.TryGetValue(parts[0], out var options)) return false;
            if (!selected.Contains(parts[0])) continue;
            var service = parts[1];
            if (service == CustomOption)
            {
                var custom = customNames.GetValueOrDefault(parts[0])?.Trim();
                if (string.IsNullOrWhiteSpace(custom) || custom.Length > 60) return false;
                service = CustomPrefix + custom;
            }
            else if (!options.Contains(service)) return false;

            if (!groups.TryGetValue(parts[0], out var services))
                groups[parts[0]] = services = [];
            services.Add(service);
        }
        if (groups.Count > 0) json = JsonSerializer.Serialize(groups);
        return true;
    }

    public static bool Matches(string? categories, string? json, MarketplaceTask task)
    {
        var category = task.Category.Replace(" / ", "/");
        var offered = (categories ?? "").Split(", ", StringSplitOptions.RemoveEmptyEntries);
        var hasCategory = offered.Any(service => service.Equals(category, StringComparison.OrdinalIgnoreCase) ||
            category == OtherCategory && service.StartsWith("Other/General:", StringComparison.OrdinalIgnoreCase));
        if (!hasCategory) return false;
        if (string.IsNullOrWhiteSpace(task.SpecificService)) return true;
        var specialties = Read(json).GetValueOrDefault(category) ?? [];
        return task.SpecificService == CustomOption
            ? specialties.Any(service => service.StartsWith(CustomPrefix, StringComparison.Ordinal))
            : specialties.Contains(task.SpecificService);
    }

    public static string DisplayServiceName(string service) =>
        service.StartsWith(CustomPrefix, StringComparison.Ordinal) ? service[CustomPrefix.Length..] : service;

    public static string Display(string categories, IReadOnlyDictionary<string, string[]> specialties) =>
        string.Join(" · ", categories.Split(", ").Select(category =>
            specialties.TryGetValue(category, out var services)
                ? $"{category}: {string.Join(", ", services.Select(DisplayServiceName))}"
                : category));
}
