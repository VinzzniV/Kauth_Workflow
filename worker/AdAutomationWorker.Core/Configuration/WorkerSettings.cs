namespace AdAutomationWorker.Core.Configuration;

// Konfiguration des Windows-Workers. Wird per IOptions aus appsettings.json gebunden.
// Defaults entsprechen den Schritt-1-Sub-Entscheidungen (Heartbeat 30s, Stale 5min).
public sealed class WorkerSettings
{
    // Eindeutige Worker-Kennung im Format <hostname>:<pid>:<startup-uuid>. Wird beim Start
    // einmalig gesetzt und in automation_jobs.claimed_by geschrieben.
    public string WorkerId { get; set; } = string.Empty;

    public int PollingIntervalSeconds { get; set; } = 5;

    public int HeartbeatIntervalSeconds { get; set; } = 30;

    public int StaleClaimTimeoutMinutes { get; set; } = 5;

    public int ClaimBatchSize { get; set; } = 1;

    public AdSettings Ad { get; set; } = new();

    public VaultSettings Vault { get; set; } = new();
}

// Konfig fuer den Temporary-Credentials-Vault (Etappe 9a Schritt 6).
// TTL ist absichtlich grosszuegig (7 Tage Default) -- der Lese-Pfad wirft bei Ablauf
// mit klarer Meldung; das ist sicherer als ein zu kurzer TTL, der echte Onboarding-
// Workflows blockiert. Cleanup-Sweeper ist Folge-Slice.
public sealed class VaultSettings
{
    public int TemporaryCredentialTtlSeconds { get; set; } = 7 * 24 * 3600;
}

// Konfig fuer LDAPS-Verbindungen. Wird vom Host-Adapter `LdapsAdUserWriter` konsumiert; der
// Core selbst macht damit nichts.
public sealed class AdSettings
{
    // FQDN eines erreichbaren DCs, z.B. "dc01.example.local". Wird im Klartext fuer die LDAPS-
    // Connection benutzt. Kein Plural-Failover in V1 — fuer Multi-DC kommt in einem Folge-Slice
    // ein dedizierter Connection-Pool.
    public string DcHost { get; set; } = string.Empty;

    // Base-DN fuer Pre-Search (z.B. "DC=example,DC=local"). Subtree-Scope.
    public string BaseDn { get; set; } = string.Empty;

    public int ConnectionTimeoutSeconds { get; set; } = 30;
}

