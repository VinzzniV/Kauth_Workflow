# db/manual/ — Inplace-Migrationen fuer bestehende DBs

## Zweck

Solange das System nicht produktiv laeuft, wird das Schema in `db/01_schema.sql` direkt gepflegt — frische DBs (Dev-Container, Test-Container) bekommen dadurch automatisch das aktuelle Schema. Bestehende, befuellte DBs (Dev-Server, lokale Volumes, produktionsnahe Testumgebungen) bekommen das **nicht** automatisch und brauchen einen einmaligen Inplace-Patch.

Die `db/manual/*.sql`-Dateien sind genau das: idempotente, manuell auszufuehrende SQL-Helfer, die eine bestehende DB auf den Stand von `db/01_schema.sql` ziehen.

## Konvention

- Dateiname: `YYYY-MM-DD_<kurzname>.sql`
- Idempotent schreiben (`ADD COLUMN IF NOT EXISTS`, `RENAME` nur, wenn die alte Form noch existiert).
- Header mit Verwendungshinweis (`psql "$DATABASE_URL" -f db/manual/<datei>.sql`).
- **Pflicht**: Eintrag in `manifest.json` mit `expect_in_schema` (Marker, die nach der Migration in `db/01_schema.sql` stehen muessen) und `forbid_in_schema` (Marker, die nach einer Umbenennung/Loeschung dort nicht mehr stehen duerfen).
- Reine **Daten-Migrationen** (z. B. Seed-Inserts, Backfill-Updates) markiert man im Manifest mit `"kind": "data"` und laesst beide Marker-Listen leer. Der Paritaets-Test prueft fuer Daten-Eintraege nur Existenz + Beschreibung.

## Paritaets-Check

`api/API.Tests/SchemaParityTests.cs` prueft im normalen Testlauf:

1. Jede `*.sql`-Datei in diesem Verzeichnis (ausser `manifest.json`/`README.md`) hat genau einen Manifest-Eintrag.
2. Jeder Manifest-Eintrag verweist auf eine existierende Datei.
3. Fuer jeden Eintrag stehen alle `expect_in_schema`-Substrings tatsaechlich in `db/01_schema.sql`.
4. Fuer jeden Eintrag steht keiner der `forbid_in_schema`-Substrings in `db/01_schema.sql`.

Damit wird Drift zwischen kanonischer Schema-Datei und manuellen Inplace-Helfern frueh sichtbar — nicht erst beim Laufzeitfehler in einer bestehenden DB.

## Workflow fuer neue Schema-Aenderungen

1. `db/01_schema.sql` aktualisieren (additiv oder umbenennend).
2. Falls bestehende DBs nachgezogen werden muessen: `db/manual/<datum>_<kurzname>.sql` anlegen (idempotent).
3. `db/manual/manifest.json` ergaenzen.
4. `dotnet test --filter "FullyQualifiedName~SchemaParityTests"` laufen lassen.
5. Falls relevant: Hinweis in `KauthWorkflow/Betrieb/Setup.md` § „Bestehende Datenbanken bei Schema-Renames" ergaenzen.
