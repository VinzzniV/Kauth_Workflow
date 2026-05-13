namespace API;

// Lese-Pfad fuer das Vault aus dem Linux-API (Late-Decrypt-Pattern).
// Konsumiert wird der entschluesselte Wert ausschliesslich vom `SendWelcomeMailGraphHandler`
// zur Run-time — niemals beim Payload-Build (das wuerde das Plain-Passwort im
// automation_jobs.payload_json des Folge-Jobs landen lassen).
//
// Bewusst nur ein Lookup-Pfad (per UUID, kein Lookup per workflow_node_instance_id):
// damit gibt es keine Mapping-Source-Implementierung, die den Vault beim Payload-Build
// entschluesseln koennte. Der Konsument muss eine konkrete UUID im Payload haben (kommt
// ueber die `created_ad_user.credentialVaultId`-Verkettung, siehe Sub-Slice 6c).
//
// Migrationspfad-Etappe 9a Schritt 6 Sub-A.
internal interface ITemporaryCredentialRepository
{
    Task<string> ReadAdInitialPasswordByVaultIdAsync(Guid credentialVaultId, CancellationToken cancellationToken = default);
}
