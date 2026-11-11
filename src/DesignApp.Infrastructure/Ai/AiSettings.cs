using static DesignApp.Infrastructure.Constants;

namespace DesignApp.Infrastructure.Ai;

/// <summary>
/// How the real AI is tuned: Claude's model, effort, limits and prompt, and SAM 3's endpoint and
/// mask rules. Stored as the single row of the <c>ai_settings</c> table, so a change (for now in
/// pgAdmin, later in an admin app) applies to the next analysis without a rebuild or restart.
/// The API keys are not here; they stay in the configuration's secrets.
/// </summary>
public sealed class AiSettings
{
    /// <summary>The row's id; there is only ever row <see cref="Constants.Persistence.AiSettingsRowId"/>.</summary>
    public int Id { get; set; } = Constants.Persistence.AiSettingsRowId;

    /// <summary>The Claude model that lists the objects.</summary>
    public string InventoryModel { get; set; } = Constants.Ai.Defaults.InventoryModel;

    /// <summary>How much Claude reasons while listing.</summary>
    public InventoryEffort InventoryEffort { get; set; } = Constants.Ai.Defaults.InventoryEffort;

    /// <summary>The most tokens Claude may answer with.</summary>
    public int MaxTokens { get; set; } = Constants.Ai.Defaults.MaxTokens;

    /// <summary>
    /// Claude's instructions. <see cref="Constants.Ai.Placeholders.MaxPoints"/> and
    /// <see cref="Constants.Ai.Placeholders.MaxListedObjects"/> are replaced by this row's limits
    /// (see <see cref="SystemPrompt"/>).
    /// </summary>
    public string Instructions { get; set; } = Constants.Ai.Defaults.Instructions;

    /// <summary>The text sent with the photo.</summary>
    public string UserMessage { get; set; } = Constants.Ai.Defaults.UserMessage;

    /// <summary>The most objects Claude is asked to list.</summary>
    public int MaxListedObjects { get; set; } = Constants.Ai.Defaults.MaxListedObjects;

    /// <summary>The most points Claude puts on a wall, floor or ceiling.</summary>
    public int MaxPoints { get; set; } = Constants.Ai.Defaults.MaxPoints;

    /// <summary>The SAM 3 endpoint on fal.ai.</summary>
    public string SegmentationEndpoint { get; set; } = Constants.Ai.Defaults.SegmentationEndpoint;

    /// <summary>How many SAM 3 requests run at the same time for one photo.</summary>
    public int SegmentationParallelism { get; set; } = Constants.Ai.Defaults.SegmentationParallelism;

    /// <summary>How many masks SAM 3 returns for one object to pick the best-fitting from.</summary>
    public int MaxMasks { get; set; } = Constants.Ai.Defaults.MaxMasks;

    /// <summary>How well (0 to 1) a mask must fit Claude's box to be used without asking again.</summary>
    public double MinFit { get; set; } = Constants.Ai.Defaults.MinFit;

    /// <summary>How much of a mask (0 to 1) must lie inside the box to count as a piece of the object.</summary>
    public double MinShareInsideBox { get; set; } = Constants.Ai.Defaults.MinShareInsideBox;

    /// <summary>How much of Claude's box (0 to 1) a mask must fill before its fit counts in full.</summary>
    public double MinFill { get; set; } = Constants.Ai.Defaults.MinFill;

    /// <summary>The instructions with this row's limits filled in.</summary>
    /// <returns>The system prompt sent to Claude.</returns>
    public string SystemPrompt()
    {
        return Instructions
            .Replace(Constants.Ai.Placeholders.MaxPoints, MaxPoints.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace(Constants.Ai.Placeholders.MaxListedObjects, MaxListedObjects.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    /// <summary>
    /// What is wrong with the values, if anything. The row is edited by hand for now, so a typo must
    /// stop the analysis with a clear reason instead of sending a broken request.
    /// </summary>
    /// <returns>One message per problem; empty when the settings can be used.</returns>
    public IReadOnlyList<string> Problems()
    {
        var problems = new List<string>();
        void Check(bool ok, string field, string rule)
        {
            if (!ok)
                problems.Add(Constants.Messages.InvalidAiSetting(field, rule));
        }

        Check(!string.IsNullOrWhiteSpace(InventoryModel), nameof(InventoryModel), Constants.Ai.Rules.NotEmpty);
        Check(Enum.IsDefined(InventoryEffort), nameof(InventoryEffort), Constants.Ai.Rules.Effort);
        Check(MaxTokens > 0, nameof(MaxTokens), Constants.Ai.Rules.Positive);
        Check(!string.IsNullOrWhiteSpace(Instructions), nameof(Instructions), Constants.Ai.Rules.NotEmpty);
        Check(!string.IsNullOrWhiteSpace(UserMessage), nameof(UserMessage), Constants.Ai.Rules.NotEmpty);
        Check(MaxListedObjects is > 0 and <= Application.Constants.Analysis.MaxObjects, nameof(MaxListedObjects), Constants.Ai.Rules.ListedObjects);
        Check(MaxPoints >= 0, nameof(MaxPoints), Constants.Ai.Rules.NotNegative);
        Check(Uri.TryCreate(SegmentationEndpoint, UriKind.Absolute, out var endpoint) && endpoint.Scheme == Uri.UriSchemeHttps,
            nameof(SegmentationEndpoint), Constants.Ai.Rules.HttpsUrl);
        Check(SegmentationParallelism > 0, nameof(SegmentationParallelism), Constants.Ai.Rules.Positive);
        Check(MaxMasks > 0, nameof(MaxMasks), Constants.Ai.Rules.Positive);
        Check(MinFit is >= 0 and <= 1, nameof(MinFit), Constants.Ai.Rules.Fraction);
        Check(MinShareInsideBox is >= 0 and <= 1, nameof(MinShareInsideBox), Constants.Ai.Rules.Fraction);
        Check(MinFill is >= 0 and <= 1, nameof(MinFill), Constants.Ai.Rules.Fraction);
        return problems;
    }
}
