using Npgsql;

namespace API;

// Dediziertes Repository fuer alle Rotations-/Durchlauf-Operationen.
// Aus PostgresWorkflowRepository ausgeschnitten, um die fachliche Trennung zwischen Workflow-Kern
// und Rotations-Feature sichtbar zu machen und die monolithische Klasse zu verkleinern.
internal sealed partial class PostgresRotationRepository : IRotationRepository, IRotationNotificationPreviewRepository
{
    // Verbindung wird bewusst direkt aus der Umgebung gelesen, damit API und Container identisch konfiguriert bleiben.
    private static string GetConnectionString()
    {
        return LifecycleRuntimeSettingsResolver.GetRequiredConnectionString();
    }
}
