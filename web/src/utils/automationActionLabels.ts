// Slice 6 (Admin-Gated-Automation, Action-Buendelung im Task-UI): Mapping vom
// technischen Action-Key auf die fachliche Bezeichnung im Approval-Dialog.
// Unbekannte Keys werden als Raw-Wert zurueckgegeben, damit ein Drift-Stand
// sichtbar wird statt stillschweigend zu verschwinden.

const AUTOMATION_ACTION_LABELS: Record<string, string> = {
  CreateAdUserLdaps: "Active-Directory-Benutzer anlegen",
  AssignGroupsLdaps: "Gruppen-Mitgliedschaften zuweisen",
  CreateMailboxGraph: "Mailbox-Lizenz zuweisen",
  SendWelcomeMailGraph: "Willkommens-Mail versenden",
  DisableAdUserLdaps: "Active-Directory-Benutzer deaktivieren",
  RemoveFromAllGroupsLdaps: "Aus allen Gruppen entfernen",
  RemoveMailboxLicense: "Exchange-Lizenz entfernen",
};

export function getAutomationActionLabel(actionKey: string): string {
  return AUTOMATION_ACTION_LABELS[actionKey] ?? actionKey;
}
