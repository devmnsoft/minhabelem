namespace MinhaBelem.Web.Models;

public sealed class ProjectEvolutionViewModel
{
    public int SprintNumber { get; set; } = 14;
    public string SprintTitle { get; set; } = "Redesign e homologação";
    public string Domain { get; set; } = "minhabelem.com.br";
    public string Environment { get; set; } = "VPS Windows / IIS / MySQL 8 / MinhaBelem.Worker";
    public int TotalRequirements { get; set; } = 0;
    public int MetRequirements { get; set; } = 0;
    public decimal ReadinessPercentage => TotalRequirements > 0 ? Math.Round(((decimal)MetRequirements / TotalRequirements) * 100, 1) : 0;
    public int TotalTests { get; set; } = 0;
    public int PassedTests { get; set; } = 0;
    public int FailedTests { get; set; } = 0;
    public string LastActionsRunUrl { get; set; } = "https://github.com/devmnsoft/minhabelem/actions";
    public string ActionsStatusText { get; set; } = "Sem resultado de CI carregado";
    public string WorkerStatus { get; set; } = "Sem evidência de execução";
    public int OperationalSourcesCount { get; set; }
    public int DiscoveredEventsCount { get; set; }
    public int SocialPostsCount { get; set; }
    public IReadOnlyList<ModuleEvolutionStatus> Modules { get; set; } = Array.Empty<ModuleEvolutionStatus>();
    public IReadOnlyList<QualityIndicator> QualityIndicators { get; set; } = Array.Empty<QualityIndicator>();
    public IReadOnlyList<CommitEntry> RecentCommits { get; set; } = Array.Empty<CommitEntry>();
}

public sealed class ModuleEvolutionStatus
{
    public string ModuleName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    /// <summary>
    /// Classificação oficial: "Não iniciado", "Em desenvolvimento", "Implementado", "Testado", "Homologado", "Operacional em produção", "Bloqueado"
    /// </summary>
    public string Status { get; set; } = "Não verificado";
    public int ProgressPercent { get; set; } = 0;
    public string Evidence { get; set; } = string.Empty;
}

public sealed class QualityIndicator
{
    public string Name { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string Actual { get; set; } = string.Empty;
    public bool Passed { get; set; } = false;
    public string Notes { get; set; } = string.Empty;
}

public sealed class CommitEntry
{
    public string Hash { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Author { get; set; } = "MNSOFT / Antigravity AI";
    public DateTime Date { get; set; }
}
