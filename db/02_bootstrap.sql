-- ============================================================================
-- 02_bootstrap.sql
--
-- Konsolidierte Produktions-Seed-Daten (Process Types, System Responsibilities,
-- Action Definitions, Notification Templates, Default Department-Konfiguration ...).
-- Wird im prod-Init nach 01_schema.sql ausgefuehrt.
--
-- Erzeugt aus pg_dump --data-only nach Lauf aller Migrationen.
-- Originale Bootstrap-/Seed-Dateien siehe db/_archive/.
-- ============================================================================

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Data for Name: action_definitions; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.action_definitions OVERRIDING SYSTEM VALUE VALUES
	(1, 'CreateAdUser', 'Create AD User', 'Simulated creation of an Active Directory user.', 'simulated_directory', '{"type": "object", "additionalProperties": true}', true, false, true, '2026-04-27 07:05:50.927528+00', '2026-04-27 07:05:51.998735+00'),
	(2, 'CreateMailbox', 'Create Mailbox', 'Simulated provisioning of a mailbox.', 'simulated_mailbox', '{"type": "object", "additionalProperties": true}', true, false, true, '2026-04-27 07:05:50.927528+00', '2026-04-27 07:05:51.998735+00'),
	(3, 'AssignGroups', 'Assign Groups', 'Simulated assignment of directory groups.', 'simulated_directory_groups', '{"type": "object", "additionalProperties": true}', true, false, true, '2026-04-27 07:05:50.927528+00', '2026-04-27 07:05:51.998735+00'),
	(4, 'CreateErpEmployee', 'Create ERP Employee', 'Simulated creation of an ERP employee.', 'simulated_erp', '{"type": "object", "additionalProperties": true}', true, false, false, '2026-04-27 07:05:50.927528+00', '2026-04-27 07:05:51.998735+00'),
	(5, 'SendWelcomeMail', 'Send Welcome Mail', 'Simulated sending of a welcome email.', 'simulated_notification', '{"type": "object", "additionalProperties": true}', true, false, true, '2026-04-27 07:05:50.927528+00', '2026-04-27 07:05:51.998735+00');


--
-- Data for Name: app_groups; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: departments; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.departments OVERRIDING SYSTEM VALUE VALUES
	(1, 'Ausbildung kaufmaennisch'),
	(2, 'Ausbildung technisch');


--
-- Data for Name: app_responsibilities; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.app_responsibilities OVERRIDING SYSTEM VALUE VALUES
	(37, 2, 'ausbildungsleitung_technisch', NULL, 'Ausbildungsleitung Technik', 'department_lead', 'Stabile Leitung fuer technische Ausbildung und Studenten. Immer dieselbe Ansprechperson.', true, '2026-04-27 07:05:52.103982+00'),
	(8, NULL, 'it_hardware', 'hardware', 'Hardware', 'application', 'Verantwortung fuer Hardware-Bereitstellung und Einrichtung.', true, '2026-04-27 07:05:51.230709+00'),
	(9, NULL, 'it_ln', 'ln', 'LN', 'application', 'Verantwortung fuer LN-Zugaenge.', true, '2026-04-27 07:05:51.230709+00'),
	(10, NULL, 'it_habel', 'habel', 'Habel', 'application', 'Verantwortung fuer Habel-Zugaenge.', true, '2026-04-27 07:05:51.230709+00'),
	(11, NULL, 'it_mailbox', 'mailbox', 'Mailbox', 'application', 'Verantwortung fuer Mailbox-Einrichtung.', true, '2026-04-27 07:05:51.230709+00'),
	(12, NULL, 'it_ad', 'ad', 'AD', 'application', 'Verantwortung fuer AD-Konto und zentrale Berechtigungen.', true, '2026-04-27 07:05:51.230709+00'),
	(13, NULL, 'leadership_it', NULL, 'Abteilungsleitung IT', 'department_lead', 'Fuehrungsverantwortung fuer Onboardings der IT.', true, '2026-04-27 07:05:51.230709+00'),
	(18, NULL, 'leadership_sales', NULL, 'Abteilungsleitung Vertrieb', 'department_lead', 'Fuehrungsverantwortung fuer Onboardings des Vertriebs.', true, '2026-04-27 07:05:51.230709+00'),
	(7, NULL, 'leadership_prototype', NULL, 'Abteilungsleitung Prototypenbau', 'department_lead', 'Fuehrungsverantwortung fuer Onboardings im Prototypenbau.', true, '2026-04-27 07:05:51.230709+00'),
	(1, NULL, 'qmb_consense', 'consense', 'Consense', 'application', 'Verantwortung fuer Consense im QMB.', true, '2026-04-27 07:05:51.230709+00'),
	(2, NULL, 'leadership_qmb', NULL, 'Abteilungsleitung QMB', 'department_lead', 'Fuehrungsverantwortung fuer Onboardings des QMB.', true, '2026-04-27 07:05:51.230709+00'),
	(14, NULL, 'leadership_hr', NULL, 'Abteilungsleitung HR', 'department_lead', 'Fuehrungsverantwortung fuer Onboardings der HR.', true, '2026-04-27 07:05:51.230709+00'),
	(15, NULL, 'hr_onboarding', NULL, 'HR-Onboarding', 'process', 'Verantwortung fuer Start, Abstimmung und Begleitung des Onboardings.', true, '2026-04-27 07:05:51.230709+00'),
	(3, NULL, 'av_provis', 'provis', 'Provis', 'application', 'Verantwortung fuer Provis in der AV.', true, '2026-04-27 07:05:51.230709+00'),
	(4, NULL, 'av_gewatec', 'gewatec', 'Gewatec', 'application', 'Verantwortung fuer Gewatec in der AV.', true, '2026-04-27 07:05:51.230709+00'),
	(5, NULL, 'leadership_av', NULL, 'Abteilungsleitung AV', 'department_lead', 'Fuehrungsverantwortung fuer Onboardings der AV.', true, '2026-04-27 07:05:51.230709+00'),
	(16, NULL, 'qs_babtec', 'babtec', 'Babtec', 'application', 'Verantwortung fuer Babtec in der QS.', true, '2026-04-27 07:05:51.230709+00'),
	(17, NULL, 'leadership_qs', NULL, 'Abteilungsleitung QS', 'department_lead', 'Fuehrungsverantwortung fuer Onboardings der QS.', true, '2026-04-27 07:05:51.230709+00'),
	(6, NULL, 'leadership_production', NULL, 'Abteilungsleitung Produktion', 'department_lead', 'Fuehrungsverantwortung fuer Onboardings der Produktion.', true, '2026-04-27 07:05:51.230709+00');


--
-- Data for Name: app_group_responsibilities; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: app_roles; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.app_roles OVERRIDING SYSTEM VALUE VALUES
	(1, NULL, 'auth_reader', 'Leser', 'system', true, '2026-04-27 07:05:51.226844+00'),
	(2, NULL, 'auth_admin', 'Admin', 'system', true, '2026-04-27 07:05:51.226844+00'),
	(3, NULL, 'auth_worker', 'Bearbeiter', 'system', true, '2026-04-27 07:05:51.226844+00'),
	(4, NULL, 'auth_manager', 'Abteilungsleitung', 'system', true, '2026-04-27 07:05:51.226844+00'),
	(5, NULL, 'auth_hr', 'HR', 'system', true, '2026-04-27 07:05:51.226844+00');


--
-- Data for Name: app_group_roles; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: app_permissions; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.app_permissions OVERRIDING SYSTEM VALUE VALUES
	(1, 'app.access', 'App-Zugang', 'Erlaubt die Nutzung der Anwendung.', 'global', 'general', true, '2026-04-27 07:05:51.902457+00'),
	(2, 'users.view_department', 'Benutzer der eigenen Abteilung sehen', 'Darf Benutzer der freigegebenen Abteilungen sehen.', 'department', 'users', true, '2026-04-27 07:05:51.902457+00'),
	(3, 'users.view_all_departments', 'Alle Benutzer sehen', 'Darf Benutzer aller Abteilungen sehen.', 'global', 'users', true, '2026-04-27 07:05:51.902457+00'),
	(4, 'workflows.view_department', 'Workflows der eigenen Abteilung sehen', 'Darf Workflows der freigegebenen Abteilungen sehen.', 'department', 'workflows', true, '2026-04-27 07:05:51.902457+00'),
	(5, 'workflows.view_all', 'Alle Workflows sehen', 'Darf Workflows aller Abteilungen sehen.', 'global', 'workflows', true, '2026-04-27 07:05:51.902457+00'),
	(6, 'workflows.create.onboarding', 'Onboarding starten', 'Darf Onboarding-Vorgaenge starten.', 'department', 'workflows', true, '2026-04-27 07:05:51.902457+00'),
	(7, 'workflows.create.offboarding', 'Offboarding starten', 'Darf Offboarding-Vorgaenge starten.', 'department', 'workflows', true, '2026-04-27 07:05:51.902457+00'),
	(8, 'workflows.create.department_change', 'Abteilungswechsel starten', 'Darf Abteilungswechsel-Vorgaenge starten.', 'department', 'workflows', true, '2026-04-27 07:05:51.902457+00'),
	(9, 'workflows.create.position_change', 'Positionswechsel starten', 'Darf Positionswechsel-Vorgaenge starten.', 'department', 'workflows', true, '2026-04-27 07:05:51.902457+00'),
	(10, 'workflows.create.role_change', 'Rollenwechsel starten', 'Darf Rollenwechsel-Vorgaenge starten.', 'department', 'workflows', true, '2026-04-27 07:05:51.902457+00'),
	(11, 'workflows.create.name_change', 'Namensaenderung starten', 'Darf Namensaenderungen starten.', 'department', 'workflows', true, '2026-04-27 07:05:51.902457+00'),
	(12, 'tasks.execute.supervisor', 'Supervisor-Aufgaben bearbeiten', 'Darf Supervisor-Schritte der freigegebenen Abteilungen bearbeiten.', 'department', 'tasks', true, '2026-04-27 07:05:51.902457+00'),
	(13, 'tasks.execute.department', 'Fachbereichsaufgaben bearbeiten', 'Darf Fachbereichsaufgaben der freigegebenen Abteilungen bearbeiten.', 'department', 'tasks', true, '2026-04-27 07:05:51.902457+00'),
	(14, 'tasks.assign.override', 'Aufgaben umverteilen', 'Darf Aufgaben administrativ umverteilen.', 'global', 'tasks', true, '2026-04-27 07:05:51.902457+00'),
	(15, 'admin.directory.manage', 'Verzeichnisverwaltung', 'Darf Entra-Sync und Gruppen-Mappings pflegen.', 'global', 'admin', true, '2026-04-27 07:05:51.902457+00'),
	(16, 'admin.permissions.manage', 'Berechtigungen verwalten', 'Darf Rollen-Bundles und Benutzer-Overrides pflegen.', 'global', 'admin', true, '2026-04-27 07:05:51.902457+00');


--
-- Data for Name: process_types; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.process_types OVERRIDING SYSTEM VALUE VALUES
	(1, 'onboarding', 'Onboarding', 'Start eines neuen Mitarbeiters mit Aufgaben fuer HR, Fuehrungskraft und Fachbereiche.', true, 'supervisor_fills_document', false, 'identitat', false, true, 10, '2026-04-27 07:05:51.224854+00'),
	(3, 'offboarding', 'Offboarding', 'Geordneter Abschluss eines Mitarbeiterverhältnisses mit Rückgabe aller Zugänge und Ausstattung.', false, NULL, true, 'identitat', false, true, 20, '2026-04-27 07:05:51.458591+00'),
	(4, 'department_change', 'Abteilungswechsel', 'Koordinierter Wechsel eines Mitarbeiters in eine andere Abteilung mit Anpassung aller Zugänge und Ausstattung.', false, NULL, true, 'identitat', true, true, 30, '2026-04-27 07:05:51.478815+00'),
	(5, 'name_change', 'Namensaenderung', 'Koordinierte Aktualisierung eines Mitarbeiternamens in Stammdaten, Verzeichnisdiensten und Kommunikationssystemen.', false, NULL, true, 'identitat', true, true, 40, '2026-04-27 07:05:51.494337+00'),
	(6, 'position_change', 'Positionswechsel', 'Koordinierter Wechsel eines Mitarbeiters in eine neue Position mit Anpassung von Berechtigungen, Systemzugaengen und Schulungen.', false, NULL, true, 'identitat', true, true, 50, '2026-04-27 07:05:51.565471+00'),
	(7, 'role_change', 'Rollenwechsel', 'Koordinierte Anpassung einer Mitarbeiterrolle mit gezielter Aktualisierung von Rollen- und Berechtigungszuweisungen.', false, NULL, true, 'identitat', true, true, 60, '2026-04-27 07:05:51.581978+00');


--
-- Data for Name: workflow_answer_definitions; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.workflow_answer_definitions OVERRIDING SYSTEM VALUE VALUES
	(1, 1, 'ad_user_requested', 'AD-Konto', 'Zugänge', 'Soll für die neue Person ein AD-Konto eingerichtet werden?', 'ad_user', 'boolean', true, 1, true),
	(2, 1, 'comparison_user_available', 'Vergleichsuser vorhanden?', 'Zugänge', 'Gibt es eine Vergleichsperson für die Übernahme der AD-Berechtigungen?', 'berechtigungen', 'boolean', false, 2, true),
	(3, 1, 'comparison_user_name', 'Referenzuser', 'Zugänge', 'Welcher Referenzuser soll für die Übernahme der AD-Berechtigungen verwendet werden?', 'berechtigungen', 'text', false, 3, true),
	(4, 1, 'mailbox_requested', 'Mailbox', 'Zugänge', 'Soll optional eine Mailbox für die neue Person eingerichtet werden?', 'mailbox', 'boolean', false, 4, true),
	(5, 1, 'internet_requested', 'Internetzugang', 'Zugänge', 'Wird für die neue Person ein Internetzugang benötigt?', 'internetzugang', 'boolean', false, 5, true),
	(6, 1, 'microsoft_office_requested', 'Microsoft Office', 'Programme und Systeme', 'Soll Microsoft Office für die neue Person bereitgestellt werden?', 'microsoft_office', 'boolean', false, 6, true),
	(7, 1, 'habel_user_requested', 'Habel', 'Programme und Systeme', 'Soll ein Habel-User für die neue Person angelegt werden?', 'habel', 'boolean', false, 7, true),
	(8, 1, 'ln_user_requested', 'InforLN', 'Programme und Systeme', 'Soll ein InforLN-User für die neue Person angelegt werden?', 'inforln', 'boolean', false, 8, true),
	(9, 1, 'hardware_requested', 'Hardware benötigt?', 'Ausstattung', 'Wird für die neue Person überhaupt Hardware benötigt?', 'pc', 'boolean', true, 9, true),
	(10, 1, 'hardware_available', 'Hardware vorhanden?', 'Ausstattung', 'Ist für die neue Person bereits passende Hardware vorhanden?', 'pc', 'boolean', false, 10, true),
	(12, 1, 'phone_requested', 'Tragbares Telefon', 'Ausstattung', 'Wird für die neue Person ein tragbares Telefon benötigt?', 'phone', 'boolean', false, 13, true),
	(13, 1, 'hardware_type', 'Hardware', 'Ausstattung', 'Welche Hardware soll bereitgestellt werden?', 'pc', 'select', false, 11, true),
	(14, 1, 'laptop_vpn_type', 'Laptop', 'Ausstattung', 'Soll der Laptop mit VPN oder ohne VPN bereitgestellt werden?', 'vpn', 'select', false, 12, true),
	(15, 1, 'laptop_with_vpn_requested', 'Laptop mit VPN', 'Ausstattung', 'Legacy-Feld für bisherige Laptop-Auswahl mit VPN.', 'vpn', 'boolean', false, 111, false),
	(16, 1, 'laptop_without_vpn_requested', 'Laptop ohne VPN', 'Ausstattung', 'Legacy-Feld für bisherige Laptop-Auswahl ohne VPN.', 'laptop', 'boolean', false, 112, false),
	(17, 1, 'desktop_pc_requested', 'Rechner fest', 'Ausstattung', 'Legacy-Feld für bisherige Auswahl eines festen Rechners.', 'pc', 'boolean', false, 113, false),
	(18, 1, 'babtec_requested', 'Babtec', 'Programme und Systeme', 'Soll ein User in Babtec für die neue Person angelegt werden?', 'babtec', 'boolean', false, 14, true),
	(19, 1, 'catia_requested', 'Catia', 'Programme und Systeme', 'Soll Catia für die neue Person bereitgestellt werden?', 'catia', 'boolean', false, 15, true),
	(20, 1, 'datev_requested', 'DATEV', 'Programme und Systeme', 'Soll DATEV für die neue Person bereitgestellt werden?', 'datev', 'boolean', false, 16, true),
	(21, 1, 'tiso_requested', 'Tisoware', 'Programme und Systeme', 'Soll Tisoware für die neue Person bereitgestellt werden?', 'tiso', 'boolean', false, 17, true),
	(22, 1, 'gewatec_requested', 'Gewatec', 'Programme und Systeme', 'Soll ein Gewatec-User für die neue Person angelegt werden?', 'gewatec', 'boolean', false, 18, true),
	(23, 1, 'provis_requested', 'Provis', 'Programme und Systeme', 'Soll ein Provis-User für die neue Person angelegt werden?', 'provis', 'boolean', false, 19, true),
	(25, 1, 'internal_drive_access_requested', 'Zugangsrechte internes Laufwerk', 'Zugangsrechte', 'Sollen Zugangsrechte für ein internes Laufwerk vergeben werden?', 'berechtigungen', 'boolean', false, 21, true),
	(26, 1, 'internal_drive_access_roles', 'Funktion für Laufwerksrechte', 'Zugangsrechte', 'Welche Funktion soll für die Laufwerksrechte berücksichtigt werden?', 'berechtigungen', 'multi_select', false, 22, true),
	(27, 1, 'special_notes', 'Besondere Hinweise', 'Dokumentation', 'Freitext für wichtige Hinweise im Onboarding.', 'identitat', 'text', false, 92, false),
	(28, 3, 'ob_has_ad_account', 'AD-Konto vorhanden?', 'Zugänge', 'Hat die Person ein aktives AD-Konto, das deaktiviert werden muss?', 'ad_user', 'boolean', true, 1, true),
	(29, 3, 'ob_has_mailbox', 'Mailbox vorhanden?', 'Zugänge', 'Hat die Person eine Mailbox, die deaktiviert werden muss?', 'mailbox', 'boolean', false, 2, true),
	(30, 3, 'ob_has_hardware', 'Hardware zurückzugeben?', 'Ausstattung', 'Hat die Person Hardware (Laptop, Workstation, etc.), die eingezogen werden muss?', 'pc', 'boolean', true, 3, true),
	(31, 3, 'ob_has_phone', 'Telefon zurückzugeben?', 'Ausstattung', 'Hat die Person ein tragbares Telefon, das eingezogen werden muss?', 'phone', 'boolean', false, 4, true),
	(32, 3, 'ob_has_habel', 'Habel-Zugang vorhanden?', 'Programme und Systeme', 'Hat die Person einen aktiven Habel-User?', 'habel', 'boolean', false, 5, true),
	(33, 3, 'ob_has_ln', 'InforLN-Zugang vorhanden?', 'Programme und Systeme', 'Hat die Person einen aktiven InforLN-User?', 'inforln', 'boolean', false, 6, true),
	(34, 3, 'ob_has_babtec', 'Babtec-Zugang vorhanden?', 'Programme und Systeme', 'Hat die Person einen aktiven Babtec-User?', 'babtec', 'boolean', false, 7, true),
	(35, 3, 'ob_has_gewatec', 'Gewatec-Zugang vorhanden?', 'Programme und Systeme', 'Hat die Person einen aktiven Gewatec-User?', 'gewatec', 'boolean', false, 8, true),
	(36, 3, 'ob_has_provis', 'Provis-Zugang vorhanden?', 'Programme und Systeme', 'Hat die Person einen aktiven Provis-User?', 'berechtigungen', 'boolean', false, 9, true),
	(38, 3, 'ob_exit_interview', 'Austrittsgespräch führen?', 'Abschluss', 'Soll ein Austrittsgespräch mit der ausscheidenden Person geführt werden?', 'identitat', 'boolean', true, 11, true),
	(39, 3, 'ob_knowledge_transfer', 'Wissenstransfer notwendig?', 'Abschluss', 'Muss vor dem Austritt ein strukturierter Wissenstransfer stattfinden?', 'identitat', 'boolean', false, 12, true),
	(40, 4, 'dc_new_department', 'Neue Abteilung', 'Wechseldetails', 'Name der Zielabteilung, in die der Mitarbeiter wechselt.', 'identitat', 'text', true, 1, true),
	(41, 4, 'dc_change_date', 'Wechseldatum', 'Wechseldetails', 'Geplanter Termin des Abteilungswechsels (z. B. 2025-07-01).', 'identitat', 'text', true, 2, true),
	(42, 4, 'dc_ad_group_change', 'AD-Gruppen anpassen?', 'Zugänge', 'Müssen AD-Gruppen und Berechtigungen an die neue Abteilung angepasst werden?', 'ad_user', 'boolean', true, 3, true),
	(43, 4, 'dc_drive_access_change', 'Laufwerk-Zugänge anpassen?', 'Zugänge', 'Müssen Netzlaufwerk-Zugriffsrechte für die neue Abteilung geändert werden?', 'pc', 'boolean', true, 4, true),
	(44, 4, 'dc_email_alias_change', 'E-Mail Alias anpassen?', 'Zugänge', 'Muss der E-Mail Alias wegen Abteilungsbezug im Mailnamen geändert werden?', 'mailbox', 'boolean', false, 5, true),
	(45, 4, 'dc_hardware_change', 'Hardware-Tausch notwendig?', 'Ausstattung', 'Muss die Hardware (z. B. stationär ↔ mobil) aufgrund der neuen Abteilung getauscht werden?', 'pc', 'boolean', false, 6, true),
	(37, 3, 'ob_has_consense', 'Consense-Zugang vorhanden?', 'Programme und Systeme', 'Hat die Person einen aktiven Consense-User?', 'consense', 'boolean', false, 10, true),
	(11, 1, 'hardware_takeover_details', 'Zu übernehmende Hardware', 'Ausstattung', 'Welche vorhandene Hardware wird übernommen? Bitte z. B. Rechnernummer, Asset-ID oder kurzen Hinweis angeben.', 'pc', 'text', false, 10, true),
	(46, 4, 'dc_has_habel', 'Habel-Zugang anpassen?', 'Programme und Systeme', 'Muss der Habel-Zugang für die neue Abteilung angepasst oder neu eingerichtet werden?', 'habel', 'boolean', false, 7, true),
	(47, 4, 'dc_has_ln', 'InforLN-Zugang anpassen?', 'Programme und Systeme', 'Muss der InforLN-Zugang für die neue Abteilung angepasst oder neu eingerichtet werden?', 'inforln', 'boolean', false, 8, true),
	(48, 4, 'dc_has_babtec', 'Babtec-Zugang anpassen?', 'Programme und Systeme', 'Muss der Babtec-Zugang für die neue Abteilung angepasst oder neu eingerichtet werden?', 'babtec', 'boolean', false, 9, true),
	(49, 4, 'dc_has_gewatec', 'Gewatec-Zugang anpassen?', 'Programme und Systeme', 'Muss der Gewatec-Zugang für die neue Abteilung angepasst oder neu eingerichtet werden?', 'gewatec', 'boolean', false, 10, true),
	(50, 4, 'dc_has_provis', 'Provis-Zugang anpassen?', 'Programme und Systeme', 'Muss der Provis-Zugang für die neue Abteilung angepasst oder neu eingerichtet werden?', 'berechtigungen', 'boolean', false, 11, true),
	(52, 5, 'nc_new_first_name', 'Neuer Vorname', 'Namensaenderung', 'Neuer gueltiger Vorname der betroffenen Person.', 'identitat', 'text', true, 1, true);
INSERT INTO public.workflow_answer_definitions OVERRIDING SYSTEM VALUE VALUES
	(53, 5, 'nc_new_last_name', 'Neuer Nachname', 'Namensaenderung', 'Neuer gueltiger Nachname der betroffenen Person.', 'identitat', 'text', true, 2, true),
	(54, 5, 'nc_effective_date', 'Wirksamkeitsdatum', 'Namensaenderung', 'Datum, ab dem der neue Name in allen Systemen gelten soll.', 'identitat', 'text', true, 3, true),
	(55, 6, 'pc_new_position', 'Neue Position / Rolle', 'Wechseldetails', 'Neue Position oder Rolle, die die Person kuenftig ausueben soll.', 'identitat', 'text', true, 1, true),
	(56, 6, 'pc_change_date', 'Wechseldatum', 'Wechseldetails', 'Datum, ab dem die neue Position wirksam wird.', 'identitat', 'text', true, 2, true),
	(57, 6, 'pc_permission_change', 'Berechtigungen anpassen?', 'Berechtigungen', 'Muessen allgemeine Berechtigungen und Zugriffsprofile wegen der neuen Position angepasst werden?', 'ad_user', 'boolean', true, 3, true),
	(58, 6, 'pc_training_required', 'Neue Schulungen erforderlich?', 'Qualifizierung', 'Sind fuer die neue Position neue Schulungen oder Einweisungen notwendig?', 'identitat', 'boolean', false, 4, true),
	(59, 6, 'pc_ad_groups_change', 'AD-Gruppen anpassen?', 'Zugaenge', 'Muessen AD-Gruppen und Rollen fuer die neue Position geaendert werden?', 'ad_user', 'boolean', false, 5, true),
	(60, 6, 'pc_drive_access_change', 'Laufwerk-Zugaenge anpassen?', 'Zugaenge', 'Muessen Laufwerks- und Datei-Zugriffe an die neue Position angepasst werden?', 'pc', 'boolean', false, 6, true),
	(61, 6, 'pc_mail_alias_change', 'Mailbox oder Alias anpassen?', 'Zugaenge', 'Muessen Mailbox-bezogene Sichtbarkeit oder Aliasdaten geaendert werden?', 'mailbox', 'boolean', false, 7, true),
	(62, 6, 'pc_has_habel', 'Habel-Zugang anpassen?', 'Programme und Systeme', 'Muessen Habel-Berechtigungen wegen der neuen Position angepasst werden?', 'habel', 'boolean', false, 8, true),
	(63, 6, 'pc_has_ln', 'InforLN-Zugang anpassen?', 'Programme und Systeme', 'Muessen InforLN-Berechtigungen wegen der neuen Position angepasst werden?', 'inforln', 'boolean', false, 9, true),
	(64, 6, 'pc_has_babtec', 'Babtec-Zugang anpassen?', 'Programme und Systeme', 'Muessen Babtec-Berechtigungen wegen der neuen Position angepasst werden?', 'babtec', 'boolean', false, 10, true),
	(65, 6, 'pc_has_gewatec', 'Gewatec-Zugang anpassen?', 'Programme und Systeme', 'Muessen Gewatec-Berechtigungen wegen der neuen Position angepasst werden?', 'gewatec', 'boolean', false, 11, true),
	(66, 6, 'pc_has_provis', 'Provis-Zugang anpassen?', 'Programme und Systeme', 'Muessen Provis-Berechtigungen wegen der neuen Position angepasst werden?', 'berechtigungen', 'boolean', false, 12, true),
	(68, 7, 'rc_new_role', 'Neue Rolle', 'Rollendetails', 'Neue Rolle oder Berechtigungsfunktion, die die Person kuenftig erhalten soll.', 'identitat', 'text', true, 1, true),
	(69, 7, 'rc_effective_date', 'Wirksamkeitsdatum', 'Rollendetails', 'Datum, ab dem die neue Rolle wirksam wird.', 'identitat', 'text', true, 2, true),
	(70, 7, 'rc_role_assignment_change', 'Rollen-Zuweisung anpassen?', 'Berechtigungen', 'Muessen fachliche oder technische Rollen explizit neu zugewiesen oder entzogen werden?', 'ad_user', 'boolean', true, 3, true),
	(71, 7, 'rc_permission_change', 'Weitere Berechtigungen anpassen?', 'Berechtigungen', 'Muessen zusaetzliche Berechtigungen oder Profile an die neue Rolle angepasst werden?', 'berechtigungen', 'boolean', false, 4, true),
	(72, 7, 'rc_ad_groups_change', 'AD-Gruppen anpassen?', 'Zugaenge', 'Muessen AD-Gruppen und Verzeichnisrollen an die neue Rolle angepasst werden?', 'ad_user', 'boolean', false, 5, true),
	(73, 7, 'rc_mailbox_change', 'Mailbox oder Alias anpassen?', 'Zugaenge', 'Muessen mailboxbezogene Sichtbarkeit oder Aliasrechte geaendert werden?', 'mailbox', 'boolean', false, 6, true),
	(74, 7, 'rc_has_habel', 'Habel-Zugang anpassen?', 'Programme und Systeme', 'Muessen Habel-Rollen oder Berechtigungen wegen des Rollenwechsels angepasst werden?', 'habel', 'boolean', false, 7, true),
	(75, 7, 'rc_has_ln', 'InforLN-Zugang anpassen?', 'Programme und Systeme', 'Muessen InforLN-Rollen oder Berechtigungen wegen des Rollenwechsels angepasst werden?', 'inforln', 'boolean', false, 8, true),
	(76, 7, 'rc_has_babtec', 'Babtec-Zugang anpassen?', 'Programme und Systeme', 'Muessen Babtec-Rollen oder Berechtigungen wegen des Rollenwechsels angepasst werden?', 'babtec', 'boolean', false, 9, true),
	(77, 7, 'rc_has_gewatec', 'Gewatec-Zugang anpassen?', 'Programme und Systeme', 'Muessen Gewatec-Rollen oder Berechtigungen wegen des Rollenwechsels angepasst werden?', 'gewatec', 'boolean', false, 10, true),
	(78, 7, 'rc_has_provis', 'Provis-Zugang anpassen?', 'Programme und Systeme', 'Muessen Provis-Rollen oder Berechtigungen wegen des Rollenwechsels angepasst werden?', 'berechtigungen', 'boolean', false, 11, true),
	(24, 1, 'consense_requested', 'Consense-User anlegen?', 'Programme und Systeme', 'Soll fuer die neue Person ein Consense-User angelegt werden?', 'consense', 'boolean', false, 20, true),
	(51, 4, 'dc_has_consense', 'Consense-Zugang anpassen?', 'Programme und Systeme', 'Muss der Consense-Zugang fuer die neue Abteilung angepasst oder neu eingerichtet werden?', 'consense', 'boolean', false, 12, true),
	(67, 6, 'pc_has_consense', 'Consense-Zugang anpassen?', 'Programme und Systeme', 'Muessen Consense-Berechtigungen wegen der neuen Position angepasst werden?', 'consense', 'boolean', false, 13, true),
	(79, 7, 'rc_has_consense', 'Consense-Zugang anpassen?', 'Programme und Systeme', 'Muessen Consense-Rollen oder Berechtigungen wegen des Rollenwechsels angepasst werden?', 'consense', 'boolean', false, 12, true);


--
-- Data for Name: app_role_answer_defaults; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: workflow_answer_options; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.workflow_answer_options OVERRIDING SYSTEM VALUE VALUES
	(1, 13, 'workstation', 'workstation', 'Workstation', 2),
	(2, 13, 'laptop', 'laptop', 'Laptop', 1),
	(3, 14, 'without_vpn', 'without_vpn', 'Ohne VPN', 2),
	(4, 14, 'with_vpn', 'with_vpn', 'Mit VPN', 1),
	(5, 26, 'stellvertretende_abteilungsleitung', 'stellvertretende_abteilungsleitung', 'stv. Abtlg.', 4),
	(6, 26, 'abteilungsleitung', 'abteilungsleitung', 'Abtlg. Ltg.', 3),
	(7, 26, 'bereichsleitung', 'bereichsleitung', 'Bereichsleitung', 2),
	(8, 26, 'leitung', 'leitung', 'Leitung', 1);


--
-- Data for Name: app_role_answer_default_options; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: app_role_permissions; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.app_role_permissions VALUES
	(1, 1, '2026-04-27 07:05:51.906903+00'),
	(2, 16, '2026-04-27 07:05:51.906903+00'),
	(2, 15, '2026-04-27 07:05:51.906903+00'),
	(2, 14, '2026-04-27 07:05:51.906903+00'),
	(2, 13, '2026-04-27 07:05:51.906903+00'),
	(2, 12, '2026-04-27 07:05:51.906903+00'),
	(2, 11, '2026-04-27 07:05:51.906903+00'),
	(2, 10, '2026-04-27 07:05:51.906903+00'),
	(2, 9, '2026-04-27 07:05:51.906903+00'),
	(2, 8, '2026-04-27 07:05:51.906903+00'),
	(2, 7, '2026-04-27 07:05:51.906903+00'),
	(2, 6, '2026-04-27 07:05:51.906903+00'),
	(2, 5, '2026-04-27 07:05:51.906903+00'),
	(2, 3, '2026-04-27 07:05:51.906903+00'),
	(2, 1, '2026-04-27 07:05:51.906903+00'),
	(3, 13, '2026-04-27 07:05:51.906903+00'),
	(3, 1, '2026-04-27 07:05:51.906903+00'),
	(4, 12, '2026-04-27 07:05:51.906903+00'),
	(4, 11, '2026-04-27 07:05:51.906903+00'),
	(4, 10, '2026-04-27 07:05:51.906903+00'),
	(4, 9, '2026-04-27 07:05:51.906903+00'),
	(4, 8, '2026-04-27 07:05:51.906903+00'),
	(4, 4, '2026-04-27 07:05:51.906903+00'),
	(4, 2, '2026-04-27 07:05:51.906903+00'),
	(4, 1, '2026-04-27 07:05:51.906903+00'),
	(5, 11, '2026-04-27 07:05:51.906903+00'),
	(5, 10, '2026-04-27 07:05:51.906903+00'),
	(5, 9, '2026-04-27 07:05:51.906903+00'),
	(5, 8, '2026-04-27 07:05:51.906903+00'),
	(5, 7, '2026-04-27 07:05:51.906903+00'),
	(5, 6, '2026-04-27 07:05:51.906903+00'),
	(5, 5, '2026-04-27 07:05:51.906903+00'),
	(5, 3, '2026-04-27 07:05:51.906903+00'),
	(5, 1, '2026-04-27 07:05:51.906903+00');


--
-- Data for Name: app_users; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: app_user_groups; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: app_user_permission_overrides; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: app_user_responsibilities; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: app_user_roles; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: auth_permission_audit_log; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: directory_identities; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: people; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: workflow_definitions; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.workflow_definitions OVERRIDING SYSTEM VALUE VALUES
	(2, 'offboarding', 'Offboarding', 'Business-phase workflow definition mapped to the legacy offboarding task generator.', '2026-04-27 07:05:51.979078+00', '2026-04-27 07:05:52.0201+00'),
	(3, 'department_change', 'Abteilungswechsel', 'Business-phase workflow definition mapped to the legacy department change task generator.', '2026-04-27 07:05:51.982966+00', '2026-04-27 07:05:52.023922+00'),
	(7, 'name_change', 'Namensaenderung', 'Business-phase workflow definition mapped to the legacy name change task generator.', '2026-04-27 07:05:52.03267+00', '2026-04-27 07:05:52.03267+00'),
	(8, 'position_change', 'Positionswechsel', 'Business-phase workflow definition mapped to the legacy position change task generator.', '2026-04-27 07:05:52.036601+00', '2026-04-27 07:05:52.036601+00'),
	(9, 'role_change', 'Rollenwechsel', 'Business-phase workflow definition mapped to the legacy role change task generator.', '2026-04-27 07:05:52.039432+00', '2026-04-27 07:05:52.039432+00'),
	(1, 'onboarding', 'Onboarding', 'Business-phase workflow definition mapped to the legacy onboarding task generator.', '2026-04-27 07:05:51.965724+00', '2026-04-27 07:05:52.043926+00');


--
-- Data for Name: workflow_definition_versions; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.workflow_definition_versions OVERRIDING SYSTEM VALUE VALUES
	(2, 2, 1, 'published', 'Offboarding Standard', 'Published offboarding mapping with a deprovision measure block and internal task generation.', 3, '2026-04-27 07:05:51.979078+00', '2026-04-27 07:05:52.0201+00', '2026-04-27 07:05:51.979078+00'),
	(3, 3, 1, 'published', 'Abteilungswechsel Standard', 'Published department change mapping with a change measure block and internal task generation.', 4, '2026-04-27 07:05:51.982966+00', '2026-04-27 07:05:52.023922+00', '2026-04-27 07:05:51.982966+00'),
	(4, 7, 1, 'published', 'Namensaenderung Standard', 'Published name change mapping with a rename measure block and internal task generation.', 5, '2026-04-27 07:05:52.03267+00', '2026-04-27 07:05:52.03267+00', '2026-04-27 07:05:52.03267+00'),
	(5, 8, 1, 'published', 'Positionswechsel Standard', 'Published position change mapping with a change measure block and internal task generation.', 6, '2026-04-27 07:05:52.036601+00', '2026-04-27 07:05:52.036601+00', '2026-04-27 07:05:52.036601+00'),
	(6, 9, 1, 'published', 'Rollenwechsel Standard', 'Published role change mapping with a change measure block and internal task generation.', 7, '2026-04-27 07:05:52.039432+00', '2026-04-27 07:05:52.039432+00', '2026-04-27 07:05:52.039432+00'),
	(1, 1, 1, 'published', 'Onboarding Standard', 'Published onboarding mapping with a provision measure block and internal task generation.', 1, '2026-04-27 07:05:51.965724+00', '2026-04-27 07:05:52.043926+00', '2026-04-27 07:05:51.965724+00');


--
-- Data for Name: workflow_nodes; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.workflow_nodes OVERRIDING SYSTEM VALUE VALUES
	(19, 2, 'start', 'start', 'Start', 0, NULL, NULL, '2026-04-27 07:05:52.0201+00'),
	(20, 2, 'collect_requirements', 'form', 'Offboarding-Umfang erfassen', 10, NULL, NULL, '2026-04-27 07:05:52.0201+00'),
	(21, 2, 'department_setup', 'measure_deprovision', 'Entzugsmaßnahmen erzeugen', 20, NULL, NULL, '2026-04-27 07:05:52.0201+00'),
	(22, 2, 'end', 'end', 'Abschluss', 30, NULL, NULL, '2026-04-27 07:05:52.0201+00'),
	(23, 3, 'start', 'start', 'Start', 0, NULL, NULL, '2026-04-27 07:05:52.023922+00'),
	(24, 3, 'collect_requirements', 'form', 'Wechselumfang erfassen', 10, NULL, NULL, '2026-04-27 07:05:52.023922+00'),
	(25, 3, 'department_setup', 'measure_change', 'Änderungsmaßnahmen erzeugen', 20, NULL, NULL, '2026-04-27 07:05:52.023922+00'),
	(26, 3, 'end', 'end', 'Abschluss', 30, NULL, NULL, '2026-04-27 07:05:52.023922+00'),
	(27, 4, 'start', 'start', 'Start', 0, NULL, NULL, '2026-04-27 07:05:52.03267+00'),
	(28, 4, 'collect_requirements', 'form', 'Namensänderung erfassen', 10, NULL, NULL, '2026-04-27 07:05:52.03267+00'),
	(29, 4, 'department_setup', 'measure_rename', 'Umbenennungsmaßnahmen erzeugen', 20, NULL, NULL, '2026-04-27 07:05:52.03267+00'),
	(30, 4, 'end', 'end', 'Abschluss', 30, NULL, NULL, '2026-04-27 07:05:52.03267+00'),
	(31, 5, 'start', 'start', 'Start', 0, NULL, NULL, '2026-04-27 07:05:52.036601+00'),
	(32, 5, 'collect_requirements', 'form', 'Positionswechsel erfassen', 10, NULL, NULL, '2026-04-27 07:05:52.036601+00'),
	(33, 5, 'department_setup', 'measure_change', 'Änderungsmaßnahmen erzeugen', 20, NULL, NULL, '2026-04-27 07:05:52.036601+00'),
	(34, 5, 'end', 'end', 'Abschluss', 30, NULL, NULL, '2026-04-27 07:05:52.036601+00'),
	(35, 6, 'start', 'start', 'Start', 0, NULL, NULL, '2026-04-27 07:05:52.039432+00'),
	(36, 6, 'collect_requirements', 'form', 'Rollenwechsel erfassen', 10, NULL, NULL, '2026-04-27 07:05:52.039432+00'),
	(37, 6, 'department_setup', 'measure_change', 'Änderungsmaßnahmen erzeugen', 20, NULL, NULL, '2026-04-27 07:05:52.039432+00'),
	(38, 6, 'end', 'end', 'Abschluss', 30, NULL, NULL, '2026-04-27 07:05:52.039432+00'),
	(39, 1, 'start', 'start', 'Start', 0, NULL, NULL, '2026-04-27 07:05:52.043926+00'),
	(40, 1, 'collect_requirements', 'form', 'Anforderungen erfassen', 10, NULL, NULL, '2026-04-27 07:05:52.043926+00'),
	(41, 1, 'department_setup', 'measure_provision', 'Bereitstellungsmaßnahmen erzeugen', 20, NULL, NULL, '2026-04-27 07:05:52.043926+00'),
	(42, 1, 'end', 'end', 'Abschluss', 30, NULL, NULL, '2026-04-27 07:05:52.043926+00');


--
-- Data for Name: workflow_node_actions; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: workflows; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: workflow_node_instances; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: automation_jobs; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: automation_job_attempts; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: automation_job_logs; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: department_action_templates; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: department_settings; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: directory_groups; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: directory_group_members; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: directory_group_role_mappings; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: directory_mapping_audit_log; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: directory_sync_log; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: notification_email_settings; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: notification_templates; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.notification_templates VALUES
	('workflow_created', 'Vorgang gestartet', 'Wird ausgelöst, wenn ein neuer Vorgang gestartet wurde und Empfänger für den Startfall auflösbar sind.', '{{workflow_label}} gestartet', 'Ein neuer {{workflow_label}} wurde gestartet und wartet auf Ihre Bearbeitung.

Bitte öffnen Sie den Vorgang über den folgenden Link.', false, '2026-04-27 07:05:52.140259+00', '2026-04-27 07:05:52.154301+00'),
	('task_ready', 'Aufgabe bereit', 'Wird ausgelöst, wenn aktuell benachrichtigbare offene oder bereitstehende Aufgaben für einen Vorgang vorhanden sind.', 'Neue Aufgaben für {{recipient_name}}', 'Für Sie wurden im {{process_label}} Aufgaben vorbereitet.

{{task_list_text}}

Bitte öffnen Sie Ihren Arbeitsbereich über den folgenden Link und bearbeiten Sie den Vorgang.', false, '2026-04-27 07:05:52.140259+00', '2026-04-27 07:05:52.154301+00'),
	('workflow_completed', 'Vorgang abgeschlossen', 'Wird ausgelöst, wenn ein Vorgang abgeschlossen ist und der Initiator aktuell benachrichtigt werden kann.', '{{process_name}} abgeschlossen', 'Der von Ihnen gestartete {{workflow_label}} wurde abgeschlossen.

Sie können den Vorgang bei Bedarf über den folgenden Link öffnen.', false, '2026-04-27 07:05:52.140259+00', '2026-04-27 07:05:52.154301+00'),
	('upcoming_change', 'Bevorstehender Wechsel', 'Wird ausgelöst, wenn für einen aktiven Durchlaufplan aktuell ein bevorstehender Bereichswechsel benachrichtigt werden soll.', 'Bevorstehender Wechsel: {{person_name}}', 'Für den Durchlaufplan {{plan_title}} von {{person_name}} steht ein Bereichswechsel bevor.

- Aktueller Bereich: {{current_department_name}}
- Nächster Bereich: {{next_department_name}}
- Wechseltermin: {{change_date}}

{{task_list_text}}

Bitte öffnen Sie die Anwendung über den folgenden Link.', false, '2026-04-27 07:05:52.140259+00', '2026-04-27 07:05:52.154301+00'),
	('reminder', 'Erinnerung fällige Aufgaben', 'Wird ausgelöst, wenn für einen aktiven Durchlaufplan heute fällige Rotationsaufgaben benachrichtigt werden sollen.', 'Fällige Rotationsaufgaben für {{person_name}}', 'Für den Durchlaufplan {{plan_title}} von {{person_name}} haben offene Aufgaben heute ihren Fälligkeitstermin erreicht.

- Aktueller Bereich: {{current_department_name}}
- Nächster Bereich: {{next_department_name}}
- Wechseltermin: {{change_date}}

{{task_list_text}}

Bitte öffnen Sie die Anwendung über den folgenden Link.', false, '2026-04-27 07:05:52.140259+00', '2026-04-27 07:05:52.154301+00'),
	('overdue', 'Überfällige Aufgaben', 'Wird ausgelöst, wenn für einen aktiven Durchlaufplan aktuell überfällige Rotationsaufgaben vorhanden sind.', 'Überfällige Rotationsaufgaben für {{person_name}}', 'Für den Durchlaufplan {{plan_title}} von {{person_name}} gibt es offene Rotationsaufgaben mit überschrittenem Fälligkeitstermin.

- Aktueller Bereich: {{current_department_name}}
- Nächster Bereich: {{next_department_name}}
- Wechseltermin: {{change_date}}

{{task_list_text}}

Bitte öffnen Sie die Anwendung über den folgenden Link.', false, '2026-04-27 07:05:52.140259+00', '2026-04-27 07:05:52.154301+00');


--
-- Data for Name: rotation_plans; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: rotation_stations; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: rotation_generated_tasks; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: rotation_audit_log; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: rotation_notifications; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: rotation_task_assignments; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: rotation_task_comments; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: system_event_log; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: system_responsibilities; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: task_templates; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.task_templates OVERRIDING SYSTEM VALUE VALUES
	(1, 1, 'supervisor_fills_document', 'Anforderungen auswählen und bestätigen', 'Führungskraft', 'Die Abteilungsleitung wählt die benötigten Anforderungen aus und bestätigt diese.', 'identitat', NULL, NULL, 'Abteilungsleitung', false, true, 2, 40, true, '2026-04-27 07:05:51.259997+00'),
	(3, 1, 'gewatec_user_create', 'Gewatec-User anlegen', 'Fachanwendungen', 'Gewatec-User für die neue Person anlegen.', 'berechtigungen', NULL, 4, 'AV', true, true, 3, 210, true, '2026-04-27 07:05:51.259997+00'),
	(4, 1, 'provis_user_create', 'Provis-User anlegen', 'Fachanwendungen', 'Provis-User für die neue Person anlegen.', 'berechtigungen', NULL, 3, 'AV', true, true, 3, 220, true, '2026-04-27 07:05:51.259997+00'),
	(41, 4, 'dc_habel_access_update', 'Habel-Zugang anpassen', 'Fachanwendungen', 'Habel-Berechtigungen auf die neue Abteilung umstellen.', 'habel', NULL, 10, 'IT', true, true, 3, 130, true, '2026-04-27 07:05:51.483999+00'),
	(5, 1, 'ad_user_create', 'AD-User anlegen', 'Zugänge', 'AD-User für die neue Person anlegen.', 'ad_user', NULL, 12, 'IT', true, true, 3, 100, true, '2026-04-27 07:05:51.259997+00'),
	(6, 1, 'permissions_from_reference_user', 'AD-Berechtigungen anhand Vergleichsuser übernehmen', 'Zugänge', 'AD-Berechtigungen anhand einer Vergleichsperson übernehmen.', 'berechtigungen', NULL, 12, 'IT', true, true, 3, 110, true, '2026-04-27 07:05:51.259997+00'),
	(7, 1, 'internet_access_enable', 'Internetzugang einrichten', 'Zugänge', 'Internetzugang für die neue Person freischalten.', 'internetzugang', NULL, 12, 'IT', true, true, 3, 145, true, '2026-04-27 07:05:51.259997+00'),
	(8, 1, 'internal_drive_access_grant', 'Laufwerksrechte vergeben', 'Zugänge', 'Zugriffsrechte für das interne Laufwerk der neuen Person einrichten.', 'berechtigungen', NULL, 12, 'IT', true, true, 3, 147, true, '2026-04-27 07:05:51.259997+00'),
	(9, 1, 'exchange_create', 'Mailbox anlegen', 'Zugänge', 'Mailbox für die neue Person anlegen.', 'mailbox', NULL, 11, 'IT', true, true, 3, 120, true, '2026-04-27 07:05:51.259997+00'),
	(10, 1, 'habel_user_create', 'Habel-User anlegen', 'Fachanwendungen', 'Habel-User für die neue Person anlegen.', 'habel', NULL, 10, 'IT', true, true, 3, 130, true, '2026-04-27 07:05:51.259997+00'),
	(11, 1, 'ln_user_create', 'LN-User anlegen', 'Fachanwendungen', 'LN-User für die neue Person anlegen.', 'react', NULL, 9, 'IT', true, true, 3, 140, true, '2026-04-27 07:05:51.259997+00'),
	(12, 1, 'office_install', 'Microsoft Office bereitstellen', 'Fachanwendungen', 'Microsoft Office für die neue Person bereitstellen und konfigurieren.', 'microsoft_office', NULL, 8, 'IT', true, true, 3, 148, true, '2026-04-27 07:05:51.259997+00'),
	(13, 1, 'hardware_procure', 'Hardware beschaffen', 'Ausstattung', 'Hardware-Bedarf prüfen und bei Bedarf passende Hardware beschaffen.', 'pc', NULL, 8, 'IT', true, true, 5, 150, true, '2026-04-27 07:05:51.259997+00'),
	(14, 1, 'hardware_setup', 'Hardware einrichten', 'Ausstattung', 'Hardware installieren und für den Einsatz vorbereiten.', 'pc', NULL, 8, 'IT', true, true, 3, 160, true, '2026-04-27 07:05:51.259997+00'),
	(15, 1, 'hardware_handover', 'Hardware bereitstellen', 'Ausstattung', 'Eingerichtete Hardware für die neue Person bereitstellen.', 'pc', NULL, 8, 'IT', true, true, 1, 170, true, '2026-04-27 07:05:51.259997+00'),
	(16, 1, 'phone_prepare', 'Tragbares Telefon bereitstellen', 'Ausstattung', 'Tragbares Telefon für die neue Person bereitstellen.', 'phone', NULL, 8, 'IT', true, true, 3, 175, true, '2026-04-27 07:05:51.259997+00'),
	(17, 1, 'catia_install', 'Catia bereitstellen', 'Fachanwendungen', 'Catia für die neue Person installieren und bereitstellen.', 'catia', NULL, 8, 'IT', true, true, 3, 180, true, '2026-04-27 07:05:51.259997+00'),
	(18, 1, 'datev_install', 'DATEV bereitstellen', 'Fachanwendungen', 'DATEV für die neue Person installieren und bereitstellen.', 'datev', NULL, 8, 'IT', true, true, 3, 185, true, '2026-04-27 07:05:51.259997+00'),
	(19, 1, 'tisoware_install', 'Tisoware bereitstellen', 'Fachanwendungen', 'Tisoware für die neue Person installieren und bereitstellen.', 'tiso', NULL, 8, 'IT', true, true, 3, 190, true, '2026-04-27 07:05:51.259997+00'),
	(20, 1, 'babtec_user_create', 'Babtec-User anlegen', 'Fachanwendungen', 'User in Babtec für die neue Person anlegen.', 'babtec', NULL, 16, 'QS', true, true, 3, 200, true, '2026-04-27 07:05:51.259997+00'),
	(22, 3, 'ob_gewatec_user_disable', 'Gewatec-User deaktivieren', 'Fachanwendungen', 'Gewatec-Zugang der ausscheidenden Person deaktivieren.', 'gewatec', NULL, 4, 'AV', true, true, 2, 210, true, '2026-04-27 07:05:51.468581+00'),
	(23, 3, 'ob_provis_user_disable', 'Provis-User deaktivieren', 'Fachanwendungen', 'Provis-Zugang der ausscheidenden Person deaktivieren.', 'berechtigungen', NULL, 3, 'AV', true, true, 2, 220, true, '2026-04-27 07:05:51.468581+00'),
	(24, 3, 'ob_ad_account_disable', 'AD-Konto deaktivieren', 'Zugänge', 'AD-Konto der ausscheidenden Person deaktivieren und Berechtigungen entziehen.', 'ad_user', NULL, 12, 'IT', true, true, 1, 100, true, '2026-04-27 07:05:51.468581+00'),
	(25, 3, 'ob_mailbox_disable', 'Mailbox deaktivieren', 'Zugänge', 'Mailbox der ausscheidenden Person deaktivieren.', 'mailbox', NULL, 11, 'IT', true, true, 1, 110, true, '2026-04-27 07:05:51.468581+00'),
	(26, 3, 'ob_habel_user_disable', 'Habel-User deaktivieren', 'Fachanwendungen', 'Habel-Zugang der ausscheidenden Person sperren.', 'habel', NULL, 10, 'IT', true, true, 2, 120, true, '2026-04-27 07:05:51.468581+00'),
	(27, 3, 'ob_ln_user_disable', 'LN-User deaktivieren', 'Fachanwendungen', 'InforLN-Zugang der ausscheidenden Person sperren.', 'react', NULL, 9, 'IT', true, true, 2, 130, true, '2026-04-27 07:05:51.468581+00'),
	(28, 3, 'ob_hardware_return', 'Hardware einziehen', 'Ausstattung', 'Hardware (Laptop, Workstation, Zubehör) der ausscheidenden Person einziehen und auf Vollständigkeit prüfen.', 'pc', NULL, 8, 'IT', true, true, 1, 140, true, '2026-04-27 07:05:51.468581+00'),
	(29, 3, 'ob_phone_return', 'Telefon einziehen', 'Ausstattung', 'Tragbares Telefon der ausscheidenden Person einziehen.', 'phone', NULL, 8, 'IT', true, true, 1, 150, true, '2026-04-27 07:05:51.468581+00'),
	(30, 3, 'ob_last_day_confirmed', 'Letzten Arbeitstag bestätigen', 'HR', 'Letzten Arbeitstag der ausscheidenden Person im System bestätigen. Schaltet alle Zugangs-Entzug-Aufgaben frei.', 'identitat', NULL, 15, 'HR', true, true, 1, 10, true, '2026-04-27 07:05:51.468581+00'),
	(31, 3, 'ob_exit_interview', 'Austrittsgespräch führen', 'HR', 'Strukturiertes Abschlussgespräch mit der ausscheidenden Person führen und dokumentieren.', 'identitat', NULL, 15, 'HR', true, true, 5, 20, true, '2026-04-27 07:05:51.468581+00'),
	(32, 3, 'ob_knowledge_transfer', 'Wissenstransfer organisieren', 'HR', 'Sicherstellen, dass kritisches Wissen und laufende Aufgaben an Nachfolger oder Team übergeben werden.', 'identitat', NULL, 15, 'HR', true, true, 5, 30, true, '2026-04-27 07:05:51.468581+00'),
	(33, 3, 'ob_badge_key_return', 'Schlüssel und Badge zurückgeben', 'HR', 'Ausweis, Schlüssel und sonstige Zugangsmittel von der ausscheidenden Person einziehen.', 'identitat', NULL, 15, 'HR', true, true, 1, 40, true, '2026-04-27 07:05:51.468581+00'),
	(34, 3, 'ob_babtec_user_disable', 'Babtec-User deaktivieren', 'Fachanwendungen', 'Babtec-Zugang der ausscheidenden Person deaktivieren.', 'babtec', NULL, 16, 'QS', true, true, 2, 200, true, '2026-04-27 07:05:51.468581+00'),
	(36, 4, 'dc_gewatec_access_update', 'Gewatec-Zugang anpassen', 'Fachanwendungen', 'Gewatec-Berechtigungen auf die neue Abteilung umstellen.', 'gewatec', NULL, 4, 'AV', true, true, 3, 210, true, '2026-04-27 07:05:51.483999+00'),
	(37, 4, 'dc_provis_access_update', 'Provis-Zugang anpassen', 'Fachanwendungen', 'Provis-Berechtigungen auf die neue Abteilung umstellen.', 'berechtigungen', NULL, 3, 'AV', true, true, 3, 220, true, '2026-04-27 07:05:51.483999+00'),
	(38, 4, 'dc_ad_group_update', 'AD-Gruppen aktualisieren', 'Zugänge', 'AD-Gruppen und Berechtigungen auf die neue Abteilung umstellen, alte abteilungsspezifische Gruppen entfernen.', 'ad_user', NULL, 12, 'IT', true, true, 2, 100, true, '2026-04-27 07:05:51.483999+00'),
	(2, 1, 'consense_setup', 'Consense User anlegen', 'Fachanwendungen', 'Consense-User fuer die neue Person anlegen.', 'consense', NULL, 1, 'QMB', true, true, 3, 230, true, '2026-04-27 07:05:51.259997+00'),
	(21, 3, 'ob_consense_user_disable', 'Consense-User deaktivieren', 'Fachanwendungen', 'Consense-Zugang der ausscheidenden Person deaktivieren.', 'consense', NULL, 1, 'QMB', true, true, 2, 230, true, '2026-04-27 07:05:51.468581+00'),
	(35, 4, 'dc_consense_access_update', 'Consense-Zugang anpassen', 'Fachanwendungen', 'Consense-Berechtigungen auf die neue Abteilung umstellen.', 'consense', NULL, 1, 'QMB', true, true, 3, 230, true, '2026-04-27 07:05:51.483999+00'),
	(39, 4, 'dc_drive_access_update', 'Laufwerk-Zugänge anpassen', 'Zugänge', 'Netzlaufwerk-Zugriffsrechte anpassen: Zugriff auf neue Abteilungs-Laufwerke gewähren, alte entziehen.', 'pc', NULL, 12, 'IT', true, true, 2, 110, true, '2026-04-27 07:05:51.483999+00'),
	(76, 7, 'rc_babtec_access_update', 'Babtec-Zugang anpassen', 'Fachanwendungen', 'Babtec-Rollen oder Berechtigungen an die neue Rolle anpassen.', 'babtec', NULL, 16, 'QS', true, true, 3, 220, true, '2026-04-27 07:05:51.58713+00'),
	(40, 4, 'dc_email_alias_update', 'E-Mail Alias anpassen', 'Zugänge', 'E-Mail Alias des Mitarbeiters aktualisieren, falls die neue Abteilung einen anderen Kürzel erfordert.', 'mailbox', NULL, 11, 'IT', true, true, 3, 120, true, '2026-04-27 07:05:51.483999+00'),
	(42, 4, 'dc_ln_access_update', 'InforLN-Zugang anpassen', 'Fachanwendungen', 'InforLN-Berechtigungen auf die neue Abteilung umstellen.', 'react', NULL, 9, 'IT', true, true, 3, 140, true, '2026-04-27 07:05:51.483999+00'),
	(43, 4, 'dc_hardware_swap', 'Hardware tauschen', 'Ausstattung', 'Hardware der neuen Arbeitsanforderungen entsprechend tauschen (z. B. stationär durch Laptop ersetzen).', 'pc', NULL, 8, 'IT', true, true, 2, 150, true, '2026-04-27 07:05:51.483999+00'),
	(44, 4, 'dc_change_date_confirmed', 'Wechseldatum bestätigen', 'HR', 'Bestätigen, dass das Wechseldatum eingetroffen ist. Schaltet alle Zugangs- und Ausstattungsaufgaben frei.', 'identitat', NULL, 15, 'HR', true, true, 1, 10, true, '2026-04-27 07:05:51.483999+00'),
	(45, 4, 'dc_hr_system_update', 'Abteilung im HR-System aktualisieren', 'HR', 'Abteilung des Mitarbeiters in der Personalakte und im HR-System auf die neue Abteilung umstellen.', 'identitat', NULL, 15, 'HR', true, true, 3, 20, true, '2026-04-27 07:05:51.483999+00'),
	(46, 4, 'dc_babtec_access_update', 'Babtec-Zugang anpassen', 'Fachanwendungen', 'Babtec-Berechtigungen auf die neue Abteilung umstellen.', 'babtec', NULL, 16, 'QS', true, true, 3, 200, true, '2026-04-27 07:05:51.483999+00'),
	(47, 5, 'nc_ad_username_update', 'AD-Benutzername aktualisieren', 'Zugaenge', 'AD-Benutzername, Anzeigename und verzeichnisbezogene Namensfelder auf den neuen Namen umstellen.', 'ad_user', NULL, 12, 'IT', true, true, 2, 100, true, '2026-04-27 07:05:51.498356+00'),
	(48, 5, 'nc_system_display_name_update', 'Anzeigenamen in Systemen aktualisieren', 'Systeme', 'Anzeigenamen in angeschlossenen Systemen und Verzeichnissen auf den neuen Namen angleichen.', 'berechtigungen', NULL, 12, 'IT', true, true, 3, 120, true, '2026-04-27 07:05:51.498356+00'),
	(49, 5, 'nc_mailbox_update', 'Mailbox und Alias aktualisieren', 'Zugaenge', 'Mailbox, primäre Adresse und Alias auf den neuen Namen umstellen.', 'mailbox', NULL, 11, 'IT', true, true, 2, 110, true, '2026-04-27 07:05:51.498356+00');
INSERT INTO public.task_templates OVERRIDING SYSTEM VALUE VALUES
	(50, 5, 'nc_effective_date_confirmed', 'Wirksamkeitsdatum bestaetigen', 'HR', 'Bestaetigen, dass das Wirksamkeitsdatum erreicht ist. Schaltet die technischen Umstellungsaufgaben frei.', 'identitat', NULL, 15, 'HR', true, true, 1, 10, true, '2026-04-27 07:05:51.498356+00'),
	(51, 5, 'nc_hr_master_data_update', 'HR-Stammdaten aktualisieren', 'HR', 'Neuen Namen in Personalakte und HR-Stammdaten pflegen.', 'identitat', NULL, 15, 'HR', true, true, 2, 20, true, '2026-04-27 07:05:51.498356+00'),
	(53, 6, 'pc_gewatec_access_update', 'Gewatec-Zugang anpassen', 'Fachanwendungen', 'Gewatec-Berechtigungen auf die neue Position umstellen.', 'gewatec', NULL, 4, 'AV', true, true, 3, 230, true, '2026-04-27 07:05:51.571037+00'),
	(54, 6, 'pc_provis_access_update', 'Provis-Zugang anpassen', 'Fachanwendungen', 'Provis-Berechtigungen auf die neue Position umstellen.', 'berechtigungen', NULL, 3, 'AV', true, true, 3, 240, true, '2026-04-27 07:05:51.571037+00'),
	(55, 6, 'pc_permission_profile_update', 'Berechtigungsprofil aktualisieren', 'Berechtigungen', 'Allgemeine Berechtigungsprofile und Freigaben an die neue Position anpassen.', 'ad_user', NULL, 12, 'IT', true, true, 2, 100, true, '2026-04-27 07:05:51.571037+00'),
	(56, 6, 'pc_ad_groups_update', 'AD-Gruppen aktualisieren', 'Zugaenge', 'AD-Gruppen und Rollen entsprechend der neuen Position anpassen.', 'ad_user', NULL, 12, 'IT', true, true, 2, 120, true, '2026-04-27 07:05:51.571037+00'),
	(57, 6, 'pc_drive_access_update', 'Laufwerk-Zugaenge anpassen', 'Zugaenge', 'Datei- und Laufwerksberechtigungen auf die Anforderungen der neuen Position umstellen.', 'pc', NULL, 12, 'IT', true, true, 2, 130, true, '2026-04-27 07:05:51.571037+00'),
	(58, 6, 'pc_mailbox_update', 'Mailbox und Alias anpassen', 'Zugaenge', 'Mailbox-bezogene Sichtbarkeit oder Aliasdaten an die neue Position anpassen.', 'mailbox', NULL, 11, 'IT', true, true, 3, 140, true, '2026-04-27 07:05:51.571037+00'),
	(59, 6, 'pc_habel_access_update', 'Habel-Zugang anpassen', 'Fachanwendungen', 'Habel-Berechtigungen auf die neue Position umstellen.', 'habel', NULL, 10, 'IT', true, true, 3, 200, true, '2026-04-27 07:05:51.571037+00'),
	(60, 6, 'pc_ln_access_update', 'InforLN-Zugang anpassen', 'Fachanwendungen', 'InforLN-Berechtigungen auf die neue Position umstellen.', 'react', NULL, 9, 'IT', true, true, 3, 210, true, '2026-04-27 07:05:51.571037+00'),
	(61, 6, 'pc_change_date_confirmed', 'Wechseldatum bestaetigen', 'HR', 'Bestaetigen, dass das Wechseldatum fuer die neue Position erreicht ist. Schaltet Folgeaufgaben frei.', 'identitat', NULL, 15, 'HR', true, true, 1, 10, true, '2026-04-27 07:05:51.571037+00'),
	(62, 6, 'pc_hr_master_data_update', 'Position in HR-Stammdaten aktualisieren', 'HR', 'Neue Position in Personalakte und HR-Stammdaten nachfuehren.', 'identitat', NULL, 15, 'HR', true, true, 2, 20, true, '2026-04-27 07:05:51.571037+00'),
	(63, 6, 'pc_training_assign', 'Schulungen einplanen', 'Qualifizierung', 'Noetige Schulungen und Einweisungen fuer die neue Position planen und dokumentieren.', 'identitat', NULL, 15, 'HR', true, true, 5, 110, true, '2026-04-27 07:05:51.571037+00'),
	(64, 6, 'pc_babtec_access_update', 'Babtec-Zugang anpassen', 'Fachanwendungen', 'Babtec-Berechtigungen auf die neue Position umstellen.', 'babtec', NULL, 16, 'QS', true, true, 3, 220, true, '2026-04-27 07:05:51.571037+00'),
	(66, 7, 'rc_gewatec_access_update', 'Gewatec-Zugang anpassen', 'Fachanwendungen', 'Gewatec-Rollen oder Berechtigungen an die neue Rolle anpassen.', 'gewatec', NULL, 4, 'AV', true, true, 3, 230, true, '2026-04-27 07:05:51.58713+00'),
	(67, 7, 'rc_provis_access_update', 'Provis-Zugang anpassen', 'Fachanwendungen', 'Provis-Rollen oder Berechtigungen an die neue Rolle anpassen.', 'berechtigungen', NULL, 3, 'AV', true, true, 3, 240, true, '2026-04-27 07:05:51.58713+00'),
	(68, 7, 'rc_role_assignment_update', 'Rollen-Zuweisung aktualisieren', 'Berechtigungen', 'Fachliche und technische Rollen der betroffenen Person auf die neue Rolle umstellen.', 'ad_user', NULL, 12, 'IT', true, true, 2, 100, true, '2026-04-27 07:05:51.58713+00'),
	(69, 7, 'rc_permission_profile_update', 'Berechtigungsprofil aktualisieren', 'Berechtigungen', 'Weitere Berechtigungsprofile und Freigaben an die neue Rolle anpassen.', 'berechtigungen', NULL, 12, 'IT', true, true, 2, 110, true, '2026-04-27 07:05:51.58713+00'),
	(70, 7, 'rc_ad_groups_update', 'AD-Gruppen aktualisieren', 'Zugaenge', 'AD-Gruppen und Verzeichnisrollen an die neue Rolle anpassen.', 'ad_user', NULL, 12, 'IT', true, true, 2, 120, true, '2026-04-27 07:05:51.58713+00'),
	(71, 7, 'rc_mailbox_update', 'Mailbox und Alias anpassen', 'Zugaenge', 'Mailboxbezogene Sichtbarkeit oder Aliasrechte an die neue Rolle anpassen.', 'mailbox', NULL, 11, 'IT', true, true, 3, 130, true, '2026-04-27 07:05:51.58713+00'),
	(72, 7, 'rc_habel_access_update', 'Habel-Zugang anpassen', 'Fachanwendungen', 'Habel-Rollen oder Berechtigungen an die neue Rolle anpassen.', 'habel', NULL, 10, 'IT', true, true, 3, 200, true, '2026-04-27 07:05:51.58713+00'),
	(73, 7, 'rc_ln_access_update', 'InforLN-Zugang anpassen', 'Fachanwendungen', 'InforLN-Rollen oder Berechtigungen an die neue Rolle anpassen.', 'react', NULL, 9, 'IT', true, true, 3, 210, true, '2026-04-27 07:05:51.58713+00'),
	(74, 7, 'rc_effective_date_confirmed', 'Wirksamkeitsdatum bestaetigen', 'HR', 'Bestaetigen, dass das Wirksamkeitsdatum fuer den Rollenwechsel erreicht ist. Schaltet Folgeaufgaben frei.', 'identitat', NULL, 15, 'HR', true, true, 1, 10, true, '2026-04-27 07:05:51.58713+00'),
	(75, 7, 'rc_hr_master_data_update', 'Rolle in HR-Stammdaten aktualisieren', 'HR', 'Neue Rolle in Personalakte und HR-Stammdaten nachfuehren.', 'identitat', NULL, 15, 'HR', true, true, 2, 20, true, '2026-04-27 07:05:51.58713+00'),
	(65, 7, 'rc_consense_access_update', 'Consense-Zugang anpassen', 'Fachanwendungen', 'Consense-Rollen oder Berechtigungen an die neue Rolle anpassen.', 'consense', NULL, 1, 'QMB', true, true, 3, 250, true, '2026-04-27 07:05:51.58713+00'),
	(52, 6, 'pc_consense_access_update', 'Consense-Zugang anpassen', 'Fachanwendungen', 'Consense-Berechtigungen auf die neue Position umstellen.', 'consense', NULL, 1, 'QMB', true, true, 3, 250, true, '2026-04-27 07:05:51.571037+00');


--
-- Data for Name: workflow_tasks; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: task_assignments; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: task_template_conditions; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.task_template_conditions OVERRIDING SYSTEM VALUE VALUES
	(1, 2, 1, 'consense_requested', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.264725+00'),
	(2, 3, 1, 'gewatec_requested', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.264725+00'),
	(3, 4, 1, 'provis_requested', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.264725+00'),
	(4, 5, 1, 'ad_user_requested', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.264725+00'),
	(5, 6, 1, 'comparison_user_available', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.264725+00'),
	(6, 6, 1, 'ad_user_requested', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.264725+00'),
	(7, 7, 1, 'internet_requested', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.264725+00'),
	(8, 8, 1, 'internal_drive_access_requested', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.264725+00'),
	(9, 9, 1, 'mailbox_requested', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.264725+00'),
	(10, 10, 1, 'habel_user_requested', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.264725+00'),
	(11, 11, 1, 'ln_user_requested', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.264725+00'),
	(12, 12, 1, 'microsoft_office_requested', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.264725+00'),
	(13, 13, 1, 'hardware_available', 'is_false', NULL, false, NULL, '2026-04-27 07:05:51.264725+00'),
	(14, 13, 1, 'hardware_requested', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.264725+00'),
	(15, 14, 1, 'hardware_requested', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.264725+00'),
	(16, 15, 1, 'hardware_requested', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.264725+00'),
	(17, 16, 1, 'phone_requested', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.264725+00'),
	(18, 17, 1, 'catia_requested', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.264725+00'),
	(19, 18, 1, 'datev_requested', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.264725+00'),
	(20, 19, 1, 'tiso_requested', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.264725+00'),
	(21, 20, 1, 'babtec_requested', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.264725+00'),
	(22, 21, 1, 'ob_has_consense', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.471889+00'),
	(23, 22, 1, 'ob_has_gewatec', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.471889+00'),
	(24, 23, 1, 'ob_has_provis', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.471889+00'),
	(25, 24, 1, 'ob_has_ad_account', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.471889+00'),
	(26, 25, 1, 'ob_has_mailbox', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.471889+00'),
	(27, 26, 1, 'ob_has_habel', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.471889+00'),
	(28, 27, 1, 'ob_has_ln', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.471889+00'),
	(29, 28, 1, 'ob_has_hardware', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.471889+00'),
	(30, 29, 1, 'ob_has_phone', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.471889+00'),
	(31, 31, 1, 'ob_exit_interview', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.471889+00'),
	(32, 32, 1, 'ob_knowledge_transfer', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.471889+00'),
	(33, 34, 1, 'ob_has_babtec', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.471889+00'),
	(34, 35, 1, 'dc_has_consense', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.48756+00'),
	(35, 36, 1, 'dc_has_gewatec', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.48756+00'),
	(36, 37, 1, 'dc_has_provis', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.48756+00'),
	(37, 38, 1, 'dc_ad_group_change', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.48756+00'),
	(38, 39, 1, 'dc_drive_access_change', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.48756+00'),
	(39, 40, 1, 'dc_email_alias_change', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.48756+00'),
	(40, 41, 1, 'dc_has_habel', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.48756+00'),
	(41, 42, 1, 'dc_has_ln', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.48756+00'),
	(42, 43, 1, 'dc_hardware_change', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.48756+00'),
	(43, 46, 1, 'dc_has_babtec', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.48756+00'),
	(44, 52, 1, 'pc_has_consense', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.575034+00'),
	(45, 53, 1, 'pc_has_gewatec', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.575034+00'),
	(46, 54, 1, 'pc_has_provis', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.575034+00'),
	(47, 55, 1, 'pc_permission_change', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.575034+00'),
	(48, 56, 1, 'pc_ad_groups_change', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.575034+00'),
	(49, 57, 1, 'pc_drive_access_change', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.575034+00'),
	(50, 58, 1, 'pc_mail_alias_change', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.575034+00');
INSERT INTO public.task_template_conditions OVERRIDING SYSTEM VALUE VALUES
	(51, 59, 1, 'pc_has_habel', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.575034+00'),
	(52, 60, 1, 'pc_has_ln', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.575034+00'),
	(53, 63, 1, 'pc_training_required', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.575034+00'),
	(54, 64, 1, 'pc_has_babtec', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.575034+00'),
	(55, 65, 1, 'rc_has_consense', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.590644+00'),
	(56, 66, 1, 'rc_has_gewatec', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.590644+00'),
	(57, 67, 1, 'rc_has_provis', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.590644+00'),
	(58, 68, 1, 'rc_role_assignment_change', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.590644+00'),
	(59, 69, 1, 'rc_permission_change', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.590644+00'),
	(60, 70, 1, 'rc_ad_groups_change', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.590644+00'),
	(61, 71, 1, 'rc_mailbox_change', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.590644+00'),
	(62, 72, 1, 'rc_has_habel', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.590644+00'),
	(63, 73, 1, 'rc_has_ln', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.590644+00'),
	(64, 76, 1, 'rc_has_babtec', 'is_true', NULL, true, NULL, '2026-04-27 07:05:51.590644+00');


--
-- Data for Name: task_template_dependencies; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.task_template_dependencies OVERRIDING SYSTEM VALUE VALUES
	(1, 20, 1, 'done'),
	(2, 19, 1, 'done'),
	(3, 18, 1, 'done'),
	(4, 17, 1, 'done'),
	(5, 16, 1, 'done'),
	(6, 14, 1, 'done'),
	(7, 13, 1, 'done'),
	(8, 12, 1, 'done'),
	(9, 11, 1, 'done'),
	(10, 10, 1, 'done'),
	(11, 8, 1, 'done'),
	(12, 7, 1, 'done'),
	(13, 5, 1, 'done'),
	(14, 4, 1, 'done'),
	(15, 3, 1, 'done'),
	(16, 2, 1, 'done'),
	(17, 9, 5, 'done'),
	(18, 6, 5, 'done'),
	(19, 14, 13, 'done'),
	(20, 15, 14, 'done'),
	(21, 25, 24, 'done'),
	(22, 34, 30, 'done'),
	(23, 33, 30, 'done'),
	(24, 29, 30, 'done'),
	(25, 28, 30, 'done'),
	(26, 27, 30, 'done'),
	(27, 26, 30, 'done'),
	(28, 24, 30, 'done'),
	(29, 23, 30, 'done'),
	(30, 22, 30, 'done'),
	(31, 21, 30, 'done'),
	(32, 46, 44, 'done'),
	(33, 43, 44, 'done'),
	(34, 42, 44, 'done'),
	(35, 41, 44, 'done'),
	(36, 40, 44, 'done'),
	(37, 39, 44, 'done'),
	(38, 38, 44, 'done'),
	(39, 37, 44, 'done'),
	(40, 36, 44, 'done'),
	(41, 35, 44, 'done'),
	(42, 48, 47, 'done'),
	(43, 49, 50, 'done'),
	(44, 47, 50, 'done'),
	(45, 64, 61, 'done'),
	(46, 63, 61, 'done'),
	(47, 60, 61, 'done'),
	(48, 59, 61, 'done'),
	(49, 58, 61, 'done'),
	(50, 57, 61, 'done');
INSERT INTO public.task_template_dependencies OVERRIDING SYSTEM VALUE VALUES
	(51, 56, 61, 'done'),
	(52, 55, 61, 'done'),
	(53, 54, 61, 'done'),
	(54, 53, 61, 'done'),
	(55, 52, 61, 'done'),
	(56, 76, 74, 'done'),
	(57, 73, 74, 'done'),
	(58, 72, 74, 'done'),
	(59, 71, 74, 'done'),
	(60, 70, 74, 'done'),
	(61, 69, 74, 'done'),
	(62, 68, 74, 'done'),
	(63, 67, 74, 'done'),
	(64, 66, 74, 'done'),
	(65, 65, 74, 'done');


--
-- Data for Name: workflow_answer_derivation_rules; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.workflow_answer_derivation_rules OVERRIDING SYSTEM VALUE VALUES
	(1, 1, 3, 'has_ad_account', 'ob_has_ad_account', 'copy_boolean', true, 1, '2026-04-27 07:05:51.556788+00'),
	(2, 1, 3, 'has_babtec', 'ob_has_babtec', 'copy_boolean', true, 2, '2026-04-27 07:05:51.556788+00'),
	(3, 1, 3, 'has_consense', 'ob_has_consense', 'copy_boolean', true, 3, '2026-04-27 07:05:51.556788+00'),
	(4, 1, 3, 'has_gewatec', 'ob_has_gewatec', 'copy_boolean', true, 4, '2026-04-27 07:05:51.556788+00'),
	(5, 1, 3, 'has_habel', 'ob_has_habel', 'copy_boolean', true, 5, '2026-04-27 07:05:51.556788+00'),
	(6, 1, 3, 'has_hardware', 'ob_has_hardware', 'copy_boolean', true, 6, '2026-04-27 07:05:51.556788+00'),
	(7, 1, 3, 'has_ln', 'ob_has_ln', 'copy_boolean', true, 7, '2026-04-27 07:05:51.556788+00'),
	(8, 1, 3, 'has_mailbox', 'ob_has_mailbox', 'copy_boolean', true, 8, '2026-04-27 07:05:51.556788+00'),
	(9, 1, 3, 'has_phone', 'ob_has_phone', 'copy_boolean', true, 9, '2026-04-27 07:05:51.556788+00'),
	(10, 1, 3, 'has_provis', 'ob_has_provis', 'copy_boolean', true, 10, '2026-04-27 07:05:51.556788+00'),
	(11, 1, 4, 'has_ad_account', 'dc_ad_group_change', 'copy_boolean', true, 1, '2026-04-27 07:05:51.561498+00'),
	(12, 1, 4, 'has_babtec', 'dc_has_babtec', 'copy_boolean', true, 2, '2026-04-27 07:05:51.561498+00'),
	(13, 1, 4, 'has_consense', 'dc_has_consense', 'copy_boolean', true, 3, '2026-04-27 07:05:51.561498+00'),
	(14, 1, 4, 'has_gewatec', 'dc_has_gewatec', 'copy_boolean', true, 4, '2026-04-27 07:05:51.561498+00'),
	(15, 1, 4, 'has_habel', 'dc_has_habel', 'copy_boolean', true, 5, '2026-04-27 07:05:51.561498+00'),
	(16, 1, 4, 'has_hardware', 'dc_hardware_change', 'copy_boolean', true, 6, '2026-04-27 07:05:51.561498+00'),
	(17, 1, 4, 'has_ln', 'dc_has_ln', 'copy_boolean', true, 7, '2026-04-27 07:05:51.561498+00'),
	(18, 1, 4, 'has_provis', 'dc_has_provis', 'copy_boolean', true, 8, '2026-04-27 07:05:51.561498+00');


--
-- Data for Name: workflow_answer_reset_rules; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.workflow_answer_reset_rules OVERRIDING SYSTEM VALUE VALUES
	(11, 1, 'when_not_true', 2, true, false, false, false, false, 1, '2026-04-27 07:05:51.378611+00'),
	(12, 2, 'when_not_true', 3, false, true, false, false, false, 1, '2026-04-27 07:05:51.378611+00'),
	(13, 1, 'when_not_true', 3, false, true, false, false, false, 2, '2026-04-27 07:05:51.378611+00'),
	(14, 9, 'when_not_true', 10, true, false, false, false, false, 1, '2026-04-27 07:05:51.378611+00'),
	(16, 9, 'when_not_true', 12, true, false, false, false, false, 2, '2026-04-27 07:05:51.378611+00'),
	(17, 9, 'when_not_true', 13, false, false, false, true, true, 3, '2026-04-27 07:05:51.378611+00'),
	(18, 13, 'single_select_mismatch', 14, false, false, false, true, true, 1, '2026-04-27 07:05:51.378611+00'),
	(19, 9, 'when_not_true', 14, false, false, false, true, true, 4, '2026-04-27 07:05:51.378611+00'),
	(20, 25, 'when_not_true', 26, false, false, false, false, true, 1, '2026-04-27 07:05:51.378611+00'),
	(21, 28, 'when_not_true', 29, true, false, false, false, false, 1, '2026-04-27 07:05:51.465644+00'),
	(22, 30, 'when_not_true', 31, true, false, false, false, false, 1, '2026-04-27 07:05:51.465644+00'),
	(23, 10, 'when_not_true', 11, false, true, false, false, false, 1, '2026-04-27 07:05:52.166492+00');


--
-- Data for Name: workflow_answers; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: workflow_answer_selected_options; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: workflow_answer_single_select_keep_values; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.workflow_answer_single_select_keep_values OVERRIDING SYSTEM VALUE VALUES
	(2, 13, 'laptop', 1, '2026-04-27 07:05:51.380993+00');


--
-- Data for Name: workflow_answer_validation_rules; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.workflow_answer_validation_rules VALUES
	(3, 'text_required', 'Bitte den Referenzuser angeben.', '2026-04-27 07:05:51.377022+00'),
	(14, 'single_select_required', 'Bitte auswählen, ob der Laptop mit VPN oder ohne VPN benötigt wird.', '2026-04-27 07:05:51.377022+00'),
	(26, 'multi_select_required', 'Bitte mindestens eine Funktion für die Laufwerksrechte auswählen.', '2026-04-27 07:05:51.377022+00'),
	(11, 'text_required', 'Bitte angeben, welche Hardware übernommen wird.', '2026-04-27 07:05:52.162966+00');


--
-- Data for Name: workflow_answer_visibility_rules; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.workflow_answer_visibility_rules OVERRIDING SYSTEM VALUE VALUES
	(11, 3, 1, 'boolean_true', NULL, false, 1, '2026-04-27 07:05:51.374729+00'),
	(12, 2, 1, 'boolean_true', NULL, true, 1, '2026-04-27 07:05:51.374729+00'),
	(13, 3, 2, 'boolean_true', NULL, false, 2, '2026-04-27 07:05:51.374729+00'),
	(14, 14, 9, 'boolean_true', NULL, true, 1, '2026-04-27 07:05:51.374729+00'),
	(15, 13, 9, 'boolean_true', NULL, true, 1, '2026-04-27 07:05:51.374729+00'),
	(16, 12, 9, 'boolean_true', NULL, true, 1, '2026-04-27 07:05:51.374729+00'),
	(17, 10, 9, 'boolean_true', NULL, false, 1, '2026-04-27 07:05:51.374729+00'),
	(19, 14, 13, 'selected_option_value', 'laptop', false, 2, '2026-04-27 07:05:51.374729+00'),
	(20, 26, 25, 'boolean_true', NULL, false, 1, '2026-04-27 07:05:51.374729+00'),
	(21, 29, 28, 'boolean_true', NULL, false, 1, '2026-04-27 07:05:51.463267+00'),
	(22, 31, 30, 'boolean_true', NULL, false, 1, '2026-04-27 07:05:51.463267+00'),
	(23, 11, 10, 'boolean_true', NULL, false, 1, '2026-04-27 07:05:52.160257+00');


--
-- Data for Name: workflow_audit_log; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: workflow_edges; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.workflow_edges OVERRIDING SYSTEM VALUE VALUES
	(15, 2, 19, 20, 0, NULL, '2026-04-27 07:05:52.0201+00'),
	(16, 2, 20, 21, 0, NULL, '2026-04-27 07:05:52.0201+00'),
	(17, 2, 21, 22, 0, NULL, '2026-04-27 07:05:52.0201+00'),
	(18, 3, 23, 24, 0, NULL, '2026-04-27 07:05:52.023922+00'),
	(19, 3, 24, 25, 0, NULL, '2026-04-27 07:05:52.023922+00'),
	(20, 3, 25, 26, 0, NULL, '2026-04-27 07:05:52.023922+00'),
	(21, 4, 27, 28, 0, NULL, '2026-04-27 07:05:52.03267+00'),
	(22, 4, 28, 29, 0, NULL, '2026-04-27 07:05:52.03267+00'),
	(23, 4, 29, 30, 0, NULL, '2026-04-27 07:05:52.03267+00'),
	(24, 5, 31, 32, 0, NULL, '2026-04-27 07:05:52.036601+00'),
	(25, 5, 32, 33, 0, NULL, '2026-04-27 07:05:52.036601+00'),
	(26, 5, 33, 34, 0, NULL, '2026-04-27 07:05:52.036601+00'),
	(27, 6, 35, 36, 0, NULL, '2026-04-27 07:05:52.039432+00'),
	(28, 6, 36, 37, 0, NULL, '2026-04-27 07:05:52.039432+00'),
	(29, 6, 37, 38, 0, NULL, '2026-04-27 07:05:52.039432+00'),
	(30, 1, 39, 40, 0, NULL, '2026-04-27 07:05:52.043926+00'),
	(31, 1, 40, 41, 0, NULL, '2026-04-27 07:05:52.043926+00'),
	(32, 1, 41, 42, 0, NULL, '2026-04-27 07:05:52.043926+00');


--
-- Data for Name: workflow_links; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: workflow_node_configs; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.workflow_node_configs OVERRIDING SYSTEM VALUE VALUES
	(7, 20, '{"legacyProcessTypeKey": "offboarding"}', '2026-04-27 07:05:52.0201+00'),
	(8, 24, '{"legacyProcessTypeKey": "department_change"}', '2026-04-27 07:05:52.023922+00'),
	(9, 28, '{"legacyProcessTypeKey": "name_change"}', '2026-04-27 07:05:52.03267+00'),
	(10, 32, '{"legacyProcessTypeKey": "position_change"}', '2026-04-27 07:05:52.036601+00'),
	(11, 36, '{"legacyProcessTypeKey": "role_change"}', '2026-04-27 07:05:52.039432+00'),
	(12, 40, '{"legacyProcessTypeKey": "onboarding"}', '2026-04-27 07:05:52.043926+00');


--
-- Data for Name: workflow_notifications; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: workflow_runtime_events; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: workflow_task_comments; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: workflow_task_dependencies; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Name: action_definitions_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.action_definitions_id_seq', 10, true);


--
-- Name: app_groups_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.app_groups_id_seq', 1, false);


--
-- Name: app_permissions_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.app_permissions_id_seq', 16, true);


--
-- Name: app_responsibilities_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.app_responsibilities_id_seq', 37, true);


--
-- Name: app_roles_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.app_roles_id_seq', 5, true);


--
-- Name: app_user_permission_overrides_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.app_user_permission_overrides_id_seq', 1, false);


--
-- Name: app_users_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.app_users_id_seq', 1, false);


--
-- Name: auth_permission_audit_log_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.auth_permission_audit_log_id_seq', 1, false);


--
-- Name: automation_job_attempts_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.automation_job_attempts_id_seq', 1, false);


--
-- Name: automation_job_logs_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.automation_job_logs_id_seq', 1, false);


--
-- Name: automation_jobs_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.automation_jobs_id_seq', 1, false);


--
-- Name: department_action_templates_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.department_action_templates_id_seq', 1, false);


--
-- Name: departments_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.departments_id_seq', 2, true);


--
-- Name: directory_group_role_mappings_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.directory_group_role_mappings_id_seq', 1, false);


--
-- Name: directory_groups_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.directory_groups_id_seq', 1, false);


--
-- Name: directory_identities_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.directory_identities_id_seq', 1, false);


--
-- Name: directory_mapping_audit_log_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.directory_mapping_audit_log_id_seq', 1, false);


--
-- Name: directory_sync_log_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.directory_sync_log_id_seq', 1, false);


--
-- Name: people_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.people_id_seq', 1, false);


--
-- Name: process_types_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.process_types_id_seq', 7, true);


--
-- Name: rotation_audit_log_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.rotation_audit_log_id_seq', 1, false);


--
-- Name: rotation_generated_tasks_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.rotation_generated_tasks_id_seq', 1, false);


--
-- Name: rotation_notifications_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.rotation_notifications_id_seq', 1, false);


--
-- Name: rotation_plans_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.rotation_plans_id_seq', 1, false);


--
-- Name: rotation_stations_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.rotation_stations_id_seq', 1, false);


--
-- Name: rotation_task_assignments_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.rotation_task_assignments_id_seq', 1, false);


--
-- Name: rotation_task_comments_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.rotation_task_comments_id_seq', 1, false);


--
-- Name: system_event_log_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.system_event_log_id_seq', 1, false);


--
-- Name: system_responsibilities_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.system_responsibilities_id_seq', 1, false);


--
-- Name: task_assignments_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.task_assignments_id_seq', 1, false);


--
-- Name: task_template_conditions_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.task_template_conditions_id_seq', 64, true);


--
-- Name: task_template_dependencies_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.task_template_dependencies_id_seq', 65, true);


--
-- Name: task_templates_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.task_templates_id_seq', 76, true);


--
-- Name: workflow_answer_definitions_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.workflow_answer_definitions_id_seq', 80, true);


--
-- Name: workflow_answer_derivation_rules_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.workflow_answer_derivation_rules_id_seq', 18, true);


--
-- Name: workflow_answer_options_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.workflow_answer_options_id_seq', 8, true);


--
-- Name: workflow_answer_reset_rules_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.workflow_answer_reset_rules_id_seq', 23, true);


--
-- Name: workflow_answer_selected_options_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.workflow_answer_selected_options_id_seq', 1, false);


--
-- Name: workflow_answer_single_select_keep_values_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.workflow_answer_single_select_keep_values_id_seq', 2, true);


--
-- Name: workflow_answer_visibility_rules_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.workflow_answer_visibility_rules_id_seq', 23, true);


--
-- Name: workflow_answers_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.workflow_answers_id_seq', 1, false);


--
-- Name: workflow_audit_log_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.workflow_audit_log_id_seq', 1, false);


--
-- Name: workflow_definition_versions_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.workflow_definition_versions_id_seq', 6, true);


--
-- Name: workflow_definitions_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.workflow_definitions_id_seq', 10, true);


--
-- Name: workflow_edges_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.workflow_edges_id_seq', 32, true);


--
-- Name: workflow_links_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.workflow_links_id_seq', 1, false);


--
-- Name: workflow_node_actions_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.workflow_node_actions_id_seq', 1, false);


--
-- Name: workflow_node_configs_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.workflow_node_configs_id_seq', 12, true);


--
-- Name: workflow_node_instances_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.workflow_node_instances_id_seq', 1, false);


--
-- Name: workflow_nodes_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.workflow_nodes_id_seq', 42, true);


--
-- Name: workflow_notifications_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.workflow_notifications_id_seq', 1, false);


--
-- Name: workflow_runtime_events_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.workflow_runtime_events_id_seq', 1, false);


--
-- Name: workflow_task_comments_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.workflow_task_comments_id_seq', 1, false);


--
-- Name: workflow_task_dependencies_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.workflow_task_dependencies_id_seq', 1, false);


--
-- Name: workflow_tasks_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.workflow_tasks_id_seq', 1, false);


--
-- Name: workflows_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.workflows_id_seq', 1, false);
