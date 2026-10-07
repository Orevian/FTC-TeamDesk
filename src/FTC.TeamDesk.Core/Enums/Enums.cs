namespace FTC.TeamDesk.Core.Enums;

public enum ApplicationStatus { Pending, Reviewing, Accepted, Rejected, Maybe }

public enum PurchaseStatus { Planned, Ordered, Purchased, Received, Cancelled }

public enum ThemeMode { System, Light, Dark }

public enum StartupPage { Dashboard, LastVisited }

public enum AiProviderKind { OpenAI, Gemini, Anthropic, Mistral, Ollama, OpenAiCompatible }

public enum ConnectionState { Disconnected, Connected, Error }

public enum SortDirection { Ascending, Descending }

public enum ApplicationSortField { SubmittedAt, ApplicantName, Status }

/// <summary>Permission identifiers. The string form is persisted in the AIPermissions table.</summary>
public enum AiPermissionKey
{
    ReadMembers,
    ReadApplications,
    ReadBudget,
    ReadPurchases,
    ReadAnalytics,
    AddPurchase,
    EditPurchase,
    DeletePurchase,
    EditMember,
    ChangeApplicationStatus,
    AccessPasswordVault
}

public enum ActivityActor { User, Ai, System }
