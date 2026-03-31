# OnBoarding – AI-Ready TODO for Production Readiness

## Ziel
Dieses Dokument ist für KI-gestützte Umsetzung gedacht. Es zerlegt die wichtigsten Go-Live-Risiken in konkrete, ausreichend kleine Aufgabenpakete. Fokus: **Produktionsreife**, nicht kosmetische Optimierung.

## Wichtig für die KIs
- Das Projekt ist **fachlich schon stark**, aber **noch nicht produktionsreif**.
- Größte Risiken liegen aktuell in:
  1. Deployment-/Environment-Trennung
  2. Seed-/Demo-Verhalten in Production
  3. Secret-Handling
  4. Frontend Quality Gates (Tests/Lint)
  5. fehlendem Release-/CI-Schutz
- Änderungen sollen **nicht blind umsetzen**, sondern immer auf Auswirkungen auf Dev, Demo und Production prüfen.
- Bevor größere Umbauten passieren, soll die KI vorhandene Dateien, Doku und Compose-/Env-Struktur vollständig lesen.
- Keine Pseudo-Fixes. Wenn Architekturproblem erkannt wird, lieber sauber umstellen statt nur Symptome patchen.

---

## Empfohlene KI-Nutzung

### Codex – bevorzugt für
- konkrete Implementierung
- kleine bis mittlere Refactorings
- Testfixes
- Lint-Fixes
- Compose-/Config-Anpassungen
- CI-Dateien und Skripte

### Claude – bevorzugt für
- Architekturentscheidungen
- Review von Security-/Deployment-Konzepten
- Zerlegung großer Baustellen
- Risikoanalyse vor Refactors
- Review von API-/Frontend-Vertragsgrenzen

### Reasoning-Empfehlung
- **low**: klar lokalisierte, mechanische Änderungen
- **medium**: mehrere Dateien, aber klares Zielbild
- **high**: Refactoring mit Seiteneffekten oder fachlichen Verträgen
- **very high**: Architektur, Security, Deployment, systemweite Änderungen

---

## Reihenfolge
1. **P0 – Produktionsblocker beseitigen**
2. **P1 – Stabilität und Betriebsfähigkeit absichern**
3. **P2 – Architektur- und UX-Härtung**
4. **P3 – Performance und langfristige Wartbarkeit**

---

# P0 – Muss vor Go-Live erledigt werden

## TASK P0.1 – Produktions-Deployment sauber von Dev entkoppeln
**Ziel:** `compose.prod.yml` darf kein umgefärbtes Dev-Setup mehr sein.

**Warum:** Aktuell erbt Production zu viel Dev-Verhalten. Das ist ein echter Go-Live-Blocker.

**Prüfen / betroffene Bereiche:**
- `compose.yml`
- `compose.prod.yml`
- Env-Dateien / Compose-Variablen
- API/Web Build-Args
- DB-Initialisierung

**Erwartetes Ergebnis:**
- Production nutzt eigenes, sauberes Setup
- keine Dev-Volumes oder Demo-Initialisierung in Production
- keine localhost-Redirects in Production
- keine Standard-Credentials in Production
- nur produktionsrelevante Ports / Services / Variablen

**Konkrete Aufgaben:**
1. Analysiere beide Compose-Dateien vollständig und dokumentiere, welche Dev-Annahmen aktuell in Production durchrutschen.
2. Refactore die Compose-Struktur so, dass Production nicht mehr implizit Dev-Verhalten erbt.
3. Trenne DB-Init, Redirect-URIs, Credentials und Host-Port-Exposition sauber nach Umgebung.
4. Prüfe, ob Reverse-Proxy-/Ingress-Annahme sauber unterstützt wird.
5. Dokumentiere exakt, welche Variablen für Production zwingend gesetzt sein müssen.

**Definition of Done:**
- `compose.prod.yml` ist eigenständig produktionsfähig
- keine Demo-/Dev-Initialisierung mehr in Production
- Production-Redirect-URIs zeigen auf echte Ziel-Domain
- keine Default-Secrets/Credentials fest im Compose

**Empfohlene KI:** Claude für Analyse/Plan, danach Codex für Umsetzung
**Reasoning:** very high

---

## TASK P0.2 – Demo-/Seed-Daten technisch aus Production ausschließen
**Ziel:** Production darf unter keinen Umständen Demo- oder Testdaten initialisieren.

**Warum:** Aktuell besteht das Risiko, dass frische Produktionsvolumes Seed-Daten laden.

**Prüfen / betroffene Bereiche:**
- `db/02_seed.sql`
- DB-Init-Mechanismus
- Compose-Mounts
- Produktionsdoku

**Erwartetes Ergebnis:**
- technischer Ausschluss von Seed-Daten in Production
- keine Lösung nur per Kommentar oder Konvention

**Konkrete Aufgaben:**
1. Analysiere den gesamten DB-Init-Pfad.
2. Baue die Init-Strategie so um, dass Schema/Migrationen und optionale Demo-Seeds getrennt sind.
3. Stelle sicher, dass Production nur Schema/Migrationen bekommt, niemals Demo-Content.
4. Ergänze einen kurzen technischen Hinweis in der Doku, wie Test-/Demo-Daten bewusst aktiviert werden können, aber standardmäßig aus bleiben.

**Definition of Done:**
- Seed-Dateien werden in Production technisch nicht geladen
- neue produktive DB startet ohne Demo-Datensätze
- Dev/Demo bleibt weiterhin nutzbar

**Empfohlene KI:** Codex
**Reasoning:** high

---

## TASK P0.3 – Secrets aus unsicherer Persistenz entfernen
**Ziel:** Keine produktionsrelevanten Secrets mehr als Klartext in der Datenbank.

**Warum:** Das dokumentierte `client_secret` in Klartext ist für Produktion nicht akzeptabel.

**Prüfen / betroffene Bereiche:**
- Notification-/Mail-Konfiguration
- DB-Modelle / Tabellen
- API-Konfigurationslogik
- Admin-UI für Mail/Notifications
- Dokumentation zu Entra / Mail

**Erwartetes Ergebnis:**
- Secret-Handling über sichere Runtime-Konfiguration oder Secret Store
- UI darf Secret nicht im Klartext zurückgeben
- Rotation/Änderung bleibt möglich

**Konkrete Aufgaben:**
1. Analysiere, wo Secrets gespeichert, gelesen und angezeigt werden.
2. Entwerfe ein Zielmodell: Secret aus Environment / Secret Store / sicherer Runtime-Konfiguration, nur Metadaten in DB.
3. Refactore Backend und Admin-UI entsprechend.
4. Stelle sicher, dass bestehende Konfigurationen migrierbar sind.
5. Verhindere, dass Secret-Werte in Logs, API-Responses oder Admin-Reads auftauchen.

**Definition of Done:**
- kein Klartext-Secret mehr in DB als produktiver Standard
- Admin-UI zeigt Secret nicht auslesbar an
- App kann Secret sicher aus Runtime lesen
- Doku beschreibt das neue Modell sauber

**Empfohlene KI:** Claude für Zielmodell, Codex für Implementierung
**Reasoning:** very high

---

## TASK P0.4 – Frontend Lint komplett auf grün bringen
**Ziel:** Keine ESLint-Errors mehr im Frontend.

**Warum:** Rotes Lint kurz vor Produktion ist ein Disziplinproblem und verdeckt echte Fehler.

**Prüfen / betroffene Bereiche:**
- gesamtes `web/`
- besonders Hooks, Pages, Admin-/Workflow-Komponenten

**Erwartetes Ergebnis:**
- Lint vollständig grün
- nicht durch Regel-Deaktivierung erschummelt

**Konkrete Aufgaben:**
1. Führe ESLint aus und gruppiere die Fehlerarten.
2. Behebe zuerst echte Architekturwarnzeichen wie `set-state-in-effect`.
3. Behebe danach ungenutzte Variablen, fehlerhafte Imports und sonstige Verstöße.
4. Vermeide globale Abschaltungen, außer wenn fachlich wirklich begründet und eng lokalisiert.

**Definition of Done:**
- `npm run lint` oder entsprechender Lint-Command läuft fehlerfrei
- keine pauschalen Regel-Deaktivierungen als Schnellschuss

**Empfohlene KI:** Codex
**Reasoning:** medium

---

## TASK P0.5 – Frontend Tests wieder stabil auf grün bringen
**Ziel:** Testlandschaft muss wieder zur Implementierung passen.

**Warum:** Aktuell gibt es Refactoring-Drift zwischen Tests und echtem Code.

**Prüfen / betroffene Bereiche:**
- `workflowDetailModel.test.ts`
- `dashboardInsights.test.ts`
- `TaskCommentsSection.test.tsx`
- zugehörige Implementierungen und API-Importpfade

**Erwartetes Ergebnis:**
- Tests prüfen reales Verhalten
- keine kaputten Mocks durch falsche Modulgrenzen
- keine veralteten Vertragserwartungen

**Konkrete Aufgaben:**
1. Analysiere alle aktuell fehlschlagenden Tests und ordne sie in Kategorien ein:
   - veralteter Test
   - kaputte Implementierung
   - falsches Mocking
   - unstabile UI-Verträge
2. Repariere die Export-/Import-Verträge zwischen Test und Implementierung.
3. Richte Mocking an den echten Modulgrenzen aus, nicht an Wunsch-Barrels.
4. Stabilisiere Accessibility-/Status-Verträge dort, wo UI-Feedback erwartet wird.
5. Lass danach alle Frontend-Tests erneut laufen.

**Definition of Done:**
- Frontend-Tests laufen grün
- Tests greifen an echten Importgrenzen
- keine Refactoring-Reste wie erwartete, aber nicht mehr existente Exporte

**Empfohlene KI:** Codex
**Reasoning:** high

---

## TASK P0.6 – Swagger in Production absichern oder deaktivieren
**Ziel:** Swagger darf in Production nicht unkontrolliert offen sein.

**Warum:** Offene API-Dokumentation in Production erhöht Angriffsfläche und passt nicht zu einem harten Release-Standard.

**Prüfen / betroffene Bereiche:**
- API Startup / Middleware
- Environment-Abfragen
- ggf. internes Admin-/Ops-Konzept

**Erwartetes Ergebnis:**
- Swagger nur in Dev/Test oder sauber abgesichert

**Konkrete Aufgaben:**
1. Prüfe, wo Swagger aktiviert wird.
2. Implementiere klares Environment-Gating.
3. Falls Swagger in Production gewünscht ist, nur abgesichert und bewusst.
4. Dokumentiere die Entscheidung.

**Definition of Done:**
- Swagger ist in Production entweder deaktiviert oder sauber geschützt

**Empfohlene KI:** Codex
**Reasoning:** low

---

## TASK P0.7 – Minimale CI/CD-Quality-Gates einführen
**Ziel:** Jeder Release-Kandidat muss reproduzierbar validiert werden.

**Warum:** Ohne Pipeline fallen kaputte Tests/Lint/Builds erst spät auf.

**Prüfen / betroffene Bereiche:**
- Repository Root
- vorhandene Build-/Test-Skripte
- Backend-Test-Commands
- Frontend-Test-/Lint-/Build-Commands

**Erwartetes Ergebnis:**
- automatischer Pipeline-Check für API und Web
- Build, Test, Lint als Pflichtprüfungen

**Konkrete Aufgaben:**
1. Analysiere vorhandene Skripte und notwendigen Commands.
2. Erstelle eine erste CI-Pipeline für:
   - Backend Build
   - Backend Tests
   - Frontend Build
   - Frontend Lint
   - Frontend Tests
3. Stelle sicher, dass Fehler sauber und früh sichtbar sind.
4. Dokumentiere lokale Vorab-Prüfungen für Entwickler.

**Definition of Done:**
- reproduzierbare Pipeline vorhanden
- Projekt kann vor Merge/Release automatisch validiert werden

**Empfohlene KI:** Codex
**Reasoning:** medium

---

# P1 – Kurz nach P0, aber noch vor echtem Rollout dringend empfohlen

## TASK P1.1 – Health-Checks sauber in Liveness / Readiness / Deep Health trennen
**Ziel:** Monitoring soll echte Betriebszustände abbilden, ohne falsche Ausfälle durch externe Abhängigkeiten.

**Warum:** Aktuell scheint `/health` zu stark an externe Microsoft-Erreichbarkeit gekoppelt.

**Konkrete Aufgaben:**
1. Analysiere aktuellen Health-Endpoint.
2. Trenne in sinnvolle Probes:
   - Liveness: Prozess lebt
   - Readiness: App + lokale Abhängigkeiten bereit
   - Deep Health: externe Provider wie Entra/Graph erreichbar
3. Passe Monitoring-/Doku-Hinweise an.

**Definition of Done:**
- externe Störung macht nicht automatisch Liveness/Readiness rot
- klare Probe-Trennung vorhanden

**Empfohlene KI:** Claude für Design, Codex für Implementierung
**Reasoning:** high

---

## TASK P1.2 – CORS-, Auth- und Redirect-Konfiguration pro Umgebung härten
**Ziel:** Umgebungsabhängige Security-Konfigurationen müssen explizit und nachvollziehbar sein.

**Warum:** Dev-lastige Defaults kurz vor Produktion sind riskant.

**Konkrete Aufgaben:**
1. Prüfe CORS-Regeln, Redirect-URIs, Auth-Modes, Demo-Flags und Frontend-Build-Args.
2. Entferne implizite Dev-Annahmen.
3. Stelle sicher, dass Production nur freigegebene Origins und produktive Redirects akzeptiert.
4. Dokumentiere das Konfigurationsmodell kompakt.

**Definition of Done:**
- CORS und Redirects sind pro Umgebung explizit definiert
- Production übernimmt keine lockeren Dev-Defaults

**Empfohlene KI:** Codex
**Reasoning:** high

---

## TASK P1.3 – Handoff-/Release-Artefakte bereinigen
**Ziel:** ZIP-/Release-Artefakte dürfen keine unnötigen Build- oder Repo-Reste enthalten.

**Warum:** Das aktuelle Handoff zeigt Prozessschwächen.

**Konkrete Aufgaben:**
1. Prüfe bestehendes `Prepare-Handoff.ps1` oder ähnliche Skripte.
2. Stelle sicher, dass Artefakte Dinge wie `.git`, `node_modules`, `dist`, lokale Caches usw. ausschließen.
3. Ergänze eine kurze Anleitung für saubere Übergaben.

**Definition of Done:**
- sauberes Handoff-Artefakt reproduzierbar erzeugbar
- keine unnötigen Artefakte im ZIP

**Empfohlene KI:** Codex
**Reasoning:** low

---

## TASK P1.4 – Produktionskonfiguration dokumentieren wie eine Checkliste
**Ziel:** Kein implizites Betriebswissen nur im Kopf.

**Warum:** Kurz vor Go-Live scheitern viele Projekte an verstreutem Konfigurationswissen.

**Konkrete Aufgaben:**
1. Erstelle eine kompakte `PRODUCTION_CHECKLIST.md` oder erweitere bestehende Doku.
2. Dokumentiere:
   - benötigte Env-Variablen
   - Redirect-URIs
   - DB-Initialisierung
   - Secret-Bereitstellung
   - Startreihenfolge
   - Smoke-Tests nach Deployment
3. Halte die Datei kurz, konkret und ausführbar.

**Definition of Done:**
- ein neuer Techniker kann Production-Setup nachvollziehen, ohne raten zu müssen

**Empfohlene KI:** Claude
**Reasoning:** medium

---

# P2 – Architektur- und Stabilitätsverbesserungen

## TASK P2.1 – Frontend-State-Management entschlacken
**Ziel:** Weniger fragile State-Synchronisation, weniger `setState` in Effects, besser vorhersagbares UI-Verhalten.

**Warum:** Wiederkehrende `set-state-in-effect`-Muster deuten auf ein zu fragiles Zustandsmodell hin.

**Konkrete Aufgaben:**
1. Analysiere die betroffenen Komponenten/Hooks systematisch.
2. Ordne pro Fall ein, ob State wirklich abgeleitet, memoized oder server-state-getrieben sein sollte.
3. Refactore kritische Komponenten so, dass unnötige Synchronisations-Effects verschwinden.
4. Achte darauf, UI-Verhalten nicht unbemerkt zu ändern.

**Definition of Done:**
- deutlich weniger problematische Synchronisations-Effects
- sauberere Datenflüsse in zentralen Workflow-/Dashboard-Komponenten

**Empfohlene KI:** Claude für Analyse, Codex für Umsetzung
**Reasoning:** very high

---

## TASK P2.2 – Frontend-Teststrategie an echte Modulgrenzen anpassen
**Ziel:** Tests sollen nicht von zufälligen Barrel-Strukturen abhängen.

**Warum:** Aktuelle Tests brechen teilweise wegen inkonsistenter Import-/Mock-Grenzen.

**Konkrete Aufgaben:**
1. Lege fest, welche Module offiziell öffentliche Verträge darstellen.
2. Passe Tests und ggf. Service-Schicht an diese Grenzen an.
3. Verhindere, dass Tests auf zufällige interne Re-Exports angewiesen sind.

**Definition of Done:**
- stabilere Testarchitektur
- weniger fragile Mocks bei Refactorings

**Empfohlene KI:** Claude für Review, Codex für Refactor
**Reasoning:** high

---

## TASK P2.3 – Manager-/Supervisor-Dashboard serverseitig sauber modellieren
**Ziel:** Sichtbarkeits- und Queue-Logik soll aus dem Backend kommen, nicht im Frontend rekonstruiert werden.

**Warum:** Das Risiko für Berechtigungsdrift ist hoch, wenn Manager-Insights generisch aus Workflows abgeleitet werden.

**Konkrete Aufgaben:**
1. Prüfe aktuelle Dashboard-Datenpfade.
2. Vergleiche generische Workflow-Abfragen mit dedizierten Supervisor-/Queue-Endpoints.
3. Vereinheitliche das Modell so, dass der Client keine Rollenlogik erraten muss.
4. Passe Tests entsprechend an.

**Definition of Done:**
- Dashboard nutzt fachlich korrekte Backend-Quellen
- weniger clientseitige Rekonstruktion von Berechtigungslogik

**Empfohlene KI:** Claude für fachlich-technische Analyse, Codex für Umsetzung
**Reasoning:** very high

---

## TASK P2.4 – Große Frontend-Dateien und Hooks zerlegen
**Ziel:** Reduzierung von Komplexität und Refactoring-Risiko.

**Warum:** Mehrere Dateien sind zu groß und tragen zu Test-/State-Problemen bei.

**Konkrete Aufgaben:**
1. Identifiziere die größten und risikoreichsten Komponenten/Hooks.
2. Teile sie entlang fachlicher Verantwortlichkeiten auf.
3. Vermeide reine kosmetische Extraktion; Ziel sind klarere Grenzen.
4. Passe Tests parallel an.

**Definition of Done:**
- zentrale Problemdateien sind kleiner und klarer getrennt
- Lesbarkeit und Testbarkeit steigen messbar

**Empfohlene KI:** Codex
**Reasoning:** high

---

# P3 – Performance und langfristige Wartbarkeit

## TASK P3.1 – Route-basiertes Code-Splitting im Frontend einführen
**Ziel:** Initialbundle verkleinern und selten genutzte Bereiche lazy laden.

**Warum:** Das Hauptbundle ist für den aktuellen Reifegrad unnötig groß.

**Konkrete Aufgaben:**
1. Analysiere Bundle-Zusammensetzung.
2. Führe `React.lazy` / route-basiertes Splitting für seltene Bereiche ein.
3. Beginne mit Admin-, Graph-, Settings- oder seltenen Detailseiten.
4. Prüfe Ladezustände und Fehlergrenzen.

**Definition of Done:**
- Hauptbundle spürbar reduziert
- große Nebenbereiche werden lazy geladen

**Empfohlene KI:** Codex
**Reasoning:** medium

---

## TASK P3.2 – Repository-/Service-Grenzen im Backend nachschärfen
**Ziel:** Businesslogik nicht zu stark in SQL-nahe Repositories drücken.

**Warum:** Das Backend ist ordentlich, aber einige Repositories sind zu groß und fachlich schwer.

**Konkrete Aufgaben:**
1. Identifiziere größte Repository-Klassen.
2. Prüfe, welche Logik in Domain-/Application-Services gehört.
3. Refactore nur dort, wo echte Lesbarkeit und Testbarkeit steigen.
4. Vermeide unnötigen Umbau kurz vor Betrieb, wenn Risiko größer als Nutzen ist.

**Definition of Done:**
- klarere Verantwortlichkeiten in kritischen Backend-Pfaden
- bessere Testbarkeit komplexer Businesslogik

**Empfohlene KI:** Claude für Planung, Codex für gezielte Umsetzung
**Reasoning:** high

---

## TASK P3.3 – Observability verbessern
**Ziel:** Produktionsfehler schneller erkennen und eingrenzen.

**Warum:** Kurz vor Go-Live reicht Basislogging selten aus.

**Konkrete Aufgaben:**
1. Prüfe aktuelles Logging und Error-Handling.
2. Ergänze strukturierte Logs an kritischen Workflow-, Auth- und Notification-Pfaden.
3. Definiere minimale Betriebsmetriken und sinnvolle Fehlerkontexte.
4. Vermeide Secret-/PII-Leaks in Logs.

**Definition of Done:**
- kritische Produktionspfade sind nachvollziehbarer
- Fehleranalyse wird deutlich leichter

**Empfohlene KI:** Claude für Logging-Strategie, Codex für Implementierung
**Reasoning:** high

---

# Empfohlene Arbeitsweise für die KIs

## Für Claude-Prompts
Verwende Claude primär dann, wenn zuerst **Analyse, Priorisierung oder Architekturentscheidung** nötig ist.

### Gute Claude-Aufgaben
- „Analysiere die aktuelle Compose-/Env-Struktur und entwirf ein sauberes Produktionsmodell.“
- „Bewerte, wie das Secret-Handling ohne Klartext in der DB umgebaut werden sollte.“
- „Prüfe, ob das Dashboard Rollenlogik korrekt aus dem Backend bezieht oder clientseitig rekonstruiert.“
- „Zerlege die betroffenen Frontend-State-Probleme in konkrete Refactor-Pakete.“

## Für Codex-Prompts
Verwende Codex primär für **Umsetzung, Refactoring, Dateianpassungen, Tests und Skripte**.

### Gute Codex-Aufgaben
- „Setze die Compose-Trennung für Production technisch um und dokumentiere alle geänderten Variablen.“
- „Entferne Seed-Initialisierung aus Production, ohne Dev/Demo zu brechen.“
- „Bringe alle Frontend-Lint-Fehler sauber auf grün.“
- „Repariere die fehlschlagenden Frontend-Tests an den echten Modulgrenzen.“
- „Füge eine minimale CI-Pipeline für Build/Test/Lint hinzu.“

---

# Konkrete Reihenfolge für die nächste KI-Arbeit

## Sprint 1
1. P0.1 Produktions-Deployment entkoppeln
2. P0.2 Seed-Daten aus Production ausschließen
3. P0.6 Swagger absichern/deaktivieren
4. P1.2 CORS/Auth/Redirects härten

**Empfohlene KI-Mischung:** Claude für Review, dann Codex für Umsetzung

## Sprint 2
1. P0.3 Secret-Handling umbauen
2. P0.7 CI/CD-Quality-Gates
3. P1.4 Produktions-Checkliste schreiben

**Empfohlene KI-Mischung:** Claude zuerst, dann Codex

## Sprint 3
1. P0.4 Frontend Lint grün
2. P0.5 Frontend Tests grün
3. P2.2 Teststrategie stabilisieren

**Empfohlene KI:** Codex

## Sprint 4
1. P1.1 Health-Checks trennen
2. P2.1 State-Management entschlacken
3. P2.3 Manager-/Supervisor-Dashboard serverseitig härten
4. P2.4 große Dateien/Hooks zerlegen

**Empfohlene KI-Mischung:** Claude für Zerlegung/Review, Codex für Umsetzung

## Sprint 5
1. P3.1 Code-Splitting
2. P3.2 Backend-Service-/Repository-Grenzen
3. P3.3 Observability

---

# Klare Gesamtpriorität

## Sofort
- Deployment / Production-Konfiguration
- Seed-/Demo-Ausschluss
- Secret-Handling
- Tests/Lint/Pipeline

## Danach
- Health / Auth-Härtung / CORS / Dokumentation

## Später
- State-Architektur / Bundle / größere Refactors / Observability

---

# Endzustand, den die KIs anstreben sollen
Nach Abschluss der P0-Aufgaben soll das Projekt nicht „schöner“, sondern **real freigabefähiger** sein:
- Production ist technisch von Dev getrennt
- keine Demo-Daten in Production
- keine produktiven Secrets im Klartext in DB
- Frontend Checks grün
- Release-Validierung automatisiert
- Security- und Betriebsverhalten explizit statt implizit
