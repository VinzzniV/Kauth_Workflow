-- Maßnahmevorlagen für Azubi/Studenten-Abteilungsdurchläufe.
--
-- Abteilungen kommen per Entra-Sync – werden hier NICHT angelegt.
-- Die Template-Inserts greifen automatisch, sobald die jeweilige Abteilung
-- durch den Sync in der departments-Tabelle vorhanden ist (idempotent).
--
-- Erwartete Abteilungsnamen (müssen exakt mit Entra-Sync-Namen übereinstimmen):
--   - "Verkauf"                      → PM / Projektmanagement (Entra-Name; fachlich: Vertrieb)
--   - "Werkzeugbau"                → WZB / Prototyp
--   - "Versand"                   → VS + VP (Verpackerei) + QS-Endkontrolle (kombiniert)
--   - "Einkauf"
--   - "Fertigungssteuerung"          → FST
--
-- JOIN-Pattern: r.system_key (nicht r.responsibility_key) – konsistent mit Migration 55.
--
-- Fix: Migration 57 hat den JOIN auf r.responsibility_key = 'ad' gemacht,
-- obwohl 'ad' der system_key ist. Dadurch haben die Azubi-Vorlagen
-- default_responsibility_id = NULL. Wird hier repariert.

-- =========================
-- Fix: Kaputte Vorlagen aus Migration 57
-- =========================

UPDATE department_action_templates dat
SET default_responsibility_id = r.id
FROM app_responsibilities r
WHERE r.system_key = 'ad'
  AND dat.default_responsibility_id IS NULL
  AND dat.is_active = TRUE
  AND EXISTS (
      SELECT 1 FROM departments d
      WHERE d.id = dat.department_id
        AND LOWER(d.name) IN ('ausbildung technisch', 'ausbildung kaufmaennisch')
  );

-- =========================
-- Maßnahmevorlagen – Verkauf / Projektmanagement (PM)
-- (Entra-Abteilungsname: "Verkauf", fachlich: Vertrieb/PM)
-- =========================

WITH template_seed(department_name, trigger_type, title, description, task_type, responsibility_system_key, due_offset_days, reminder_offset_days) AS (
    VALUES
        ('Verkauf', 'enter',
         'Ordnerzugänge Vertrieb einrichten',
         E'Folgende Netzlaufwerke freischalten:\n' ||
         E'- H:\\Vertrieb\\Projektmanagement\n' ||
         E'- H:\\Entwicklung\\Projekte aktiv\n' ||
         E'- G:\\KauthGroup\\Vertrieb\\04_Projekte\\01_Arbeitsdatei\n' ||
         E'- G:\\KauthGroup\\Vertrieb\\04_Projekte\\02_Werkzeugdoku\n' ||
         E'- G:\\KauthGroup\\Vertrieb\\Beauftragungsblätter',
         'technical', 'ad', -5, 1),

        ('Verkauf', 'enter',
         'Infor LN Zugang einrichten',
         'LN-Benutzerkonto für Vertrieb / Projektmanagement anlegen.',
         'technical', 'ln', -5, 1),

        ('Verkauf', 'enter',
         'PRO.FILE Zugang einrichten',
         'PRO.FILE-Zugang für den Einsatz im Vertrieb anlegen.',
         'technical', 'ad', -5, 1),

        ('Verkauf', 'enter',
         'SpinFire Viewer installieren',
         'SpinFire Viewer für die Einsicht in 3D-Konstruktionsdaten bereitstellen.',
         'technical', 'ad', -5, 1)
)
INSERT INTO department_action_templates (
    department_id, trigger_type, title, description, task_type,
    default_responsibility_id, due_offset_days, reminder_offset_days,
    is_automatable, automation_key, is_active
)
SELECT d.id, s.trigger_type, s.title, s.description, s.task_type,
       r.id, s.due_offset_days, s.reminder_offset_days, FALSE, NULL, TRUE
FROM template_seed s
JOIN departments d ON LOWER(d.name) = LOWER(s.department_name)
LEFT JOIN app_responsibilities r ON r.system_key = s.responsibility_system_key
WHERE NOT EXISTS (
    SELECT 1 FROM department_action_templates e
    WHERE e.department_id = d.id AND e.trigger_type = s.trigger_type AND e.title = s.title
);

-- =========================
-- Maßnahmevorlagen – Werkzeugbau / WZB
-- =========================

WITH template_seed(department_name, trigger_type, title, description, task_type, responsibility_system_key, due_offset_days, reminder_offset_days) AS (
    VALUES
        ('Werkzeugbau', 'enter',
         'Ordnerzugang WZB einrichten',
         E'Netzlaufwerk freischalten:\n- H:\\WZB (alles außer Austausch-Ordner)',
         'technical', 'ad', -5, 1),

        ('Werkzeugbau', 'enter',
         'Infor LN Zugang einrichten',
         'LN-Benutzerkonto für den Einsatz im Werkzeugbau / WZB anlegen.',
         'technical', 'ln', -5, 1),

        ('Werkzeugbau', 'enter',
         'PRO.FILE Zugang einrichten',
         'PRO.FILE-Zugang für Werkzeugdokumentation anlegen.',
         'technical', 'ad', -5, 1),

        ('Werkzeugbau', 'enter',
         'Bambu Studio installieren',
         'Nur für technische Ausbildungen. Achtung: Software muss nach Stationsende wieder deinstalliert werden.',
         'technical', 'ad', -5, 1),

        ('Werkzeugbau', 'exit',
         'Bambu Studio deinstallieren',
         'Bambu Studio nach Stationsende vom Gerät entfernen.',
         'technical', 'ad', 0, 1)
)
INSERT INTO department_action_templates (
    department_id, trigger_type, title, description, task_type,
    default_responsibility_id, due_offset_days, reminder_offset_days,
    is_automatable, automation_key, is_active
)
SELECT d.id, s.trigger_type, s.title, s.description, s.task_type,
       r.id, s.due_offset_days, s.reminder_offset_days, FALSE, NULL, TRUE
FROM template_seed s
JOIN departments d ON LOWER(d.name) = LOWER(s.department_name)
LEFT JOIN app_responsibilities r ON r.system_key = s.responsibility_system_key
WHERE NOT EXISTS (
    SELECT 1 FROM department_action_templates e
    WHERE e.department_id = d.id AND e.trigger_type = s.trigger_type AND e.title = s.title
);

-- =========================
-- Maßnahmevorlagen – Versand (deckt Versand, Verpackerei und QS-Endkontrolle ab)
-- =========================

WITH template_seed(department_name, trigger_type, title, description, task_type, responsibility_system_key, due_offset_days, reminder_offset_days) AS (
    VALUES
        ('Versand', 'enter',
         'Ordnerzugänge Logistik / Verpackerei einrichten',
         E'Folgende Netzlaufwerke freischalten:\n' ||
         E'- H:\\Logistik\\Verpackerei\\05_Verpackungsvorschriften und -katalog\\00_Verpackungsvorschriften Zentraleinkauf\n' ||
         E'- H:\\Logistik\\Verpackerei\\04_Packlisten',
         'technical', 'ad', -5, 1),

        ('Versand', 'enter',
         'Infor LN Zugang einrichten (Versand)',
         'LN-Benutzerkonto für den Einsatz im Versandbereich anlegen.',
         'technical', 'ln', -5, 1),

        ('Versand', 'enter',
         'E-Mail Adresse einrichten',
         'E-Mail-Konto für den Stationszeitraum einrichten.',
         'technical', 'mailbox', -5, 1)
)
INSERT INTO department_action_templates (
    department_id, trigger_type, title, description, task_type,
    default_responsibility_id, due_offset_days, reminder_offset_days,
    is_automatable, automation_key, is_active
)
SELECT d.id, s.trigger_type, s.title, s.description, s.task_type,
       r.id, s.due_offset_days, s.reminder_offset_days, FALSE, NULL, TRUE
FROM template_seed s
JOIN departments d ON LOWER(d.name) = LOWER(s.department_name)
LEFT JOIN app_responsibilities r ON r.system_key = s.responsibility_system_key
WHERE NOT EXISTS (
    SELECT 1 FROM department_action_templates e
    WHERE e.department_id = d.id AND e.trigger_type = s.trigger_type AND e.title = s.title
);

-- =========================
-- Maßnahmevorlagen – Einkauf
-- =========================

WITH template_seed(department_name, trigger_type, title, description, task_type, responsibility_system_key, due_offset_days, reminder_offset_days) AS (
    VALUES
        ('Einkauf', 'enter',
         'Infor LN Zugang einrichten',
         'LN-Benutzerkonto anlegen. Referenzuser für Berechtigungsübernahme: Martin / Bertsche (Rücksprache mit Einkaufsleitung).',
         'technical', 'ln', -5, 1),

        ('Einkauf', 'enter',
         'Ordnerzugang QS Materialspezifikation einrichten',
         E'Netzlaufwerk freischalten:\n- H:\\QS\\Materialspezifikation\\3 freigegeben',
         'technical', 'ad', -5, 1),

        ('Einkauf', 'enter',
         'HABEL Zugang einrichten',
         'HABEL-Benutzerkonto für Einkauf und HABEL Recherche anlegen.',
         'technical', 'habel', -5, 1),

        ('Einkauf', 'enter',
         'ConSense Zugang einrichten',
         'ConSense-Zugang für den Einsatz im Einkauf einrichten.',
         'technical', 'consense', -5, 1),

        ('Einkauf', 'enter',
         'PRO.FILE Zugang einrichten',
         'Ggfs. erforderlich – Rücksprache mit Einkaufsleitung vor Einrichtung.',
         'technical', 'ad', -5, 1),

        ('Einkauf', 'enter',
         'Outlook einrichten',
         'E-Mail-Konto (Outlook) für den Stationszeitraum im Einkauf einrichten.',
         'technical', 'mailbox', -5, 1)
)
INSERT INTO department_action_templates (
    department_id, trigger_type, title, description, task_type,
    default_responsibility_id, due_offset_days, reminder_offset_days,
    is_automatable, automation_key, is_active
)
SELECT d.id, s.trigger_type, s.title, s.description, s.task_type,
       r.id, s.due_offset_days, s.reminder_offset_days, FALSE, NULL, TRUE
FROM template_seed s
JOIN departments d ON LOWER(d.name) = LOWER(s.department_name)
LEFT JOIN app_responsibilities r ON r.system_key = s.responsibility_system_key
WHERE NOT EXISTS (
    SELECT 1 FROM department_action_templates e
    WHERE e.department_id = d.id AND e.trigger_type = s.trigger_type AND e.title = s.title
);

-- =========================
-- Maßnahmevorlagen – Fertigungssteuerung (FST)
-- =========================

WITH template_seed(department_name, trigger_type, title, description, task_type, responsibility_system_key, due_offset_days, reminder_offset_days) AS (
    VALUES
        ('Fertigungssteuerung', 'enter',
         'Ordnerzugänge Fertigungssteuerung einrichten',
         E'Folgende Netzlaufwerke freischalten:\n' ||
         E'- H:\\Logistik\\Fertigungssteuerung (kompletter Ordner)\n' ||
         E'- U:\\User\\PROD\\000 Produktion - Neue Ordnerstruktur\\Gemeinsamer Arbeitsordner\\10 Planung FST\\20 Produktionslisten\n' ||
         E'- U:\\User\\PROD\\000 Produktion - Neue Ordnerstruktur\\ProdW- Werkzeugwartung\\60 Arbeitsvorbereitung\\20 Planung Pressenbereich\n' ||
         E'- H:\\Logistik\\Wareneingang\\03_Lager\\03_Rohmaterial\\00_Rohmaterialverfolgung',
         'technical', 'ad', -5, 1),

        ('Fertigungssteuerung', 'enter',
         'Ultra VNC Viewer einrichten',
         'Ultra VNC Viewer für Fernzugriff im Produktionsbereich installieren und konfigurieren.',
         'technical', 'ad', -5, 1)
)
INSERT INTO department_action_templates (
    department_id, trigger_type, title, description, task_type,
    default_responsibility_id, due_offset_days, reminder_offset_days,
    is_automatable, automation_key, is_active
)
SELECT d.id, s.trigger_type, s.title, s.description, s.task_type,
       r.id, s.due_offset_days, s.reminder_offset_days, FALSE, NULL, TRUE
FROM template_seed s
JOIN departments d ON LOWER(d.name) = LOWER(s.department_name)
LEFT JOIN app_responsibilities r ON r.system_key = s.responsibility_system_key
WHERE NOT EXISTS (
    SELECT 1 FROM department_action_templates e
    WHERE e.department_id = d.id AND e.trigger_type = s.trigger_type AND e.title = s.title
);
