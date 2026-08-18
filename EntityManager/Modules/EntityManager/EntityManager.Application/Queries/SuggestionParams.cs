// PURPOSE: the REAL ag-kit SuggestionParams helper, unmodified - shared
// clamping/parsing logic for all 3 Suggest queries below.
namespace EntityManager.Application.Queries;

internal static class SuggestionParams
{
    private const int DefaultPageLimit = 30;
    private const int MaxPageLimit = 100;

    internal static int Take(int? pageLimit) => Math.Clamp(pageLimit ?? DefaultPageLimit, 1, MaxPageLimit);

    internal static List<int> ParseExcludeKeys(string? excludeKeys) =>
        (excludeKeys ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var key) ? key : 0)
            .Where(x => x > 0)
            .ToList();
}
