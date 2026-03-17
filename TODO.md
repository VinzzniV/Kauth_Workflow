# TODO.md

## 🔴 MUST FIX BEFORE DEMO

### [T5] Alle Demo-User haben gleiche echte Email
- Problem:
Alle Notifications gehen an reale Adresse.

- Warum kritisch:
Demo kann reale Mails triggern.

- Lösungsidee:
Fake Emails (user@demo.local)

- Status:
Not important for now

---

## 🟠 SHOULD FIX

### [T8] DEMO_AUTH_DEFAULT_USER Risiko
- Problem:
Kann alle Requests automatisch authentifizieren.

- Lösung:
Nur für DEV aktivieren oder absichern.

---

## 🟡 TECH DEBT

### [T9.1] Requirements-Logik aus Frontend in Backend verlagern
- Problem:
Requirement-Sichtbarkeit, Reset-Regeln und Validierung liegen noch im Frontend.

- Ziel:
Backend liefert pro Requirement die maßgeblichen Metadaten für Sichtbarkeit, Pflichtstatus und Validierung.

- Betroffene Bereiche:
`web/src/utils/requirements.ts`
`web/src/utils/requirementRules.ts`

---

### [T9.2] Workflow-Detail-Ableitungen backendseitig bereitstellen
- Problem:
Prozessschritte, aktueller Verantwortungsbereich und Statuszusammenfassungen werden noch im Frontend aus Rohdaten abgeleitet.

- Ziel:
Backend liefert workflow-detail-spezifische Summary-Felder als Source of Truth.

- Betroffene Bereiche:
`web/src/components/workflow-detail/workflowDetailModel.ts`

---

### [T9.3] Dashboard-Metriken und Priorisierung ins Backend verlagern
- Problem:
Dashboard berechnet fachliche Kennzahlen und Prioritäten noch selbst aus Workflow- und Taskdaten.

- Ziel:
Backend liefert dashboard-fertige Kennzahlen und priorisierte Listen.

- Betroffene Bereiche:
`web/src/components/dashboard/dashboardInsights.ts`

---

### [T9.4] Frontend-Status-Utilities auf reine Darstellung begrenzen
- Problem:
Frontend nutzt Status-Helfer teils noch für fachliche Interpretation statt nur für Labels und Darstellung.

- Ziel:
Status-Helfer bleiben reine Präsentationsschicht über backend-definierter Logik.

- Betroffene Bereiche:
`web/src/utils/workflowStatus.ts`
`web/src/utils/taskStatus.ts`

---

### [T10] Doppelte formatDate Funktionen
- Refactor in shared util

---

### [T11] Große Dateien (AdminConfigPage, Repository)
- Mittelfristig aufteilen

---

## 🧪 OPTIONAL / FUTURE

### [T12] Admin Config erweitern (Mail Settings UI)
### [T13] DB visuell im Admin anzeigen
### [T14] Vollständig datengetriebenes System (keine Hardcodes mehr)
