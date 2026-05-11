-- ============================================================================
-- 02_dev_seed.sql
--
-- Konsolidierte Entwicklungs-Seed-Daten. Enthaelt prod-Bootstrap PLUS
-- dev-spezifische Daten (Rotations-Beispieltemplates, Department-Templates,
-- lokale notification_email_settings).
-- Wird im dev-Init nach 01_schema.sql ausgefuehrt.
--
-- Erzeugt aus pg_dump --data-only gegen einen frisch initialisierten dev-Stand.
-- Originale Migrationsdateien siehe db/_archive/.
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

-- pg_dump emittiert die Daten-Bloecke alphabetisch, nicht in FK-Dependency-Reihenfolge
-- (z.B. workflow_answer_definitions vor workflow_definitions). Wir deaktivieren daher
-- waehrend des Seed-Loads die Trigger-Auswertung — analog zu pg_dump --disable-triggers.
SET session_replication_role = replica;

--
-- Data for Name: action_definitions; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.action_definitions OVERRIDING SYSTEM VALUE VALUES
	(1, 'CreateAdUser', 'Create AD User', 'Simulated creation of an Active Directory user.', 'simulated_directory', '{"type": "object", "additionalProperties": true}', true, false, true, '2026-04-27 07:07:09.400545+00', '2026-04-27 07:07:10.354481+00'),
	(2, 'CreateMailbox', 'Create Mailbox', 'Simulated provisioning of a mailbox.', 'simulated_mailbox', '{"type": "object", "additionalProperties": true}', true, false, true, '2026-04-27 07:07:09.400545+00', '2026-04-27 07:07:10.354481+00'),
	(3, 'AssignGroups', 'Assign Groups', 'Simulated assignment of directory groups.', 'simulated_directory_groups', '{"type": "object", "additionalProperties": true}', true, false, true, '2026-04-27 07:07:09.400545+00', '2026-04-27 07:07:10.354481+00'),
	(4, 'CreateErpEmployee', 'Create ERP Employee', 'Simulated creation of an ERP employee.', 'simulated_erp', '{"type": "object", "additionalProperties": true}', true, false, false, '2026-04-27 07:07:09.400545+00', '2026-04-27 07:07:10.354481+00'),
	(5, 'SendWelcomeMail', 'Send Welcome Mail', 'Simulated sending of a welcome email.', 'simulated_notification', '{"type": "object", "additionalProperties": true}', true, false, true, '2026-04-27 07:07:09.400545+00', '2026-04-27 07:07:10.354481+00');


--
-- Data for Name: app_groups; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: departments; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.departments OVERRIDING SYSTEM VALUE VALUES
	(1, 'Versand'),
	(2, 'Einkauf'),
	(3, 'Werkzeugbau'),
	(4, 'Verkauf'),
	(5, 'Fertigungssteuerung'),
	(6, 'Ausbildung kaufmaennisch'),
	(7, 'Ausbildung technisch');


--
-- Data for Name: app_responsibilities; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.app_responsibilities OVERRIDING SYSTEM VALUE VALUES
	(37, 7, 'ausbildungsleitung_technisch', NULL, 'Ausbildungsleitung Technik', 'department_lead', 'Stabile Leitung fuer technische Ausbildung und Studenten. Immer dieselbe Ansprechperson.', true, '2026-04-27 07:07:10.470633+00'),
	(8, NULL, 'it_hardware', 'hardware', 'Hardware', 'application', 'Verantwortung fuer Hardware-Bereitstellung und Einrichtung.', true, '2026-04-27 07:07:09.703692+00'),
	(9, NULL, 'it_ln', 'ln', 'LN', 'application', 'Verantwortung fuer LN-Zugaenge.', true, '2026-04-27 07:07:09.703692+00'),
	(10, NULL, 'it_habel', 'habel', 'Habel', 'application', 'Verantwortung fuer Habel-Zugaenge.', true, '2026-04-27 07:07:09.703692+00'),
	(11, NULL, 'it_mailbox', 'mailbox', 'Mailbox', 'application', 'Verantwortung fuer Mailbox-Einrichtung.', true, '2026-04-27 07:07:09.703692+00'),
	(12, NULL, 'it_ad', 'ad', 'AD', 'application', 'Verantwortung fuer AD-Konto und zentrale Berechtigungen.', true, '2026-04-27 07:07:09.703692+00'),
	(13, NULL, 'leadership_it', NULL, 'Abteilungsleitung IT', 'department_lead', 'Fuehrungsverantwortung fuer Onboardings der IT.', true, '2026-04-27 07:07:09.703692+00'),
	(18, NULL, 'leadership_sales', NULL, 'Abteilungsleitung Vertrieb', 'department_lead', 'Fuehrungsverantwortung fuer Onboardings des Vertriebs.', true, '2026-04-27 07:07:09.703692+00'),
	(7, NULL, 'leadership_prototype', NULL, 'Abteilungsleitung Prototypenbau', 'department_lead', 'Fuehrungsverantwortung fuer Onboardings im Prototypenbau.', true, '2026-04-27 07:07:09.703692+00'),
	(1, NULL, 'qmb_consense', 'consense', 'Consense', 'application', 'Verantwortung fuer Consense im QMB.', true, '2026-04-27 07:07:09.703692+00'),
	(2, NULL, 'leadership_qmb', NULL, 'Abteilungsleitung QMB', 'department_lead', 'Fuehrungsverantwortung fuer Onboardings des QMB.', true, '2026-04-27 07:07:09.703692+00'),
	(14, NULL, 'leadership_hr', NULL, 'Abteilungsleitung HR', 'department_lead', 'Fuehrungsverantwortung fuer Onboardings der HR.', true, '2026-04-27 07:07:09.703692+00'),
	(15, NULL, 'hr_workflow_initiator', NULL, 'HR-Workflow-Initiierung', 'process', 'Verantwortung fuer Start, Abstimmung und Begleitung von Workflows.', true, '2026-04-27 07:07:09.703692+00'),
	(3, NULL, 'av_provis', 'provis', 'Provis', 'application', 'Verantwortung fuer Provis in der AV.', true, '2026-04-27 07:07:09.703692+00'),
	(4, NULL, 'av_gewatec', 'gewatec', 'Gewatec', 'application', 'Verantwortung fuer Gewatec in der AV.', true, '2026-04-27 07:07:09.703692+00'),
	(5, NULL, 'leadership_av', NULL, 'Abteilungsleitung AV', 'department_lead', 'Fuehrungsverantwortung fuer Onboardings der AV.', true, '2026-04-27 07:07:09.703692+00'),
	(16, NULL, 'qs_babtec', 'babtec', 'Babtec', 'application', 'Verantwortung fuer Babtec in der QS.', true, '2026-04-27 07:07:09.703692+00'),
	(17, NULL, 'leadership_qs', NULL, 'Abteilungsleitung QS', 'department_lead', 'Fuehrungsverantwortung fuer Onboardings der QS.', true, '2026-04-27 07:07:09.703692+00'),
	(6, NULL, 'leadership_production', NULL, 'Abteilungsleitung Produktion', 'department_lead', 'Fuehrungsverantwortung fuer Onboardings der Produktion.', true, '2026-04-27 07:07:09.703692+00');


--
-- Data for Name: app_group_responsibilities; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: app_roles; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.app_roles OVERRIDING SYSTEM VALUE VALUES
	(1, NULL, 'auth_reader', 'Leser', 'system', true, '2026-04-27 07:07:09.699706+00'),
	(2, NULL, 'auth_admin', 'Admin', 'system', true, '2026-04-27 07:07:09.699706+00'),
	(3, NULL, 'auth_worker', 'Bearbeiter', 'system', true, '2026-04-27 07:07:09.699706+00'),
	(4, NULL, 'auth_manager', 'Abteilungsleitung', 'system', true, '2026-04-27 07:07:09.699706+00'),
	(5, NULL, 'auth_hr', 'HR', 'system', true, '2026-04-27 07:07:09.699706+00');


--
-- Data for Name: app_group_roles; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: app_permissions; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.app_permissions OVERRIDING SYSTEM VALUE VALUES
	(1, 'app.access', 'App-Zugang', 'Erlaubt die Nutzung der Anwendung.', 'global', 'general', true, '2026-04-27 07:07:10.281549+00'),
	(2, 'users.view_department', 'Benutzer der eigenen Abteilung sehen', 'Darf Benutzer der freigegebenen Abteilungen sehen.', 'department', 'users', true, '2026-04-27 07:07:10.281549+00'),
	(3, 'users.view_all_departments', 'Alle Benutzer sehen', 'Darf Benutzer aller Abteilungen sehen.', 'global', 'users', true, '2026-04-27 07:07:10.281549+00'),
	(4, 'workflows.view_department', 'Workflows der eigenen Abteilung sehen', 'Darf Workflows der freigegebenen Abteilungen sehen.', 'department', 'workflows', true, '2026-04-27 07:07:10.281549+00'),
	(5, 'workflows.view_all', 'Alle Workflows sehen', 'Darf Workflows aller Abteilungen sehen.', 'global', 'workflows', true, '2026-04-27 07:07:10.281549+00'),
	(6, 'workflows.create.onboarding', 'Onboarding starten', 'Darf Onboarding-Vorgaenge starten.', 'department', 'workflows', true, '2026-04-27 07:07:10.281549+00'),
	(7, 'workflows.create.offboarding', 'Offboarding starten', 'Darf Offboarding-Vorgaenge starten.', 'department', 'workflows', true, '2026-04-27 07:07:10.281549+00'),
	(8, 'workflows.create.department_change', 'Abteilungswechsel starten', 'Darf Abteilungswechsel-Vorgaenge starten.', 'department', 'workflows', true, '2026-04-27 07:07:10.281549+00'),
	(9, 'workflows.create.position_change', 'Positionswechsel starten', 'Darf Positionswechsel-Vorgaenge starten.', 'department', 'workflows', true, '2026-04-27 07:07:10.281549+00'),
	(10, 'workflows.create.role_change', 'Rollenwechsel starten', 'Darf Rollenwechsel-Vorgaenge starten.', 'department', 'workflows', true, '2026-04-27 07:07:10.281549+00'),
	(11, 'workflows.create.name_change', 'Namensaenderung starten', 'Darf Namensaenderungen starten.', 'department', 'workflows', true, '2026-04-27 07:07:10.281549+00'),
	(12, 'tasks.execute.supervisor', 'Supervisor-Aufgaben bearbeiten', 'Darf Supervisor-Schritte der freigegebenen Abteilungen bearbeiten.', 'department', 'tasks', true, '2026-04-27 07:07:10.281549+00'),
	(13, 'tasks.execute.department', 'Fachbereichsaufgaben bearbeiten', 'Darf Fachbereichsaufgaben der freigegebenen Abteilungen bearbeiten.', 'department', 'tasks', true, '2026-04-27 07:07:10.281549+00'),
	(14, 'tasks.assign.override', 'Aufgaben umverteilen', 'Darf Aufgaben administrativ umverteilen.', 'global', 'tasks', true, '2026-04-27 07:07:10.281549+00'),
	(15, 'admin.directory.manage', 'Verzeichnisverwaltung', 'Darf Entra-Sync und Gruppen-Mappings pflegen.', 'global', 'admin', true, '2026-04-27 07:07:10.281549+00'),
	(16, 'admin.permissions.manage', 'Berechtigungen verwalten', 'Darf Rollen-Bundles und Benutzer-Overrides pflegen.', 'global', 'admin', true, '2026-04-27 07:07:10.281549+00');


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
	(28, 2, 'ob_has_ad_account', 'AD-Konto vorhanden?', 'Zugänge', 'Hat die Person ein aktives AD-Konto, das deaktiviert werden muss?', 'ad_user', 'boolean', true, 1, true),
	(29, 2, 'ob_has_mailbox', 'Mailbox vorhanden?', 'Zugänge', 'Hat die Person eine Mailbox, die deaktiviert werden muss?', 'mailbox', 'boolean', false, 2, true),
	(30, 2, 'ob_has_hardware', 'Hardware zurückzugeben?', 'Ausstattung', 'Hat die Person Hardware (Laptop, Workstation, etc.), die eingezogen werden muss?', 'pc', 'boolean', true, 3, true),
	(31, 2, 'ob_has_phone', 'Telefon zurückzugeben?', 'Ausstattung', 'Hat die Person ein tragbares Telefon, das eingezogen werden muss?', 'phone', 'boolean', false, 4, true),
	(32, 2, 'ob_has_habel', 'Habel-Zugang vorhanden?', 'Programme und Systeme', 'Hat die Person einen aktiven Habel-User?', 'habel', 'boolean', false, 5, true),
	(33, 2, 'ob_has_ln', 'InforLN-Zugang vorhanden?', 'Programme und Systeme', 'Hat die Person einen aktiven InforLN-User?', 'inforln', 'boolean', false, 6, true),
	(34, 2, 'ob_has_babtec', 'Babtec-Zugang vorhanden?', 'Programme und Systeme', 'Hat die Person einen aktiven Babtec-User?', 'babtec', 'boolean', false, 7, true),
	(35, 2, 'ob_has_gewatec', 'Gewatec-Zugang vorhanden?', 'Programme und Systeme', 'Hat die Person einen aktiven Gewatec-User?', 'gewatec', 'boolean', false, 8, true),
	(36, 2, 'ob_has_provis', 'Provis-Zugang vorhanden?', 'Programme und Systeme', 'Hat die Person einen aktiven Provis-User?', 'berechtigungen', 'boolean', false, 9, true),
	(38, 2, 'ob_exit_interview', 'Austrittsgespräch führen?', 'Abschluss', 'Soll ein Austrittsgespräch mit der ausscheidenden Person geführt werden?', 'identitat', 'boolean', true, 11, true),
	(39, 2, 'ob_knowledge_transfer', 'Wissenstransfer notwendig?', 'Abschluss', 'Muss vor dem Austritt ein strukturierter Wissenstransfer stattfinden?', 'identitat', 'boolean', false, 12, true),
	(40, 3, 'dc_new_department', 'Neue Abteilung', 'Wechseldetails', 'Name der Zielabteilung, in die der Mitarbeiter wechselt.', 'identitat', 'text', true, 1, true),
	(41, 3, 'dc_change_date', 'Wechseldatum', 'Wechseldetails', 'Geplanter Termin des Abteilungswechsels (z. B. 2025-07-01).', 'identitat', 'text', true, 2, true),
	(42, 3, 'dc_ad_group_change', 'AD-Gruppen anpassen?', 'Zugänge', 'Müssen AD-Gruppen und Berechtigungen an die neue Abteilung angepasst werden?', 'ad_user', 'boolean', true, 3, true),
	(43, 3, 'dc_drive_access_change', 'Laufwerk-Zugänge anpassen?', 'Zugänge', 'Müssen Netzlaufwerk-Zugriffsrechte für die neue Abteilung geändert werden?', 'pc', 'boolean', true, 4, true),
	(44, 3, 'dc_email_alias_change', 'E-Mail Alias anpassen?', 'Zugänge', 'Muss der E-Mail Alias wegen Abteilungsbezug im Mailnamen geändert werden?', 'mailbox', 'boolean', false, 5, true),
	(45, 3, 'dc_hardware_change', 'Hardware-Tausch notwendig?', 'Ausstattung', 'Muss die Hardware (z. B. stationär ↔ mobil) aufgrund der neuen Abteilung getauscht werden?', 'pc', 'boolean', false, 6, true),
	(37, 2, 'ob_has_consense', 'Consense-Zugang vorhanden?', 'Programme und Systeme', 'Hat die Person einen aktiven Consense-User?', 'consense', 'boolean', false, 10, true),
	(11, 1, 'hardware_takeover_details', 'Zu übernehmende Hardware', 'Ausstattung', 'Welche vorhandene Hardware wird übernommen? Bitte z. B. Rechnernummer, Asset-ID oder kurzen Hinweis angeben.', 'pc', 'text', false, 10, true),
	(46, 3, 'dc_has_habel', 'Habel-Zugang anpassen?', 'Programme und Systeme', 'Muss der Habel-Zugang für die neue Abteilung angepasst oder neu eingerichtet werden?', 'habel', 'boolean', false, 7, true),
	(47, 3, 'dc_has_ln', 'InforLN-Zugang anpassen?', 'Programme und Systeme', 'Muss der InforLN-Zugang für die neue Abteilung angepasst oder neu eingerichtet werden?', 'inforln', 'boolean', false, 8, true),
	(48, 3, 'dc_has_babtec', 'Babtec-Zugang anpassen?', 'Programme und Systeme', 'Muss der Babtec-Zugang für die neue Abteilung angepasst oder neu eingerichtet werden?', 'babtec', 'boolean', false, 9, true),
	(49, 3, 'dc_has_gewatec', 'Gewatec-Zugang anpassen?', 'Programme und Systeme', 'Muss der Gewatec-Zugang für die neue Abteilung angepasst oder neu eingerichtet werden?', 'gewatec', 'boolean', false, 10, true),
	(50, 3, 'dc_has_provis', 'Provis-Zugang anpassen?', 'Programme und Systeme', 'Muss der Provis-Zugang für die neue Abteilung angepasst oder neu eingerichtet werden?', 'berechtigungen', 'boolean', false, 11, true),
	(52, 7, 'nc_new_first_name', 'Neuer Vorname', 'Namensaenderung', 'Neuer gueltiger Vorname der betroffenen Person.', 'identitat', 'text', true, 1, true);
INSERT INTO public.workflow_answer_definitions OVERRIDING SYSTEM VALUE VALUES
	(53, 7, 'nc_new_last_name', 'Neuer Nachname', 'Namensaenderung', 'Neuer gueltiger Nachname der betroffenen Person.', 'identitat', 'text', true, 2, true),
	(54, 7, 'nc_effective_date', 'Wirksamkeitsdatum', 'Namensaenderung', 'Datum, ab dem der neue Name in allen Systemen gelten soll.', 'identitat', 'text', true, 3, true),
	(55, 8, 'pc_new_position', 'Neue Position / Rolle', 'Wechseldetails', 'Neue Position oder Rolle, die die Person kuenftig ausueben soll.', 'identitat', 'text', true, 1, true),
	(56, 8, 'pc_change_date', 'Wechseldatum', 'Wechseldetails', 'Datum, ab dem die neue Position wirksam wird.', 'identitat', 'text', true, 2, true),
	(57, 8, 'pc_permission_change', 'Berechtigungen anpassen?', 'Berechtigungen', 'Muessen allgemeine Berechtigungen und Zugriffsprofile wegen der neuen Position angepasst werden?', 'ad_user', 'boolean', true, 3, true),
	(58, 8, 'pc_training_required', 'Neue Schulungen erforderlich?', 'Qualifizierung', 'Sind fuer die neue Position neue Schulungen oder Einweisungen notwendig?', 'identitat', 'boolean', false, 4, true),
	(59, 8, 'pc_ad_groups_change', 'AD-Gruppen anpassen?', 'Zugaenge', 'Muessen AD-Gruppen und Rollen fuer die neue Position geaendert werden?', 'ad_user', 'boolean', false, 5, true),
	(60, 8, 'pc_drive_access_change', 'Laufwerk-Zugaenge anpassen?', 'Zugaenge', 'Muessen Laufwerks- und Datei-Zugriffe an die neue Position angepasst werden?', 'pc', 'boolean', false, 6, true),
	(61, 8, 'pc_mail_alias_change', 'Mailbox oder Alias anpassen?', 'Zugaenge', 'Muessen Mailbox-bezogene Sichtbarkeit oder Aliasdaten geaendert werden?', 'mailbox', 'boolean', false, 7, true),
	(62, 8, 'pc_has_habel', 'Habel-Zugang anpassen?', 'Programme und Systeme', 'Muessen Habel-Berechtigungen wegen der neuen Position angepasst werden?', 'habel', 'boolean', false, 8, true),
	(63, 8, 'pc_has_ln', 'InforLN-Zugang anpassen?', 'Programme und Systeme', 'Muessen InforLN-Berechtigungen wegen der neuen Position angepasst werden?', 'inforln', 'boolean', false, 9, true),
	(64, 8, 'pc_has_babtec', 'Babtec-Zugang anpassen?', 'Programme und Systeme', 'Muessen Babtec-Berechtigungen wegen der neuen Position angepasst werden?', 'babtec', 'boolean', false, 10, true),
	(65, 8, 'pc_has_gewatec', 'Gewatec-Zugang anpassen?', 'Programme und Systeme', 'Muessen Gewatec-Berechtigungen wegen der neuen Position angepasst werden?', 'gewatec', 'boolean', false, 11, true),
	(66, 8, 'pc_has_provis', 'Provis-Zugang anpassen?', 'Programme und Systeme', 'Muessen Provis-Berechtigungen wegen der neuen Position angepasst werden?', 'berechtigungen', 'boolean', false, 12, true),
	(68, 9, 'rc_new_role', 'Neue Rolle', 'Rollendetails', 'Neue Rolle oder Berechtigungsfunktion, die die Person kuenftig erhalten soll.', 'identitat', 'text', true, 1, true),
	(69, 9, 'rc_effective_date', 'Wirksamkeitsdatum', 'Rollendetails', 'Datum, ab dem die neue Rolle wirksam wird.', 'identitat', 'text', true, 2, true),
	(70, 9, 'rc_role_assignment_change', 'Rollen-Zuweisung anpassen?', 'Berechtigungen', 'Muessen fachliche oder technische Rollen explizit neu zugewiesen oder entzogen werden?', 'ad_user', 'boolean', true, 3, true),
	(71, 9, 'rc_permission_change', 'Weitere Berechtigungen anpassen?', 'Berechtigungen', 'Muessen zusaetzliche Berechtigungen oder Profile an die neue Rolle angepasst werden?', 'berechtigungen', 'boolean', false, 4, true),
	(72, 9, 'rc_ad_groups_change', 'AD-Gruppen anpassen?', 'Zugaenge', 'Muessen AD-Gruppen und Verzeichnisrollen an die neue Rolle angepasst werden?', 'ad_user', 'boolean', false, 5, true),
	(73, 9, 'rc_mailbox_change', 'Mailbox oder Alias anpassen?', 'Zugaenge', 'Muessen mailboxbezogene Sichtbarkeit oder Aliasrechte geaendert werden?', 'mailbox', 'boolean', false, 6, true),
	(74, 9, 'rc_has_habel', 'Habel-Zugang anpassen?', 'Programme und Systeme', 'Muessen Habel-Rollen oder Berechtigungen wegen des Rollenwechsels angepasst werden?', 'habel', 'boolean', false, 7, true),
	(75, 9, 'rc_has_ln', 'InforLN-Zugang anpassen?', 'Programme und Systeme', 'Muessen InforLN-Rollen oder Berechtigungen wegen des Rollenwechsels angepasst werden?', 'inforln', 'boolean', false, 8, true),
	(76, 9, 'rc_has_babtec', 'Babtec-Zugang anpassen?', 'Programme und Systeme', 'Muessen Babtec-Rollen oder Berechtigungen wegen des Rollenwechsels angepasst werden?', 'babtec', 'boolean', false, 9, true),
	(77, 9, 'rc_has_gewatec', 'Gewatec-Zugang anpassen?', 'Programme und Systeme', 'Muessen Gewatec-Rollen oder Berechtigungen wegen des Rollenwechsels angepasst werden?', 'gewatec', 'boolean', false, 10, true),
	(78, 9, 'rc_has_provis', 'Provis-Zugang anpassen?', 'Programme und Systeme', 'Muessen Provis-Rollen oder Berechtigungen wegen des Rollenwechsels angepasst werden?', 'berechtigungen', 'boolean', false, 11, true),
	(24, 1, 'consense_requested', 'Consense-User anlegen?', 'Programme und Systeme', 'Soll fuer die neue Person ein Consense-User angelegt werden?', 'consense', 'boolean', false, 20, true),
	(51, 3, 'dc_has_consense', 'Consense-Zugang anpassen?', 'Programme und Systeme', 'Muss der Consense-Zugang fuer die neue Abteilung angepasst oder neu eingerichtet werden?', 'consense', 'boolean', false, 12, true),
	(67, 8, 'pc_has_consense', 'Consense-Zugang anpassen?', 'Programme und Systeme', 'Muessen Consense-Berechtigungen wegen der neuen Position angepasst werden?', 'consense', 'boolean', false, 13, true),
	(79, 9, 'rc_has_consense', 'Consense-Zugang anpassen?', 'Programme und Systeme', 'Muessen Consense-Rollen oder Berechtigungen wegen des Rollenwechsels angepasst werden?', 'consense', 'boolean', false, 12, true);


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
	(1, 1, '2026-04-27 07:07:10.284358+00'),
	(2, 16, '2026-04-27 07:07:10.284358+00'),
	(2, 15, '2026-04-27 07:07:10.284358+00'),
	(2, 14, '2026-04-27 07:07:10.284358+00'),
	(2, 13, '2026-04-27 07:07:10.284358+00'),
	(2, 12, '2026-04-27 07:07:10.284358+00'),
	(2, 11, '2026-04-27 07:07:10.284358+00'),
	(2, 10, '2026-04-27 07:07:10.284358+00'),
	(2, 9, '2026-04-27 07:07:10.284358+00'),
	(2, 8, '2026-04-27 07:07:10.284358+00'),
	(2, 7, '2026-04-27 07:07:10.284358+00'),
	(2, 6, '2026-04-27 07:07:10.284358+00'),
	(2, 5, '2026-04-27 07:07:10.284358+00'),
	(2, 3, '2026-04-27 07:07:10.284358+00'),
	(2, 1, '2026-04-27 07:07:10.284358+00'),
	(3, 13, '2026-04-27 07:07:10.284358+00'),
	(3, 1, '2026-04-27 07:07:10.284358+00'),
	(4, 12, '2026-04-27 07:07:10.284358+00'),
	(4, 11, '2026-04-27 07:07:10.284358+00'),
	(4, 10, '2026-04-27 07:07:10.284358+00'),
	(4, 9, '2026-04-27 07:07:10.284358+00'),
	(4, 8, '2026-04-27 07:07:10.284358+00'),
	(4, 4, '2026-04-27 07:07:10.284358+00'),
	(4, 2, '2026-04-27 07:07:10.284358+00'),
	(4, 1, '2026-04-27 07:07:10.284358+00'),
	(5, 11, '2026-04-27 07:07:10.284358+00'),
	(5, 10, '2026-04-27 07:07:10.284358+00'),
	(5, 9, '2026-04-27 07:07:10.284358+00'),
	(5, 8, '2026-04-27 07:07:10.284358+00'),
	(5, 7, '2026-04-27 07:07:10.284358+00'),
	(5, 6, '2026-04-27 07:07:10.284358+00'),
	(5, 5, '2026-04-27 07:07:10.284358+00'),
	(5, 3, '2026-04-27 07:07:10.284358+00'),
	(5, 1, '2026-04-27 07:07:10.284358+00');


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
	(2, 'offboarding', 'Offboarding', 'Business-phase workflow definition for offboarding.', false, false, true, NULL, '2026-04-27 07:07:10.336213+00', '2026-04-27 07:07:10.374109+00'),
	(3, 'department_change', 'Abteilungswechsel', 'Business-phase workflow definition for department changes.', true, false, true, NULL, '2026-04-27 07:07:10.339459+00', '2026-04-27 07:07:10.377381+00'),
	(7, 'name_change', 'Namensaenderung', 'Business-phase workflow definition for name changes.', true, false, true, NULL, '2026-04-27 07:07:10.385153+00', '2026-04-27 07:07:10.385153+00'),
	(8, 'position_change', 'Positionswechsel', 'Business-phase workflow definition for position changes.', true, false, true, NULL, '2026-04-27 07:07:10.389326+00', '2026-04-27 07:07:10.389326+00'),
	(9, 'role_change', 'Rollenwechsel', 'Business-phase workflow definition for role changes.', true, false, true, NULL, '2026-04-27 07:07:10.392633+00', '2026-04-27 07:07:10.392633+00'),
	(1, 'onboarding', 'Onboarding', 'Business-phase workflow definition for onboarding.', false, true, false, 'supervisor_fills_document', '2026-04-27 07:07:10.326548+00', '2026-04-27 07:07:10.396926+00');


--
-- Data for Name: workflow_definition_versions; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.workflow_definition_versions OVERRIDING SYSTEM VALUE VALUES
	(2, 2, 1, 'published', 'Offboarding Standard', 'Published offboarding mapping with a deprovision measure block and internal task generation.', '2026-04-27 07:07:10.336213+00', '2026-04-27 07:07:10.374109+00', '2026-04-27 07:07:10.336213+00'),
	(3, 3, 1, 'published', 'Abteilungswechsel Standard', 'Published department change mapping with a change measure block and internal task generation.', '2026-04-27 07:07:10.339459+00', '2026-04-27 07:07:10.377381+00', '2026-04-27 07:07:10.339459+00'),
	(4, 7, 1, 'published', 'Namensaenderung Standard', 'Published name change mapping with a rename measure block and internal task generation.', '2026-04-27 07:07:10.385153+00', '2026-04-27 07:07:10.385153+00', '2026-04-27 07:07:10.385153+00'),
	(5, 8, 1, 'published', 'Positionswechsel Standard', 'Published position change mapping with a change measure block and internal task generation.', '2026-04-27 07:07:10.389326+00', '2026-04-27 07:07:10.389326+00', '2026-04-27 07:07:10.389326+00'),
	(6, 9, 1, 'published', 'Rollenwechsel Standard', 'Published role change mapping with a change measure block and internal task generation.', '2026-04-27 07:07:10.392633+00', '2026-04-27 07:07:10.392633+00', '2026-04-27 07:07:10.392633+00'),
	(1, 1, 1, 'published', 'Onboarding Standard', 'Published onboarding mapping with a provision measure block and internal task generation.', '2026-04-27 07:07:10.326548+00', '2026-04-27 07:07:10.396926+00', '2026-04-27 07:07:10.326548+00');


--
-- Data for Name: workflow_nodes; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.workflow_nodes OVERRIDING SYSTEM VALUE VALUES
	(19, 2, 'start', 'start', 'Start', 0, NULL, NULL, '2026-04-27 07:07:10.374109+00'),
	(20, 2, 'collect_requirements', 'form', 'Offboarding-Umfang erfassen', 10, NULL, NULL, '2026-04-27 07:07:10.374109+00'),
	(21, 2, 'department_setup', 'measure_deprovision', 'Entzugsmaßnahmen erzeugen', 20, NULL, NULL, '2026-04-27 07:07:10.374109+00'),
	(22, 2, 'end', 'end', 'Abschluss', 30, NULL, NULL, '2026-04-27 07:07:10.374109+00'),
	(23, 3, 'start', 'start', 'Start', 0, NULL, NULL, '2026-04-27 07:07:10.377381+00'),
	(24, 3, 'collect_requirements', 'form', 'Wechselumfang erfassen', 10, NULL, NULL, '2026-04-27 07:07:10.377381+00'),
	(25, 3, 'department_setup', 'measure_change', 'Änderungsmaßnahmen erzeugen', 20, NULL, NULL, '2026-04-27 07:07:10.377381+00'),
	(26, 3, 'end', 'end', 'Abschluss', 30, NULL, NULL, '2026-04-27 07:07:10.377381+00'),
	(27, 4, 'start', 'start', 'Start', 0, NULL, NULL, '2026-04-27 07:07:10.385153+00'),
	(28, 4, 'collect_requirements', 'form', 'Namensänderung erfassen', 10, NULL, NULL, '2026-04-27 07:07:10.385153+00'),
	(29, 4, 'department_setup', 'measure_rename', 'Umbenennungsmaßnahmen erzeugen', 20, NULL, NULL, '2026-04-27 07:07:10.385153+00'),
	(30, 4, 'end', 'end', 'Abschluss', 30, NULL, NULL, '2026-04-27 07:07:10.385153+00'),
	(31, 5, 'start', 'start', 'Start', 0, NULL, NULL, '2026-04-27 07:07:10.389326+00'),
	(32, 5, 'collect_requirements', 'form', 'Positionswechsel erfassen', 10, NULL, NULL, '2026-04-27 07:07:10.389326+00'),
	(33, 5, 'department_setup', 'measure_change', 'Änderungsmaßnahmen erzeugen', 20, NULL, NULL, '2026-04-27 07:07:10.389326+00'),
	(34, 5, 'end', 'end', 'Abschluss', 30, NULL, NULL, '2026-04-27 07:07:10.389326+00'),
	(35, 6, 'start', 'start', 'Start', 0, NULL, NULL, '2026-04-27 07:07:10.392633+00'),
	(36, 6, 'collect_requirements', 'form', 'Rollenwechsel erfassen', 10, NULL, NULL, '2026-04-27 07:07:10.392633+00'),
	(37, 6, 'department_setup', 'measure_change', 'Änderungsmaßnahmen erzeugen', 20, NULL, NULL, '2026-04-27 07:07:10.392633+00'),
	(38, 6, 'end', 'end', 'Abschluss', 30, NULL, NULL, '2026-04-27 07:07:10.392633+00'),
	(39, 1, 'start', 'start', 'Start', 0, NULL, NULL, '2026-04-27 07:07:10.396926+00'),
	(40, 1, 'collect_requirements', 'form', 'Anforderungen erfassen', 10, NULL, NULL, '2026-04-27 07:07:10.396926+00'),
	(41, 1, 'department_setup', 'measure_provision', 'Bereitstellungsmaßnahmen erzeugen', 20, NULL, NULL, '2026-04-27 07:07:10.396926+00'),
	(42, 1, 'end', 'end', 'Abschluss', 30, NULL, NULL, '2026-04-27 07:07:10.396926+00');


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

INSERT INTO public.department_action_templates OVERRIDING SYSTEM VALUE VALUES
	(1, 4, 'enter', 'Ordnerzugänge Vertrieb einrichten', 'Folgende Netzlaufwerke freischalten:
- H:\Vertrieb\Projektmanagement
- H:\Entwicklung\Projekte aktiv
- G:\KauthGroup\Vertrieb\04_Projekte\01_Arbeitsdatei
- G:\KauthGroup\Vertrieb\04_Projekte\02_Werkzeugdoku
- G:\KauthGroup\Vertrieb\Beauftragungsblätter', 'technical', 12, -5, 1, false, NULL, true, '2026-04-27 07:07:10.435045+00', '2026-04-27 07:07:10.435045+00'),
	(2, 4, 'enter', 'PRO.FILE Zugang einrichten', 'PRO.FILE-Zugang für den Einsatz im Vertrieb anlegen.', 'technical', 12, -5, 1, false, NULL, true, '2026-04-27 07:07:10.435045+00', '2026-04-27 07:07:10.435045+00'),
	(3, 4, 'enter', 'SpinFire Viewer installieren', 'SpinFire Viewer für die Einsicht in 3D-Konstruktionsdaten bereitstellen.', 'technical', 12, -5, 1, false, NULL, true, '2026-04-27 07:07:10.435045+00', '2026-04-27 07:07:10.435045+00'),
	(4, 4, 'enter', 'Infor LN Zugang einrichten', 'LN-Benutzerkonto für Vertrieb / Projektmanagement anlegen.', 'technical', 9, -5, 1, false, NULL, true, '2026-04-27 07:07:10.435045+00', '2026-04-27 07:07:10.435045+00'),
	(5, 3, 'enter', 'Ordnerzugang WZB einrichten', 'Netzlaufwerk freischalten:
- H:\WZB (alles außer Austausch-Ordner)', 'technical', 12, -5, 1, false, NULL, true, '2026-04-27 07:07:10.438973+00', '2026-04-27 07:07:10.438973+00'),
	(6, 3, 'enter', 'PRO.FILE Zugang einrichten', 'PRO.FILE-Zugang für Werkzeugdokumentation anlegen.', 'technical', 12, -5, 1, false, NULL, true, '2026-04-27 07:07:10.438973+00', '2026-04-27 07:07:10.438973+00'),
	(7, 3, 'enter', 'Bambu Studio installieren', 'Nur für technische Ausbildungen. Achtung: Software muss nach Stationsende wieder deinstalliert werden.', 'technical', 12, -5, 1, false, NULL, true, '2026-04-27 07:07:10.438973+00', '2026-04-27 07:07:10.438973+00'),
	(8, 3, 'exit', 'Bambu Studio deinstallieren', 'Bambu Studio nach Stationsende vom Gerät entfernen.', 'technical', 12, 0, 1, false, NULL, true, '2026-04-27 07:07:10.438973+00', '2026-04-27 07:07:10.438973+00'),
	(9, 3, 'enter', 'Infor LN Zugang einrichten', 'LN-Benutzerkonto für den Einsatz im Werkzeugbau / WZB anlegen.', 'technical', 9, -5, 1, false, NULL, true, '2026-04-27 07:07:10.438973+00', '2026-04-27 07:07:10.438973+00'),
	(10, 1, 'enter', 'Ordnerzugänge Logistik / Verpackerei einrichten', 'Folgende Netzlaufwerke freischalten:
- H:\Logistik\Verpackerei\05_Verpackungsvorschriften und -katalog\00_Verpackungsvorschriften Zentraleinkauf
- H:\Logistik\Verpackerei\04_Packlisten', 'technical', 12, -5, 1, false, NULL, true, '2026-04-27 07:07:10.441688+00', '2026-04-27 07:07:10.441688+00'),
	(11, 1, 'enter', 'E-Mail Adresse einrichten', 'E-Mail-Konto für den Stationszeitraum einrichten.', 'technical', 11, -5, 1, false, NULL, true, '2026-04-27 07:07:10.441688+00', '2026-04-27 07:07:10.441688+00'),
	(12, 1, 'enter', 'Infor LN Zugang einrichten (Versand)', 'LN-Benutzerkonto für den Einsatz im Versandbereich anlegen.', 'technical', 9, -5, 1, false, NULL, true, '2026-04-27 07:07:10.441688+00', '2026-04-27 07:07:10.441688+00'),
	(13, 2, 'enter', 'ConSense Zugang einrichten', 'ConSense-Zugang für den Einsatz im Einkauf einrichten.', 'technical', 1, -5, 1, false, NULL, true, '2026-04-27 07:07:10.44377+00', '2026-04-27 07:07:10.44377+00'),
	(14, 2, 'enter', 'Ordnerzugang QS Materialspezifikation einrichten', 'Netzlaufwerk freischalten:
- H:\QS\Materialspezifikation\3 freigegeben', 'technical', 12, -5, 1, false, NULL, true, '2026-04-27 07:07:10.44377+00', '2026-04-27 07:07:10.44377+00'),
	(15, 2, 'enter', 'PRO.FILE Zugang einrichten', 'Ggfs. erforderlich – Rücksprache mit Einkaufsleitung vor Einrichtung.', 'technical', 12, -5, 1, false, NULL, true, '2026-04-27 07:07:10.44377+00', '2026-04-27 07:07:10.44377+00'),
	(16, 2, 'enter', 'Outlook einrichten', 'E-Mail-Konto (Outlook) für den Stationszeitraum im Einkauf einrichten.', 'technical', 11, -5, 1, false, NULL, true, '2026-04-27 07:07:10.44377+00', '2026-04-27 07:07:10.44377+00'),
	(17, 2, 'enter', 'HABEL Zugang einrichten', 'HABEL-Benutzerkonto für Einkauf und HABEL Recherche anlegen.', 'technical', 10, -5, 1, false, NULL, true, '2026-04-27 07:07:10.44377+00', '2026-04-27 07:07:10.44377+00'),
	(18, 2, 'enter', 'Infor LN Zugang einrichten', 'LN-Benutzerkonto anlegen. Referenzuser für Berechtigungsübernahme: Martin / Bertsche (Rücksprache mit Einkaufsleitung).', 'technical', 9, -5, 1, false, NULL, true, '2026-04-27 07:07:10.44377+00', '2026-04-27 07:07:10.44377+00'),
	(19, 5, 'enter', 'Ordnerzugänge Fertigungssteuerung einrichten', 'Folgende Netzlaufwerke freischalten:
- H:\Logistik\Fertigungssteuerung (kompletter Ordner)
- U:\User\PROD\000 Produktion - Neue Ordnerstruktur\Gemeinsamer Arbeitsordner\10 Planung FST\20 Produktionslisten
- U:\User\PROD\000 Produktion - Neue Ordnerstruktur\ProdW- Werkzeugwartung\60 Arbeitsvorbereitung\20 Planung Pressenbereich
- H:\Logistik\Wareneingang\03_Lager\03_Rohmaterial\00_Rohmaterialverfolgung', 'technical', 12, -5, 1, false, NULL, true, '2026-04-27 07:07:10.446561+00', '2026-04-27 07:07:10.446561+00'),
	(20, 5, 'enter', 'Ultra VNC Viewer einrichten', 'Ultra VNC Viewer für Fernzugriff im Produktionsbereich installieren und konfigurieren.', 'technical', 12, -5, 1, false, NULL, true, '2026-04-27 07:07:10.446561+00', '2026-04-27 07:07:10.446561+00');


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

INSERT INTO public.notification_email_settings VALUES
	(1, false, NULL, 'http://localhost:5173', NULL, NULL, true, true, true, 'never', NULL, NULL, '2026-04-27 07:07:09.746768+00', '2026-04-27 07:07:09.746768+00');


--
-- Data for Name: notification_templates; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.notification_templates VALUES
	('workflow_created', 'Vorgang gestartet', 'Wird ausgelöst, wenn ein neuer Vorgang gestartet wurde und Empfänger für den Startfall auflösbar sind.', '{{workflow_label}} gestartet', 'Ein neuer {{workflow_label}} wurde gestartet und wartet auf Ihre Bearbeitung.

Bitte öffnen Sie den Vorgang über den folgenden Link.', false, '2026-04-27 07:07:10.505153+00', '2026-04-27 07:07:10.518072+00'),
	('task_ready', 'Aufgabe bereit', 'Wird ausgelöst, wenn aktuell benachrichtigbare offene oder bereitstehende Aufgaben für einen Vorgang vorhanden sind.', 'Neue Aufgaben für {{recipient_name}}', 'Für Sie wurden im {{process_label}} Aufgaben vorbereitet.

{{task_list_text}}

Bitte öffnen Sie Ihren Arbeitsbereich über den folgenden Link und bearbeiten Sie den Vorgang.', false, '2026-04-27 07:07:10.505153+00', '2026-04-27 07:07:10.518072+00'),
	('workflow_completed', 'Vorgang abgeschlossen', 'Wird ausgelöst, wenn ein Vorgang abgeschlossen ist und der Initiator aktuell benachrichtigt werden kann.', '{{process_name}} abgeschlossen', 'Der von Ihnen gestartete {{workflow_label}} wurde abgeschlossen.

Sie können den Vorgang bei Bedarf über den folgenden Link öffnen.', false, '2026-04-27 07:07:10.505153+00', '2026-04-27 07:07:10.518072+00'),
	('upcoming_change', 'Bevorstehender Wechsel', 'Wird ausgelöst, wenn für einen aktiven Durchlaufplan aktuell ein bevorstehender Bereichswechsel benachrichtigt werden soll.', 'Bevorstehender Wechsel: {{person_name}}', 'Für den Durchlaufplan {{plan_title}} von {{person_name}} steht ein Bereichswechsel bevor.

- Aktueller Bereich: {{current_department_name}}
- Nächster Bereich: {{next_department_name}}
- Wechseltermin: {{change_date}}

{{task_list_text}}

Bitte öffnen Sie die Anwendung über den folgenden Link.', false, '2026-04-27 07:07:10.505153+00', '2026-04-27 07:07:10.518072+00'),
	('reminder', 'Erinnerung fällige Aufgaben', 'Wird ausgelöst, wenn für einen aktiven Durchlaufplan heute fällige Rotationsaufgaben benachrichtigt werden sollen.', 'Fällige Rotationsaufgaben für {{person_name}}', 'Für den Durchlaufplan {{plan_title}} von {{person_name}} haben offene Aufgaben heute ihren Fälligkeitstermin erreicht.

- Aktueller Bereich: {{current_department_name}}
- Nächster Bereich: {{next_department_name}}
- Wechseltermin: {{change_date}}

{{task_list_text}}

Bitte öffnen Sie die Anwendung über den folgenden Link.', false, '2026-04-27 07:07:10.505153+00', '2026-04-27 07:07:10.518072+00'),
	('overdue', 'Überfällige Aufgaben', 'Wird ausgelöst, wenn für einen aktiven Durchlaufplan aktuell überfällige Rotationsaufgaben vorhanden sind.', 'Überfällige Rotationsaufgaben für {{person_name}}', 'Für den Durchlaufplan {{plan_title}} von {{person_name}} gibt es offene Rotationsaufgaben mit überschrittenem Fälligkeitstermin.

- Aktueller Bereich: {{current_department_name}}
- Nächster Bereich: {{next_department_name}}
- Wechseltermin: {{change_date}}

{{task_list_text}}

Bitte öffnen Sie die Anwendung über den folgenden Link.', false, '2026-04-27 07:07:10.505153+00', '2026-04-27 07:07:10.518072+00');


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
-- LA5-F: workflow_node_task_specs replaces task_templates. spec_key + workflow_node_id
-- (measure-Node der published Version) sind die kanonische Identitaet.
--

INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (1, 21, 'ob_gewatec_user_disable', 'Gewatec-User deaktivieren', 'Gewatec-Zugang der ausscheidenden Person deaktivieren.', 'Fachanwendungen', 'gewatec', 4, 'AV', true, true, 2, 210, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (2, 21, 'ob_provis_user_disable', 'Provis-User deaktivieren', 'Provis-Zugang der ausscheidenden Person deaktivieren.', 'Fachanwendungen', 'berechtigungen', 3, 'AV', true, true, 2, 220, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (3, 21, 'ob_ad_account_disable', 'AD-Konto deaktivieren', 'AD-Konto der ausscheidenden Person deaktivieren und Berechtigungen entziehen.', 'Zugänge', 'ad_user', 12, 'IT', true, true, 1, 100, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (4, 21, 'ob_mailbox_disable', 'Mailbox deaktivieren', 'Mailbox der ausscheidenden Person deaktivieren.', 'Zugänge', 'mailbox', 11, 'IT', true, true, 1, 110, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (5, 21, 'ob_habel_user_disable', 'Habel-User deaktivieren', 'Habel-Zugang der ausscheidenden Person sperren.', 'Fachanwendungen', 'habel', 10, 'IT', true, true, 2, 120, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (6, 21, 'ob_ln_user_disable', 'LN-User deaktivieren', 'InforLN-Zugang der ausscheidenden Person sperren.', 'Fachanwendungen', 'react', 9, 'IT', true, true, 2, 130, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (7, 21, 'ob_hardware_return', 'Hardware einziehen', 'Hardware (Laptop, Workstation, Zubehör) der ausscheidenden Person einziehen und auf Vollständigkeit prüfen.', 'Ausstattung', 'pc', 8, 'IT', true, true, 1, 140, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (8, 21, 'ob_phone_return', 'Telefon einziehen', 'Tragbares Telefon der ausscheidenden Person einziehen.', 'Ausstattung', 'phone', 8, 'IT', true, true, 1, 150, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (9, 21, 'ob_last_day_confirmed', 'Letzten Arbeitstag bestätigen', 'Letzten Arbeitstag der ausscheidenden Person im System bestätigen. Schaltet alle Zugangs-Entzug-Aufgaben frei.', 'HR', 'identitat', 15, 'HR', true, true, 1, 10, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (10, 21, 'ob_exit_interview', 'Austrittsgespräch führen', 'Strukturiertes Abschlussgespräch mit der ausscheidenden Person führen und dokumentieren.', 'HR', 'identitat', 15, 'HR', true, true, 5, 20, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (11, 21, 'ob_knowledge_transfer', 'Wissenstransfer organisieren', 'Sicherstellen, dass kritisches Wissen und laufende Aufgaben an Nachfolger oder Team übergeben werden.', 'HR', 'identitat', 15, 'HR', true, true, 5, 30, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (12, 21, 'ob_badge_key_return', 'Schlüssel und Badge zurückgeben', 'Ausweis, Schlüssel und sonstige Zugangsmittel von der ausscheidenden Person einziehen.', 'HR', 'identitat', 15, 'HR', true, true, 1, 40, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (13, 21, 'ob_babtec_user_disable', 'Babtec-User deaktivieren', 'Babtec-Zugang der ausscheidenden Person deaktivieren.', 'Fachanwendungen', 'babtec', 16, 'QS', true, true, 2, 200, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (14, 21, 'ob_consense_user_disable', 'Consense-User deaktivieren', 'Consense-Zugang der ausscheidenden Person deaktivieren.', 'Fachanwendungen', 'consense', 1, 'QMB', true, true, 2, 230, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (15, 25, 'dc_habel_access_update', 'Habel-Zugang anpassen', 'Habel-Berechtigungen auf die neue Abteilung umstellen.', 'Fachanwendungen', 'habel', 10, 'IT', true, true, 3, 130, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (16, 25, 'dc_gewatec_access_update', 'Gewatec-Zugang anpassen', 'Gewatec-Berechtigungen auf die neue Abteilung umstellen.', 'Fachanwendungen', 'gewatec', 4, 'AV', true, true, 3, 210, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (17, 25, 'dc_provis_access_update', 'Provis-Zugang anpassen', 'Provis-Berechtigungen auf die neue Abteilung umstellen.', 'Fachanwendungen', 'berechtigungen', 3, 'AV', true, true, 3, 220, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (18, 25, 'dc_ad_group_update', 'AD-Gruppen aktualisieren', 'AD-Gruppen und Berechtigungen auf die neue Abteilung umstellen, alte abteilungsspezifische Gruppen entfernen.', 'Zugänge', 'ad_user', 12, 'IT', true, true, 2, 100, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (19, 25, 'dc_consense_access_update', 'Consense-Zugang anpassen', 'Consense-Berechtigungen auf die neue Abteilung umstellen.', 'Fachanwendungen', 'consense', 1, 'QMB', true, true, 3, 230, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (20, 25, 'dc_drive_access_update', 'Laufwerk-Zugänge anpassen', 'Netzlaufwerk-Zugriffsrechte anpassen: Zugriff auf neue Abteilungs-Laufwerke gewähren, alte entziehen.', 'Zugänge', 'pc', 12, 'IT', true, true, 2, 110, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (21, 25, 'dc_email_alias_update', 'E-Mail Alias anpassen', 'E-Mail Alias des Mitarbeiters aktualisieren, falls die neue Abteilung einen anderen Kürzel erfordert.', 'Zugänge', 'mailbox', 11, 'IT', true, true, 3, 120, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (22, 25, 'dc_ln_access_update', 'InforLN-Zugang anpassen', 'InforLN-Berechtigungen auf die neue Abteilung umstellen.', 'Fachanwendungen', 'react', 9, 'IT', true, true, 3, 140, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (23, 25, 'dc_hardware_swap', 'Hardware tauschen', 'Hardware der neuen Arbeitsanforderungen entsprechend tauschen (z. B. stationär durch Laptop ersetzen).', 'Ausstattung', 'pc', 8, 'IT', true, true, 2, 150, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (24, 25, 'dc_change_date_confirmed', 'Wechseldatum bestätigen', 'Bestätigen, dass das Wechseldatum eingetroffen ist. Schaltet alle Zugangs- und Ausstattungsaufgaben frei.', 'HR', 'identitat', 15, 'HR', true, true, 1, 10, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (25, 25, 'dc_hr_system_update', 'Abteilung im HR-System aktualisieren', 'Abteilung des Mitarbeiters in der Personalakte und im HR-System auf die neue Abteilung umstellen.', 'HR', 'identitat', 15, 'HR', true, true, 3, 20, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (26, 25, 'dc_babtec_access_update', 'Babtec-Zugang anpassen', 'Babtec-Berechtigungen auf die neue Abteilung umstellen.', 'Fachanwendungen', 'babtec', 16, 'QS', true, true, 3, 200, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (27, 29, 'nc_ad_username_update', 'AD-Benutzername aktualisieren', 'AD-Benutzername, Anzeigename und verzeichnisbezogene Namensfelder auf den neuen Namen umstellen.', 'Zugaenge', 'ad_user', 12, 'IT', true, true, 2, 100, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (28, 29, 'nc_system_display_name_update', 'Anzeigenamen in Systemen aktualisieren', 'Anzeigenamen in angeschlossenen Systemen und Verzeichnissen auf den neuen Namen angleichen.', 'Systeme', 'berechtigungen', 12, 'IT', true, true, 3, 120, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (29, 29, 'nc_mailbox_update', 'Mailbox und Alias aktualisieren', 'Mailbox, primäre Adresse und Alias auf den neuen Namen umstellen.', 'Zugaenge', 'mailbox', 11, 'IT', true, true, 2, 110, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (30, 29, 'nc_effective_date_confirmed', 'Wirksamkeitsdatum bestaetigen', 'Bestaetigen, dass das Wirksamkeitsdatum erreicht ist. Schaltet die technischen Umstellungsaufgaben frei.', 'HR', 'identitat', 15, 'HR', true, true, 1, 10, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (31, 29, 'nc_hr_master_data_update', 'HR-Stammdaten aktualisieren', 'Neuen Namen in Personalakte und HR-Stammdaten pflegen.', 'HR', 'identitat', 15, 'HR', true, true, 2, 20, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (32, 33, 'pc_gewatec_access_update', 'Gewatec-Zugang anpassen', 'Gewatec-Berechtigungen auf die neue Position umstellen.', 'Fachanwendungen', 'gewatec', 4, 'AV', true, true, 3, 230, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (33, 33, 'pc_provis_access_update', 'Provis-Zugang anpassen', 'Provis-Berechtigungen auf die neue Position umstellen.', 'Fachanwendungen', 'berechtigungen', 3, 'AV', true, true, 3, 240, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (34, 33, 'pc_permission_profile_update', 'Berechtigungsprofil aktualisieren', 'Allgemeine Berechtigungsprofile und Freigaben an die neue Position anpassen.', 'Berechtigungen', 'ad_user', 12, 'IT', true, true, 2, 100, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (35, 33, 'pc_ad_groups_update', 'AD-Gruppen aktualisieren', 'AD-Gruppen und Rollen entsprechend der neuen Position anpassen.', 'Zugaenge', 'ad_user', 12, 'IT', true, true, 2, 120, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (36, 33, 'pc_drive_access_update', 'Laufwerk-Zugaenge anpassen', 'Datei- und Laufwerksberechtigungen auf die Anforderungen der neuen Position umstellen.', 'Zugaenge', 'pc', 12, 'IT', true, true, 2, 130, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (37, 33, 'pc_mailbox_update', 'Mailbox und Alias anpassen', 'Mailbox-bezogene Sichtbarkeit oder Aliasdaten an die neue Position anpassen.', 'Zugaenge', 'mailbox', 11, 'IT', true, true, 3, 140, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (38, 33, 'pc_habel_access_update', 'Habel-Zugang anpassen', 'Habel-Berechtigungen auf die neue Position umstellen.', 'Fachanwendungen', 'habel', 10, 'IT', true, true, 3, 200, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (39, 33, 'pc_ln_access_update', 'InforLN-Zugang anpassen', 'InforLN-Berechtigungen auf die neue Position umstellen.', 'Fachanwendungen', 'react', 9, 'IT', true, true, 3, 210, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (40, 33, 'pc_change_date_confirmed', 'Wechseldatum bestaetigen', 'Bestaetigen, dass das Wechseldatum fuer die neue Position erreicht ist. Schaltet Folgeaufgaben frei.', 'HR', 'identitat', 15, 'HR', true, true, 1, 10, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (41, 33, 'pc_hr_master_data_update', 'Position in HR-Stammdaten aktualisieren', 'Neue Position in Personalakte und HR-Stammdaten nachfuehren.', 'HR', 'identitat', 15, 'HR', true, true, 2, 20, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (42, 33, 'pc_training_assign', 'Schulungen einplanen', 'Noetige Schulungen und Einweisungen fuer die neue Position planen und dokumentieren.', 'Qualifizierung', 'identitat', 15, 'HR', true, true, 5, 110, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (43, 33, 'pc_babtec_access_update', 'Babtec-Zugang anpassen', 'Babtec-Berechtigungen auf die neue Position umstellen.', 'Fachanwendungen', 'babtec', 16, 'QS', true, true, 3, 220, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (44, 33, 'pc_consense_access_update', 'Consense-Zugang anpassen', 'Consense-Berechtigungen auf die neue Position umstellen.', 'Fachanwendungen', 'consense', 1, 'QMB', true, true, 3, 250, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (45, 37, 'rc_babtec_access_update', 'Babtec-Zugang anpassen', 'Babtec-Rollen oder Berechtigungen an die neue Rolle anpassen.', 'Fachanwendungen', 'babtec', 16, 'QS', true, true, 3, 220, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (46, 37, 'rc_gewatec_access_update', 'Gewatec-Zugang anpassen', 'Gewatec-Rollen oder Berechtigungen an die neue Rolle anpassen.', 'Fachanwendungen', 'gewatec', 4, 'AV', true, true, 3, 230, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (47, 37, 'rc_provis_access_update', 'Provis-Zugang anpassen', 'Provis-Rollen oder Berechtigungen an die neue Rolle anpassen.', 'Fachanwendungen', 'berechtigungen', 3, 'AV', true, true, 3, 240, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (48, 37, 'rc_role_assignment_update', 'Rollen-Zuweisung aktualisieren', 'Fachliche und technische Rollen der betroffenen Person auf die neue Rolle umstellen.', 'Berechtigungen', 'ad_user', 12, 'IT', true, true, 2, 100, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (49, 37, 'rc_permission_profile_update', 'Berechtigungsprofil aktualisieren', 'Weitere Berechtigungsprofile und Freigaben an die neue Rolle anpassen.', 'Berechtigungen', 'berechtigungen', 12, 'IT', true, true, 2, 110, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (50, 37, 'rc_ad_groups_update', 'AD-Gruppen aktualisieren', 'AD-Gruppen und Verzeichnisrollen an die neue Rolle anpassen.', 'Zugaenge', 'ad_user', 12, 'IT', true, true, 2, 120, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (51, 37, 'rc_mailbox_update', 'Mailbox und Alias anpassen', 'Mailboxbezogene Sichtbarkeit oder Aliasrechte an die neue Rolle anpassen.', 'Zugaenge', 'mailbox', 11, 'IT', true, true, 3, 130, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (52, 37, 'rc_habel_access_update', 'Habel-Zugang anpassen', 'Habel-Rollen oder Berechtigungen an die neue Rolle anpassen.', 'Fachanwendungen', 'habel', 10, 'IT', true, true, 3, 200, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (53, 37, 'rc_ln_access_update', 'InforLN-Zugang anpassen', 'InforLN-Rollen oder Berechtigungen an die neue Rolle anpassen.', 'Fachanwendungen', 'react', 9, 'IT', true, true, 3, 210, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (54, 37, 'rc_effective_date_confirmed', 'Wirksamkeitsdatum bestaetigen', 'Bestaetigen, dass das Wirksamkeitsdatum fuer den Rollenwechsel erreicht ist. Schaltet Folgeaufgaben frei.', 'HR', 'identitat', 15, 'HR', true, true, 1, 10, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (55, 37, 'rc_hr_master_data_update', 'Rolle in HR-Stammdaten aktualisieren', 'Neue Rolle in Personalakte und HR-Stammdaten nachfuehren.', 'HR', 'identitat', 15, 'HR', true, true, 2, 20, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (56, 37, 'rc_consense_access_update', 'Consense-Zugang anpassen', 'Consense-Rollen oder Berechtigungen an die neue Rolle anpassen.', 'Fachanwendungen', 'consense', 1, 'QMB', true, true, 3, 250, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (57, 41, 'supervisor_fills_document', 'Anforderungen auswählen und bestätigen', 'Die Abteilungsleitung wählt die benötigten Anforderungen aus und bestätigt diese.', 'Führungskraft', 'identitat', NULL, 'Abteilungsleitung', false, true, 2, 40, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (58, 41, 'gewatec_user_create', 'Gewatec-User anlegen', 'Gewatec-User für die neue Person anlegen.', 'Fachanwendungen', 'berechtigungen', 4, 'AV', true, true, 3, 210, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (59, 41, 'provis_user_create', 'Provis-User anlegen', 'Provis-User für die neue Person anlegen.', 'Fachanwendungen', 'berechtigungen', 3, 'AV', true, true, 3, 220, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (60, 41, 'ad_user_create', 'AD-User anlegen', 'AD-User für die neue Person anlegen.', 'Zugänge', 'ad_user', 12, 'IT', true, true, 3, 100, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (61, 41, 'permissions_from_reference_user', 'AD-Berechtigungen anhand Vergleichsuser übernehmen', 'AD-Berechtigungen anhand einer Vergleichsperson übernehmen.', 'Zugänge', 'berechtigungen', 12, 'IT', true, true, 3, 110, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (62, 41, 'internet_access_enable', 'Internetzugang einrichten', 'Internetzugang für die neue Person freischalten.', 'Zugänge', 'internetzugang', 12, 'IT', true, true, 3, 145, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (63, 41, 'internal_drive_access_grant', 'Laufwerksrechte vergeben', 'Zugriffsrechte für das interne Laufwerk der neuen Person einrichten.', 'Zugänge', 'berechtigungen', 12, 'IT', true, true, 3, 147, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (64, 41, 'exchange_create', 'Mailbox anlegen', 'Mailbox für die neue Person anlegen.', 'Zugänge', 'mailbox', 11, 'IT', true, true, 3, 120, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (65, 41, 'habel_user_create', 'Habel-User anlegen', 'Habel-User für die neue Person anlegen.', 'Fachanwendungen', 'habel', 10, 'IT', true, true, 3, 130, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (66, 41, 'ln_user_create', 'LN-User anlegen', 'LN-User für die neue Person anlegen.', 'Fachanwendungen', 'react', 9, 'IT', true, true, 3, 140, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (67, 41, 'office_install', 'Microsoft Office bereitstellen', 'Microsoft Office für die neue Person bereitstellen und konfigurieren.', 'Fachanwendungen', 'microsoft_office', 8, 'IT', true, true, 3, 148, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (68, 41, 'hardware_procure', 'Hardware beschaffen', 'Hardware-Bedarf prüfen und bei Bedarf passende Hardware beschaffen.', 'Ausstattung', 'pc', 8, 'IT', true, true, 5, 150, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (69, 41, 'hardware_setup', 'Hardware einrichten', 'Hardware installieren und für den Einsatz vorbereiten.', 'Ausstattung', 'pc', 8, 'IT', true, true, 3, 160, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (70, 41, 'hardware_handover', 'Hardware bereitstellen', 'Eingerichtete Hardware für die neue Person bereitstellen.', 'Ausstattung', 'pc', 8, 'IT', true, true, 1, 170, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (71, 41, 'phone_prepare', 'Tragbares Telefon bereitstellen', 'Tragbares Telefon für die neue Person bereitstellen.', 'Ausstattung', 'phone', 8, 'IT', true, true, 3, 175, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (72, 41, 'catia_install', 'Catia bereitstellen', 'Catia für die neue Person installieren und bereitstellen.', 'Fachanwendungen', 'catia', 8, 'IT', true, true, 3, 180, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (73, 41, 'datev_install', 'DATEV bereitstellen', 'DATEV für die neue Person installieren und bereitstellen.', 'Fachanwendungen', 'datev', 8, 'IT', true, true, 3, 185, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (74, 41, 'tisoware_install', 'Tisoware bereitstellen', 'Tisoware für die neue Person installieren und bereitstellen.', 'Fachanwendungen', 'tiso', 8, 'IT', true, true, 3, 190, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (75, 41, 'babtec_user_create', 'Babtec-User anlegen', 'User in Babtec für die neue Person anlegen.', 'Fachanwendungen', 'babtec', 16, 'QS', true, true, 3, 200, '2026-05-03 12:13:23.134512+00');
INSERT INTO public.workflow_node_task_specs OVERRIDING SYSTEM VALUE VALUES (76, 41, 'consense_setup', 'Consense User anlegen', 'Consense-User fuer die neue Person anlegen.', 'Fachanwendungen', 'consense', 1, 'QMB', true, true, 3, 230, '2026-05-03 12:13:23.134512+00');

--
-- Data for Name: workflow_tasks; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: task_assignments; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- LA5-F: workflow_node_task_spec_conditions replaces task_template_conditions.
-- condition_group entfaellt (immer 1 in real data).
--

INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (1, 76, 'consense_requested', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (2, 58, 'gewatec_requested', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (3, 59, 'provis_requested', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (4, 60, 'ad_user_requested', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (5, 61, 'comparison_user_available', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (6, 61, 'ad_user_requested', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (7, 62, 'internet_requested', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (8, 63, 'internal_drive_access_requested', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (9, 64, 'mailbox_requested', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (10, 65, 'habel_user_requested', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (11, 66, 'ln_user_requested', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (12, 67, 'microsoft_office_requested', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (13, 68, 'hardware_available', 'is_false', NULL, false, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (14, 68, 'hardware_requested', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (15, 69, 'hardware_requested', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (16, 70, 'hardware_requested', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (17, 71, 'phone_requested', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (18, 72, 'catia_requested', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (19, 73, 'datev_requested', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (20, 74, 'tiso_requested', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (21, 75, 'babtec_requested', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (22, 14, 'ob_has_consense', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (23, 1, 'ob_has_gewatec', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (24, 2, 'ob_has_provis', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (25, 3, 'ob_has_ad_account', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (26, 4, 'ob_has_mailbox', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (27, 5, 'ob_has_habel', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (28, 6, 'ob_has_ln', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (29, 7, 'ob_has_hardware', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (30, 8, 'ob_has_phone', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (31, 10, 'ob_exit_interview', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (32, 11, 'ob_knowledge_transfer', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (33, 13, 'ob_has_babtec', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (34, 19, 'dc_has_consense', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (35, 16, 'dc_has_gewatec', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (36, 17, 'dc_has_provis', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (37, 18, 'dc_ad_group_change', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (38, 20, 'dc_drive_access_change', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (39, 21, 'dc_email_alias_change', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (40, 15, 'dc_has_habel', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (41, 22, 'dc_has_ln', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (42, 23, 'dc_hardware_change', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (43, 26, 'dc_has_babtec', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (44, 44, 'pc_has_consense', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (45, 32, 'pc_has_gewatec', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (46, 33, 'pc_has_provis', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (47, 34, 'pc_permission_change', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (48, 35, 'pc_ad_groups_change', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (49, 36, 'pc_drive_access_change', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (50, 37, 'pc_mail_alias_change', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (51, 38, 'pc_has_habel', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (52, 39, 'pc_has_ln', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (53, 42, 'pc_training_required', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (54, 43, 'pc_has_babtec', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (55, 56, 'rc_has_consense', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (56, 46, 'rc_has_gewatec', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (57, 47, 'rc_has_provis', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (58, 48, 'rc_role_assignment_change', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (59, 49, 'rc_permission_change', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (60, 50, 'rc_ad_groups_change', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (61, 51, 'rc_mailbox_change', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (62, 52, 'rc_has_habel', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (63, 53, 'rc_has_ln', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');
INSERT INTO public.workflow_node_task_spec_conditions OVERRIDING SYSTEM VALUE VALUES (64, 45, 'rc_has_babtec', 'is_true', NULL, true, NULL, '2026-05-03 12:13:23.141756+00');

--
-- LA5-F: workflow_node_task_spec_dependencies replaces task_template_dependencies.
-- required_status entfaellt (immer 'done' in real data); workflow_node_id
-- erzwingt via composite-FK Same-Node-Constraint.
--

INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (1, 75, 57, 41);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (2, 74, 57, 41);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (3, 73, 57, 41);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (4, 72, 57, 41);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (5, 71, 57, 41);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (6, 69, 57, 41);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (7, 68, 57, 41);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (8, 67, 57, 41);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (9, 66, 57, 41);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (10, 65, 57, 41);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (11, 63, 57, 41);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (12, 62, 57, 41);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (13, 60, 57, 41);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (14, 59, 57, 41);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (15, 58, 57, 41);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (16, 76, 57, 41);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (17, 64, 60, 41);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (18, 61, 60, 41);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (19, 69, 68, 41);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (20, 70, 69, 41);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (21, 4, 3, 21);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (22, 13, 9, 21);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (23, 12, 9, 21);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (24, 8, 9, 21);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (25, 7, 9, 21);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (26, 6, 9, 21);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (27, 5, 9, 21);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (28, 3, 9, 21);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (29, 2, 9, 21);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (30, 1, 9, 21);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (31, 14, 9, 21);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (32, 26, 24, 25);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (33, 23, 24, 25);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (34, 22, 24, 25);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (35, 15, 24, 25);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (36, 21, 24, 25);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (37, 20, 24, 25);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (38, 18, 24, 25);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (39, 17, 24, 25);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (40, 16, 24, 25);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (41, 19, 24, 25);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (42, 28, 27, 29);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (43, 29, 30, 29);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (44, 27, 30, 29);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (45, 43, 40, 33);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (46, 42, 40, 33);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (47, 39, 40, 33);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (48, 38, 40, 33);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (49, 37, 40, 33);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (50, 36, 40, 33);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (51, 35, 40, 33);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (52, 34, 40, 33);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (53, 33, 40, 33);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (54, 32, 40, 33);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (55, 44, 40, 33);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (56, 45, 54, 37);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (57, 53, 54, 37);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (58, 52, 54, 37);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (59, 51, 54, 37);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (60, 50, 54, 37);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (61, 49, 54, 37);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (62, 48, 54, 37);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (63, 47, 54, 37);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (64, 46, 54, 37);
INSERT INTO public.workflow_node_task_spec_dependencies OVERRIDING SYSTEM VALUE VALUES (65, 56, 54, 37);

--
-- Data for Name: workflow_answer_derivation_rules; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.workflow_answer_derivation_rules OVERRIDING SYSTEM VALUE VALUES
	(1, 1, 2, 'has_ad_account', 'ob_has_ad_account', 'copy_boolean', true, 1, '2026-04-27 07:07:10.014855+00'),
	(2, 1, 2, 'has_babtec', 'ob_has_babtec', 'copy_boolean', true, 2, '2026-04-27 07:07:10.014855+00'),
	(3, 1, 2, 'has_consense', 'ob_has_consense', 'copy_boolean', true, 3, '2026-04-27 07:07:10.014855+00'),
	(4, 1, 2, 'has_gewatec', 'ob_has_gewatec', 'copy_boolean', true, 4, '2026-04-27 07:07:10.014855+00'),
	(5, 1, 2, 'has_habel', 'ob_has_habel', 'copy_boolean', true, 5, '2026-04-27 07:07:10.014855+00'),
	(6, 1, 2, 'has_hardware', 'ob_has_hardware', 'copy_boolean', true, 6, '2026-04-27 07:07:10.014855+00'),
	(7, 1, 2, 'has_ln', 'ob_has_ln', 'copy_boolean', true, 7, '2026-04-27 07:07:10.014855+00'),
	(8, 1, 2, 'has_mailbox', 'ob_has_mailbox', 'copy_boolean', true, 8, '2026-04-27 07:07:10.014855+00'),
	(9, 1, 2, 'has_phone', 'ob_has_phone', 'copy_boolean', true, 9, '2026-04-27 07:07:10.014855+00'),
	(10, 1, 2, 'has_provis', 'ob_has_provis', 'copy_boolean', true, 10, '2026-04-27 07:07:10.014855+00'),
	(11, 1, 3, 'has_ad_account', 'dc_ad_group_change', 'copy_boolean', true, 1, '2026-04-27 07:07:10.019728+00'),
	(12, 1, 3, 'has_babtec', 'dc_has_babtec', 'copy_boolean', true, 2, '2026-04-27 07:07:10.019728+00'),
	(13, 1, 3, 'has_consense', 'dc_has_consense', 'copy_boolean', true, 3, '2026-04-27 07:07:10.019728+00'),
	(14, 1, 3, 'has_gewatec', 'dc_has_gewatec', 'copy_boolean', true, 4, '2026-04-27 07:07:10.019728+00'),
	(15, 1, 3, 'has_habel', 'dc_has_habel', 'copy_boolean', true, 5, '2026-04-27 07:07:10.019728+00'),
	(16, 1, 3, 'has_hardware', 'dc_hardware_change', 'copy_boolean', true, 6, '2026-04-27 07:07:10.019728+00'),
	(17, 1, 3, 'has_ln', 'dc_has_ln', 'copy_boolean', true, 7, '2026-04-27 07:07:10.019728+00'),
	(18, 1, 3, 'has_provis', 'dc_has_provis', 'copy_boolean', true, 8, '2026-04-27 07:07:10.019728+00');


--
-- Data for Name: workflow_answer_reset_rules; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.workflow_answer_reset_rules OVERRIDING SYSTEM VALUE VALUES
	(11, 1, 'when_not_true', 2, true, false, false, false, false, 1, '2026-04-27 07:07:09.852184+00'),
	(12, 2, 'when_not_true', 3, false, true, false, false, false, 1, '2026-04-27 07:07:09.852184+00'),
	(13, 1, 'when_not_true', 3, false, true, false, false, false, 2, '2026-04-27 07:07:09.852184+00'),
	(14, 9, 'when_not_true', 10, true, false, false, false, false, 1, '2026-04-27 07:07:09.852184+00'),
	(16, 9, 'when_not_true', 12, true, false, false, false, false, 2, '2026-04-27 07:07:09.852184+00'),
	(17, 9, 'when_not_true', 13, false, false, false, true, true, 3, '2026-04-27 07:07:09.852184+00'),
	(18, 13, 'single_select_mismatch', 14, false, false, false, true, true, 1, '2026-04-27 07:07:09.852184+00'),
	(19, 9, 'when_not_true', 14, false, false, false, true, true, 4, '2026-04-27 07:07:09.852184+00'),
	(20, 25, 'when_not_true', 26, false, false, false, false, true, 1, '2026-04-27 07:07:09.852184+00'),
	(21, 28, 'when_not_true', 29, true, false, false, false, false, 1, '2026-04-27 07:07:09.935345+00'),
	(22, 30, 'when_not_true', 31, true, false, false, false, false, 1, '2026-04-27 07:07:09.935345+00'),
	(23, 10, 'when_not_true', 11, false, true, false, false, false, 1, '2026-04-27 07:07:10.529637+00');


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
	(2, 13, 'laptop', 1, '2026-04-27 07:07:09.854647+00');


--
-- Data for Name: workflow_answer_validation_rules; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.workflow_answer_validation_rules VALUES
	(3, 'text_required', 'Bitte den Referenzuser angeben.', '2026-04-27 07:07:09.85019+00'),
	(14, 'single_select_required', 'Bitte auswählen, ob der Laptop mit VPN oder ohne VPN benötigt wird.', '2026-04-27 07:07:09.85019+00'),
	(26, 'multi_select_required', 'Bitte mindestens eine Funktion für die Laufwerksrechte auswählen.', '2026-04-27 07:07:09.85019+00'),
	(11, 'text_required', 'Bitte angeben, welche Hardware übernommen wird.', '2026-04-27 07:07:10.526284+00');


--
-- Data for Name: workflow_answer_visibility_rules; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.workflow_answer_visibility_rules OVERRIDING SYSTEM VALUE VALUES
	(11, 3, 1, 'boolean_true', NULL, false, 1, '2026-04-27 07:07:09.848193+00'),
	(12, 2, 1, 'boolean_true', NULL, true, 1, '2026-04-27 07:07:09.848193+00'),
	(13, 3, 2, 'boolean_true', NULL, false, 2, '2026-04-27 07:07:09.848193+00'),
	(14, 14, 9, 'boolean_true', NULL, true, 1, '2026-04-27 07:07:09.848193+00'),
	(15, 13, 9, 'boolean_true', NULL, true, 1, '2026-04-27 07:07:09.848193+00'),
	(16, 12, 9, 'boolean_true', NULL, true, 1, '2026-04-27 07:07:09.848193+00'),
	(17, 10, 9, 'boolean_true', NULL, false, 1, '2026-04-27 07:07:09.848193+00'),
	(19, 14, 13, 'selected_option_value', 'laptop', false, 2, '2026-04-27 07:07:09.848193+00'),
	(20, 26, 25, 'boolean_true', NULL, false, 1, '2026-04-27 07:07:09.848193+00'),
	(21, 29, 28, 'boolean_true', NULL, false, 1, '2026-04-27 07:07:09.933105+00'),
	(22, 31, 30, 'boolean_true', NULL, false, 1, '2026-04-27 07:07:09.933105+00'),
	(23, 11, 10, 'boolean_true', NULL, false, 1, '2026-04-27 07:07:10.523738+00');


--
-- Data for Name: workflow_audit_log; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: workflow_edges; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.workflow_edges OVERRIDING SYSTEM VALUE VALUES
	(15, 2, 19, 20, 0, NULL, '2026-04-27 07:07:10.374109+00'),
	(16, 2, 20, 21, 0, NULL, '2026-04-27 07:07:10.374109+00'),
	(17, 2, 21, 22, 0, NULL, '2026-04-27 07:07:10.374109+00'),
	(18, 3, 23, 24, 0, NULL, '2026-04-27 07:07:10.377381+00'),
	(19, 3, 24, 25, 0, NULL, '2026-04-27 07:07:10.377381+00'),
	(20, 3, 25, 26, 0, NULL, '2026-04-27 07:07:10.377381+00'),
	(21, 4, 27, 28, 0, NULL, '2026-04-27 07:07:10.385153+00'),
	(22, 4, 28, 29, 0, NULL, '2026-04-27 07:07:10.385153+00'),
	(23, 4, 29, 30, 0, NULL, '2026-04-27 07:07:10.385153+00'),
	(24, 5, 31, 32, 0, NULL, '2026-04-27 07:07:10.389326+00'),
	(25, 5, 32, 33, 0, NULL, '2026-04-27 07:07:10.389326+00'),
	(26, 5, 33, 34, 0, NULL, '2026-04-27 07:07:10.389326+00'),
	(27, 6, 35, 36, 0, NULL, '2026-04-27 07:07:10.392633+00'),
	(28, 6, 36, 37, 0, NULL, '2026-04-27 07:07:10.392633+00'),
	(29, 6, 37, 38, 0, NULL, '2026-04-27 07:07:10.392633+00'),
	(30, 1, 39, 40, 0, NULL, '2026-04-27 07:07:10.396926+00'),
	(31, 1, 40, 41, 0, NULL, '2026-04-27 07:07:10.396926+00'),
	(32, 1, 41, 42, 0, NULL, '2026-04-27 07:07:10.396926+00');


--
-- Data for Name: workflow_links; Type: TABLE DATA; Schema: public; Owner: -
--



--
-- Data for Name: workflow_node_configs; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.workflow_node_configs OVERRIDING SYSTEM VALUE VALUES
	(7, 20, '{"workflowDefinitionKey": "offboarding"}', '2026-04-27 07:07:10.374109+00'),
	(8, 24, '{"workflowDefinitionKey": "department_change"}', '2026-04-27 07:07:10.377381+00'),
	(9, 28, '{"workflowDefinitionKey": "name_change"}', '2026-04-27 07:07:10.385153+00'),
	(10, 32, '{"workflowDefinitionKey": "position_change"}', '2026-04-27 07:07:10.389326+00'),
	(11, 36, '{"workflowDefinitionKey": "role_change"}', '2026-04-27 07:07:10.392633+00'),
	(12, 40, '{"workflowDefinitionKey": "onboarding"}', '2026-04-27 07:07:10.396926+00');


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

SELECT pg_catalog.setval('public.department_action_templates_id_seq', 20, true);


--
-- Name: departments_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.departments_id_seq', 7, true);


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
-- Name: workflow_node_task_spec_conditions_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.workflow_node_task_spec_conditions_id_seq', 64, true);


--
-- Name: workflow_node_task_spec_dependencies_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.workflow_node_task_spec_dependencies_id_seq', 65, true);


--
-- Name: workflow_node_task_specs_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.workflow_node_task_specs_id_seq', 76, true);


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

SET session_replication_role = origin;
