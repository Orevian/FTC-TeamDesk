using FTC.TeamDesk.AI.Chat;
using FTC.TeamDesk.Core.Abstractions.Repositories;
using FTC.TeamDesk.Core.Abstractions.Services;
using FTC.TeamDesk.Core.Enums;

namespace FTC.TeamDesk.AI.Tools;

/// <summary>The fixed catalogue of tools the assistant may call, filtered by the user's permissions on every request.</summary>
public sealed class AiToolRegistry
{
    private readonly IReadOnlyList<IAiTool> _tools;
    private readonly IAiConfigRepository _config;

    public AiToolRegistry(IAiConfigRepository config, IMemberService members, IApplicationService applications,
        IBudgetService budget, IAnalyticsService analytics, IVaultService vault)
    {
        _config = config;
        _tools = new IAiTool[]
        {
            new GetMembersTool(members), new GetMemberTool(members), new GetApplicationsTool(applications), new GetApplicationTool(applications),
            new GetBudgetTool(budget), new GetPurchasesTool(budget), new GetAnalyticsTool(analytics), new GetTeamSummaryTool(analytics),
            new GetVaultEntriesTool(vault),
            new AddPurchaseTool(budget), new EditPurchaseTool(budget), new DeletePurchaseTool(budget),
            new UpdateMemberTool(members), new UpdateApplicationStatusTool(applications)
        };
    }

    public IReadOnlyList<string> AllToolNames => _tools.Select(t => t.Name).ToList();

    public async Task<IReadOnlyList<IAiTool>> GetAllowedAsync(CancellationToken ct)
    {
        var perms = await _config.GetPermissionsAsync(ct).ConfigureAwait(false);
        return _tools.Where(t => perms.GetValueOrDefault(t.Permission)).ToList();
    }

    public static ToolDefinition ToDefinition(IAiTool tool) => new(tool.Name, tool.Description, tool.Schema);
}
