import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import EmptyState from "../feedback/EmptyState";
import LoadingState from "../feedback/LoadingState";
import { useToast } from "../feedback/useToast";
import {
  createAdminResponsibility,
  deleteAdminResponsibility,
  getAdminResponsibilityOwners,
  getAdminUsers,
  updateAdminResponsibilityOwner,
} from "../../services/adminApi";
import {
  createAdminRotationTemplate,
  deleteAdminRotationTemplate,
  updateAdminRotationTemplate,
} from "../../services/rotationApi";
import { queryKeys } from "../../services/queryKeys";
import { useAdminRotationTemplates } from "../../services/queries/rotationQueries";
import { useDepartments } from "../../services/queries/roleQueries";
import type { AdminResponsibilityOwner } from "../../types/auth";
import type {
  DepartmentActionTemplate,
  DepartmentActionTemplateUpsertPayload,
  RotationTaskType,
  RotationTriggerType,
} from "../../types/rotation";
import { formatDateTime } from "../../utils/dateFormat";

// ──────────────────────────────────────────────────────────────────────────────
// Fachliche Zuständigkeiten
// ──────────────────────────────────────────────────────────────────────────────

type ResponsibilityEditDraft = { appUserId: string; departmentId: string };

function getResponsibilityTypeLabel(type: string | null): string {
  if (type === "application") return "Anwendung";
  return "Prozess";
}

function getResponsibilityUserOptionLabel(user: { displayName: string; departmentName: string | null }): string {
  return user.departmentName?.trim()
    ? `${user.displayName} (${user.departmentName.trim()})`
    : user.displayName;
}

function FachlicheZustaendigkeitenPanel() {
  const queryClient = useQueryClient();
  const { showError, showSuccess } = useToast();

  const responsibilitiesQuery = useQuery({
    queryKey: queryKeys.admin.responsibilityOwners(),
    queryFn: getAdminResponsibilityOwners,
    staleTime: 2 * 60 * 1000,
  });

  const usersQuery = useQuery({
    queryKey: queryKeys.admin.users(),
    queryFn: getAdminUsers,
    staleTime: 5 * 60 * 1000,
  });

  const departmentsQuery = useDepartments();

  const responsibilities = responsibilitiesQuery.data ?? [];
  const users = (usersQuery.data ?? []).filter((u) => !u.isTechnicalActor && u.isActive);
  const departments = departmentsQuery.data ?? [];

  const [newName, setNewName] = useState("");
  const [newDepartmentId, setNewDepartmentId] = useState("");
  const [isCreating, setIsCreating] = useState(false);

  const [editingId, setEditingId] = useState<number | null>(null);
  const [editDraft, setEditDraft] = useState<ResponsibilityEditDraft>({ appUserId: "", departmentId: "" });
  const [isSavingEdit, setIsSavingEdit] = useState(false);
  const [deletingId, setDeletingId] = useState<number | null>(null);

  async function invalidate() {
    await queryClient.invalidateQueries({ queryKey: queryKeys.admin.responsibilityOwners() });
  }

  async function handleCreate() {
    if (!newName.trim()) { showError("Bitte einen Namen eingeben."); return; }
    setIsCreating(true);
    try {
      await createAdminResponsibility(newName.trim(), newDepartmentId ? Number(newDepartmentId) : null);
      showSuccess("Zuständigkeit angelegt.");
      setNewName("");
      setNewDepartmentId("");
      await invalidate();
    } catch (error) {
      showError(error instanceof Error ? error.message : "Fehler beim Anlegen.");
    } finally {
      setIsCreating(false);
    }
  }

  function openEdit(r: AdminResponsibilityOwner) {
    setEditingId(r.responsibilityId);
    setEditDraft({
      appUserId: r.appUserId ? String(r.appUserId) : "",
      departmentId: r.departmentId ? String(r.departmentId) : "",
    });
  }

  async function handleSaveEdit() {
    if (!editingId) return;
    setIsSavingEdit(true);
    try {
      await updateAdminResponsibilityOwner(
        editingId,
        editDraft.appUserId ? Number(editDraft.appUserId) : null,
        editDraft.departmentId ? Number(editDraft.departmentId) : null
      );
      showSuccess("Zuweisung gespeichert.");
      setEditingId(null);
      await invalidate();
    } catch (error) {
      showError(error instanceof Error ? error.message : "Fehler beim Speichern.");
    } finally {
      setIsSavingEdit(false);
    }
  }

  async function handleDelete(r: AdminResponsibilityOwner) {
    setDeletingId(r.responsibilityId);
    try {
      await deleteAdminResponsibility(r.responsibilityId);
      showSuccess("Zuständigkeit gelöscht.");
      if (editingId === r.responsibilityId) setEditingId(null);
      await invalidate();
    } catch (error) {
      showError(error instanceof Error ? error.message : "Fehler beim Löschen.");
    } finally {
      setDeletingId(null);
    }
  }

  return (
    <>
      <section className="panel panel-muted">
        <div className="panel-head">
          <h2>Neue Zuständigkeit anlegen</h2>
          <p>Fachliche Zuständigkeiten steuern, wer für Aufgaben in Onboarding-Vorgängen oder Abteilungsanforderungen zuständig ist.</p>
        </div>
        <div className="toolbar-row toolbar-row-filters">
          <label className="field compact">
            <span>Name</span>
            <input
              type="text"
              value={newName}
              onChange={(e) => setNewName(e.target.value)}
              placeholder="z. B. HR Onboarding, IT-Zugang"
            />
          </label>
          <label className="field compact">
            <span>Bereich (optional)</span>
            <select
              value={newDepartmentId}
              onChange={(e) => setNewDepartmentId(e.target.value)}
              disabled={departmentsQuery.isLoading}
            >
              <option value="">Kein Bereich</option>
              {departments.map((d) => (
                <option key={d.id} value={String(d.id)}>{d.name}</option>
              ))}
            </select>
          </label>
          <button
            type="button"
            className="btn btn-primary"
            onClick={() => void handleCreate()}
            disabled={isCreating || !newName.trim()}
          >
            {isCreating ? "Anlegen..." : "Zuständigkeit anlegen"}
          </button>
        </div>
      </section>

      <section className="panel">
        <div className="panel-head">
          <h2>Fachliche Zuständigkeiten{responsibilities.length > 0 ? ` (${responsibilities.length})` : ""}</h2>
        </div>

        {responsibilitiesQuery.isLoading ? <LoadingState title="Zuständigkeiten werden geladen..." /> : null}

        {!responsibilitiesQuery.isLoading && responsibilitiesQuery.error ? (
          <EmptyState
            title="Zuständigkeiten konnten nicht geladen werden."
            actionLabel="Erneut versuchen"
            onAction={() => void responsibilitiesQuery.refetch()}
          />
        ) : null}

        {!responsibilitiesQuery.isLoading && !responsibilitiesQuery.error && responsibilities.length === 0 ? (
          <p className="panel-note">Noch keine fachlichen Zuständigkeiten angelegt.</p>
        ) : null}

        {!responsibilitiesQuery.isLoading && !responsibilitiesQuery.error && responsibilities.length > 0 ? (
          <div className="workflow-grid" aria-label="Zuständigkeiten">
            {responsibilities.map((r) => (
              <article
                key={r.responsibilityId}
                className={`workflow-card card-list${editingId === r.responsibilityId ? " card-selected" : ""}`}
              >
                <div className="workflow-card-top">
                  <h3>{r.responsibilityName}</h3>
                  <span className="status-pill open">{getResponsibilityTypeLabel(r.responsibilityType)}</span>
                </div>
                <dl className="workflow-meta">
                  <div>
                    <dt>Verantwortlich</dt>
                    <dd>{r.appUserDisplayName ?? "–"}</dd>
                  </div>
                  <div>
                    <dt>Bereich</dt>
                    <dd>{r.departmentName ?? "–"}</dd>
                  </div>
                  {r.systemKey ? (
                    <div>
                      <dt>System-Key</dt>
                      <dd>{r.systemKey}</dd>
                    </div>
                  ) : null}
                </dl>

                {editingId === r.responsibilityId ? (
                  <div className="content-stack">
                    <label className="field compact">
                      <span>Person</span>
                      <select
                        value={editDraft.appUserId}
                        onChange={(e) => {
                          const nextUserId = e.target.value;
                          const selectedUser = users.find((user) => String(user.userId) === nextUserId) ?? null;
                          setEditDraft((draft) => ({
                            ...draft,
                            appUserId: nextUserId,
                            departmentId: selectedUser?.departmentId ? String(selectedUser.departmentId) : "",
                          }));
                        }}
                        disabled={usersQuery.isLoading}
                      >
                        <option value="">Keine Person</option>
                        {users.map((u) => (
                          <option key={u.userId} value={String(u.userId)}>{getResponsibilityUserOptionLabel(u)}</option>
                        ))}
                      </select>
                    </label>
                    <label className="field compact">
                      <span>Bereich</span>
                      <select
                        value={editDraft.departmentId}
                        onChange={(e) => setEditDraft((d) => ({ ...d, departmentId: e.target.value }))}
                        disabled={departmentsQuery.isLoading}
                      >
                        <option value="">Kein Bereich</option>
                        {departments.map((d) => (
                          <option key={d.id} value={String(d.id)}>{d.name}</option>
                        ))}
                      </select>
                    </label>
                    <div className="action-row">
                      <button
                        type="button"
                        className="btn btn-primary"
                        onClick={() => void handleSaveEdit()}
                        disabled={isSavingEdit}
                      >
                        {isSavingEdit ? "Speichere..." : "Speichern"}
                      </button>
                      <button
                        type="button"
                        className="btn btn-secondary"
                        onClick={() => setEditingId(null)}
                        disabled={isSavingEdit}
                      >
                        Abbrechen
                      </button>
                    </div>
                  </div>
                ) : (
                  <div className="action-row">
                    <button
                      type="button"
                      className="btn btn-secondary"
                      onClick={() => openEdit(r)}
                    >
                      Zuweisung bearbeiten
                    </button>
                    <button
                      type="button"
                      className="btn btn-secondary"
                      onClick={() => void handleDelete(r)}
                      disabled={deletingId === r.responsibilityId}
                    >
                      {deletingId === r.responsibilityId ? "Lösche..." : "Löschen"}
                    </button>
                  </div>
                )}
              </article>
            ))}
          </div>
        ) : null}
      </section>
    </>
  );
}

// ──────────────────────────────────────────────────────────────────────────────
// Abteilungsanforderungen
// ──────────────────────────────────────────────────────────────────────────────

type TemplateFormState = {
  departmentId: string;
  triggerType: RotationTriggerType;
  title: string;
  description: string;
  taskType: RotationTaskType;
  defaultResponsibilityId: string;
  dueOffsetDays: string;
  reminderOffsetDays: string;
  isAutomatable: boolean;
  automationKey: string;
  isActive: boolean;
};

function createEmptyTemplateForm(): TemplateFormState {
  return {
    departmentId: "",
    triggerType: "enter",
    title: "",
    description: "",
    taskType: "manual",
    defaultResponsibilityId: "",
    dueOffsetDays: "0",
    reminderOffsetDays: "",
    isAutomatable: false,
    automationKey: "",
    isActive: true,
  };
}

function buildTemplateForm(template: DepartmentActionTemplate): TemplateFormState {
  return {
    departmentId: String(template.departmentId),
    triggerType: template.triggerType,
    title: template.title,
    description: template.description ?? "",
    taskType: template.taskType,
    defaultResponsibilityId: template.defaultResponsibilityId ? String(template.defaultResponsibilityId) : "",
    dueOffsetDays: String(template.dueOffsetDays),
    reminderOffsetDays: template.reminderOffsetDays != null ? String(template.reminderOffsetDays) : "",
    isAutomatable: template.isAutomatable,
    automationKey: template.automationKey ?? "",
    isActive: template.isActive,
  };
}

function normalizeTemplatePayload(form: TemplateFormState): DepartmentActionTemplateUpsertPayload {
  return {
    departmentId: Number(form.departmentId),
    triggerType: form.triggerType,
    title: form.title.trim(),
    description: form.description.trim() || undefined,
    taskType: form.taskType,
    defaultResponsibilityId: form.defaultResponsibilityId ? Number(form.defaultResponsibilityId) : undefined,
    dueOffsetDays: Number(form.dueOffsetDays),
    reminderOffsetDays: form.reminderOffsetDays !== "" ? Number(form.reminderOffsetDays) : undefined,
    isAutomatable: form.isAutomatable,
    automationKey: form.isAutomatable && form.automationKey.trim() ? form.automationKey.trim() : undefined,
    isActive: form.isActive,
  };
}

function getTriggerLabel(t: RotationTriggerType): string {
  return t === "enter" ? "Eintritt" : "Austritt";
}

function formatDueOffsetHint(offsetStr: string, triggerType: string): string {
  const offset = parseInt(offsetStr, 10);
  if (isNaN(offset)) return "";
  const anchor = triggerType === "enter" ? "Stationsbeginn" : "Stationsende";
  if (offset === 0) return `Am Tag des ${anchor} fällig`;
  if (offset < 0) return `${Math.abs(offset)} Tag${Math.abs(offset) === 1 ? "" : "e"} vor ${anchor} fällig`;
  return `${offset} Tag${offset === 1 ? "" : "e"} nach ${anchor} fällig`;
}

function formatReminderOffsetHint(offsetStr: string): string {
  const offset = parseInt(offsetStr, 10);
  if (offsetStr === "" || isNaN(offset)) return "Kein Reminder — zuständige Stelle wird nicht vorab benachrichtigt";
  if (offset === 0) return "Benachrichtigung am Tag des Stationswechsels";
  return `Benachrichtigung ${offset} Tag${offset === 1 ? "" : "e"} vor dem Stationswechsel`;
}

function getTaskTypeLabel(t: RotationTaskType): string {
  switch (t) {
    case "technical": return "Technisch";
    case "approval": return "Freigabe";
    case "information": return "Information";
    default: return "Manuell";
  }
}

function AbteilungsanforderungenPanel() {
  const queryClient = useQueryClient();
  const { showError, showSuccess } = useToast();

  const [filterDepartmentId, setFilterDepartmentId] = useState<number | null>(null);
  const [filterIsActive, setFilterIsActive] = useState<boolean | null>(true);
  const [editingTemplateId, setEditingTemplateId] = useState<number | null>(null);
  const [isCreating, setIsCreating] = useState(false);
  const [form, setForm] = useState<TemplateFormState>(createEmptyTemplateForm());
  const [isSaving, setIsSaving] = useState(false);
  const [deletingId, setDeletingId] = useState<number | null>(null);

  const departmentsQuery = useDepartments();
  const departments = departmentsQuery.data ?? [];

  const responsibilitiesQuery = useQuery({
    queryKey: queryKeys.admin.responsibilityOwners(),
    queryFn: getAdminResponsibilityOwners,
    staleTime: 5 * 60 * 1000,
  });
  const responsibilities = responsibilitiesQuery.data ?? [];

  const templatesQuery = useAdminRotationTemplates(filterDepartmentId, filterIsActive);
  const templates = templatesQuery.data ?? [];

  async function invalidateTemplates() {
    await queryClient.invalidateQueries({ queryKey: ["rotation", "admin-templates"] });
  }

  function openCreateForm() {
    setEditingTemplateId(null);
    setIsCreating(true);
    setForm({ ...createEmptyTemplateForm(), departmentId: filterDepartmentId ? String(filterDepartmentId) : "" });
  }

  function openEditForm(template: DepartmentActionTemplate) {
    setEditingTemplateId(template.id);
    setIsCreating(false);
    setForm(buildTemplateForm(template));
  }

  function closeForm() {
    setEditingTemplateId(null);
    setIsCreating(false);
    setForm(createEmptyTemplateForm());
  }

  async function handleSave() {
    const payload = normalizeTemplatePayload(form);
    if (!payload.departmentId || payload.departmentId <= 0) { showError("Bitte eine Abteilung wählen."); return; }
    if (!payload.title) { showError("Bitte einen Titel angeben."); return; }
    if (!Number.isFinite(payload.dueOffsetDays) || payload.dueOffsetDays < -365 || payload.dueOffsetDays > 365) {
      showError("Fälligkeits-Offset muss zwischen -365 und 365 liegen.");
      return;
    }
    setIsSaving(true);
    try {
      if (editingTemplateId) {
        await updateAdminRotationTemplate(editingTemplateId, payload);
        showSuccess("Vorlage aktualisiert.");
      } else {
        await createAdminRotationTemplate(payload);
        showSuccess("Vorlage angelegt.");
      }
      await invalidateTemplates();
      closeForm();
    } catch (error) {
      showError(error instanceof Error ? error.message : "Vorlage konnte nicht gespeichert werden.");
    } finally {
      setIsSaving(false);
    }
  }

  async function handleDelete(template: DepartmentActionTemplate) {
    setDeletingId(template.id);
    try {
      await deleteAdminRotationTemplate(template.id);
      showSuccess("Vorlage gelöscht.");
      await invalidateTemplates();
      if (editingTemplateId === template.id) closeForm();
    } catch (error) {
      showError(error instanceof Error ? error.message : "Vorlage konnte nicht gelöscht werden.");
    } finally {
      setDeletingId(null);
    }
  }

  const showForm = isCreating || editingTemplateId !== null;

  return (
    <>
      <section className="panel panel-muted">
        <div className="panel-head">
          <h2>Abteilungsanforderungen</h2>
          <p>Vorlagen legen fest, welche Maßnahmen bei Eintritt oder Austritt in eine Abteilung entstehen. Inaktive Vorlagen sind standardmäßig ausgeblendet.</p>
        </div>
        <div className="toolbar-row toolbar-row-filters">
          <label className="field compact">
            <span>Abteilung</span>
            <select
              value={filterDepartmentId ?? ""}
              onChange={(e) => setFilterDepartmentId(e.target.value ? Number(e.target.value) : null)}
              disabled={departmentsQuery.isLoading}
            >
              <option value="">Alle Abteilungen</option>
              {departments.map((d) => (
                <option key={d.id} value={String(d.id)}>{d.name}</option>
              ))}
            </select>
          </label>
              <label className="field compact">
                <span>Status</span>
                <select
                  value={filterIsActive === null ? "" : String(filterIsActive)}
                  onChange={(e) => setFilterIsActive(e.target.value === "" ? null : e.target.value === "true")}
                >
                  <option value="true">Nur aktive</option>
                  <option value="">Alle</option>
                  <option value="false">Nur inaktive</option>
                </select>
              </label>
          <button type="button" className="btn btn-primary" onClick={openCreateForm} disabled={showForm}>
            Neue Vorlage
          </button>
        </div>
      </section>

      {showForm ? (
        <section className="panel panel-muted">
          <div className="panel-head">
            <h2>{editingTemplateId ? "Vorlage bearbeiten" : "Neue Vorlage anlegen"}</h2>
          </div>
          <div className="workflow-grid" aria-label="Vorlagenformular">
            <div className="dashboard-card card-primary rotation-form-card">
              <label className="field compact">
                <span>Abteilung</span>
                <select
                  value={form.departmentId}
                  onChange={(e) => setForm((c) => ({ ...c, departmentId: e.target.value }))}
                  disabled={departmentsQuery.isLoading}
                >
                  <option value="">Abteilung wählen...</option>
                  {departments.map((d) => (
                    <option key={d.id} value={String(d.id)}>{d.name}</option>
                  ))}
                </select>
              </label>
              <label className="field compact">
                <span>Auslöser</span>
                <select
                  value={form.triggerType}
                  onChange={(e) => setForm((c) => ({ ...c, triggerType: e.target.value as RotationTriggerType }))}
                >
                  <option value="enter">Eintritt in Abteilung</option>
                  <option value="exit">Austritt aus Abteilung</option>
                </select>
              </label>
              <label className="field compact">
                <span>Titel</span>
                <input
                  type="text"
                  value={form.title}
                  onChange={(e) => setForm((c) => ({ ...c, title: e.target.value }))}
                  placeholder="z. B. Ordnerrechte Einkauf setzen"
                />
              </label>
              <label className="field compact">
                <span>Beschreibung</span>
                <textarea
                  value={form.description}
                  onChange={(e) => setForm((c) => ({ ...c, description: e.target.value }))}
                  rows={3}
                  placeholder="Optionale Hinweise"
                />
              </label>
              <label className="field compact">
                <span>Aufgabentyp</span>
                <select
                  value={form.taskType}
                  onChange={(e) => setForm((c) => ({ ...c, taskType: e.target.value as RotationTaskType }))}
                >
                  <option value="manual">Manuell</option>
                  <option value="technical">Technisch (IT)</option>
                  <option value="approval">Freigabe</option>
                  <option value="information">Information</option>
                </select>
              </label>
              <label className="field compact">
                <span>Zuständige Stelle *</span>
                <select
                  value={form.defaultResponsibilityId}
                  onChange={(e) => setForm((c) => ({ ...c, defaultResponsibilityId: e.target.value }))}
                  disabled={responsibilitiesQuery.isLoading}
                  required
                >
                  <option value="" disabled>Bitte Zuständigkeit wählen</option>
                  {responsibilities.map((r) => (
                    <option key={r.responsibilityId} value={String(r.responsibilityId)}>
                      {r.responsibilityName}{r.responsibilityType ? ` (${r.responsibilityType})` : ""}
                    </option>
                  ))}
                </select>
              </label>
              <div className="field compact">
                <label>
                  <span>Fälligkeit (Tage relativ zum Auslöser)</span>
                  <input
                    type="number"
                    value={form.dueOffsetDays}
                    onChange={(e) => setForm((c) => ({ ...c, dueOffsetDays: e.target.value }))}
                    placeholder="0 = am Wechseltag, -5 = 5 Tage vorher"
                    min={-365}
                    max={365}
                  />
                </label>
                {form.dueOffsetDays !== "" ? (
                  <p className="panel-note">{formatDueOffsetHint(form.dueOffsetDays, form.triggerType)}</p>
                ) : null}
              </div>
              <div className="field compact">
                <label>
                  <span>Erinnerung (Tage vor Wechsel, optional)</span>
                  <input
                    type="number"
                    value={form.reminderOffsetDays}
                    onChange={(e) => setForm((c) => ({ ...c, reminderOffsetDays: e.target.value }))}
                    placeholder="Leer lassen = kein Reminder"
                    min={0}
                    max={365}
                  />
                </label>
                <p className="panel-note">{formatReminderOffsetHint(form.reminderOffsetDays)}</p>
              </div>
              <label className="field compact">
                <span>Status</span>
                <select
                  value={form.isActive ? "true" : "false"}
                  onChange={(e) => setForm((c) => ({ ...c, isActive: e.target.value === "true" }))}
                >
                  <option value="true">Aktiv</option>
                  <option value="false">Inaktiv</option>
                </select>
              </label>
              <label className="field compact">
                <span>Automatisierbar</span>
                <select
                  value={form.isAutomatable ? "true" : "false"}
                  onChange={(e) => setForm((c) => ({ ...c, isAutomatable: e.target.value === "true" }))}
                >
                  <option value="false">Nein (manuell)</option>
                  <option value="true">Ja (automatisierbar)</option>
                </select>
              </label>
              {form.isAutomatable ? (
                <label className="field compact">
                  <span>Automation-Key</span>
                  <input
                    type="text"
                    value={form.automationKey}
                    onChange={(e) => setForm((c) => ({ ...c, automationKey: e.target.value }))}
                    placeholder="z. B. ad_group_add, folder_permission_set"
                  />
                </label>
              ) : null}
              <div className="action-row">
                <button type="button" className="btn btn-primary" onClick={() => void handleSave()} disabled={isSaving}>
                  {isSaving ? "Speichere..." : editingTemplateId ? "Vorlage aktualisieren" : "Vorlage anlegen"}
                </button>
                {editingTemplateId ? (
                  <button
                    type="button"
                    className="btn btn-secondary"
                    onClick={() => {
                      const currentTemplate = templates.find((template) => template.id === editingTemplateId) ?? null;
                      if (!currentTemplate) {
                        return;
                      }

                      void handleDelete(currentTemplate);
                    }}
                    disabled={isSaving || deletingId === editingTemplateId}
                  >
                    {deletingId === editingTemplateId ? "Lösche..." : "Vorlage löschen"}
                  </button>
                ) : null}
                <button type="button" className="btn btn-secondary" onClick={closeForm} disabled={isSaving}>
                  Abbrechen
                </button>
              </div>
            </div>
          </div>
        </section>
      ) : null}

      <section className="panel">
        <div className="panel-head">
          <h2>
            Maßnahmenvorlagen
            {filterDepartmentId ? ` – ${departments.find((d) => d.id === filterDepartmentId)?.name ?? ""}` : ""}
          </h2>
          <p>
            {templates.length > 0
              ? `${templates.length} Vorlage${templates.length !== 1 ? "n" : ""} gefunden.`
              : "Keine Vorlagen für die gewählten Filter vorhanden."}
          </p>
        </div>

        {templatesQuery.isLoading ? <LoadingState title="Vorlagen werden geladen..." /> : null}

        {!templatesQuery.isLoading && templatesQuery.error ? (
          <EmptyState
            title="Vorlagen konnten nicht geladen werden."
            actionLabel="Erneut versuchen"
            onAction={() => void templatesQuery.refetch()}
          />
        ) : null}

        {!templatesQuery.isLoading && !templatesQuery.error && templates.length === 0 ? (
          <EmptyState
            title="Keine Vorlagen vorhanden"
            description={
              filterIsActive === true
                ? "Aktuell sind keine aktiven Vorlagen sichtbar. Blenden Sie bei Bedarf inaktive Vorlagen über den Statusfilter ein oder legen Sie eine neue Vorlage an."
                : "Legen Sie die erste Maßnahmenvorlage über 'Neue Vorlage' an."
            }
            actionLabel="Neue Vorlage anlegen"
            onAction={openCreateForm}
          />
        ) : null}

        {!templatesQuery.isLoading && !templatesQuery.error && templates.length > 0 ? (
          <div className="workflow-grid" aria-label="Vorlagenliste">
            {templates.map((template) => (
              <article
                key={template.id}
                className={`workflow-card card-list${editingTemplateId === template.id ? " card-selected" : ""}`}
              >
                <div className="workflow-card-top">
                  <h3>{template.title}</h3>
                  <span className={`status-pill ${template.isActive ? "running" : "completed"}`}>
                    {template.isActive ? "Aktiv" : "Inaktiv"}
                  </span>
                </div>
                {template.isActive && !template.defaultResponsibilityName ? (
                  <p className="panel-note" style={{ color: "var(--text-warning)" }}>
                    Keine Zuständigkeit: generierte Aufgaben sind für Fachbereiche nicht sichtbar.
                  </p>
                ) : null}
                <dl className="workflow-meta">
                  <div><dt>Abteilung</dt><dd>{template.departmentName ?? "–"}</dd></div>
                  <div><dt>Auslöser</dt><dd>{getTriggerLabel(template.triggerType)}</dd></div>
                  <div><dt>Typ</dt><dd>{getTaskTypeLabel(template.taskType)}</dd></div>
                  <div><dt>Zuständig</dt><dd>{template.defaultResponsibilityName ?? "–"}</dd></div>
                  <div>
                    <dt>Fälligkeit</dt>
                    <dd>
                      {template.dueOffsetDays === 0
                        ? "Am Wechseltag"
                        : template.dueOffsetDays < 0
                          ? `${Math.abs(template.dueOffsetDays)} Tage vorher`
                          : `${template.dueOffsetDays} Tage danach`}
                    </dd>
                  </div>
                  {template.isAutomatable ? (
                    <div><dt>Automation-Key</dt><dd>{template.automationKey ?? "–"}</dd></div>
                  ) : null}
                  <div><dt>Zuletzt geändert</dt><dd>{formatDateTime(template.updatedAt)}</dd></div>
                </dl>
                {template.description ? <p className="panel-note">{template.description}</p> : null}
                <div className="action-row">
                  <button
                    type="button"
                    className="btn btn-secondary"
                    onClick={() => openEditForm(template)}
                    disabled={editingTemplateId === template.id}
                  >
                    Bearbeiten
                  </button>
                  <button
                    type="button"
                    className="btn btn-secondary"
                    onClick={() => void handleDelete(template)}
                    disabled={deletingId === template.id}
                  >
                    {deletingId === template.id ? "Lösche..." : "Löschen"}
                  </button>
                </div>
              </article>
            ))}
          </div>
        ) : null}
      </section>
    </>
  );
}

// ──────────────────────────────────────────────────────────────────────────────
// Main export
// ──────────────────────────────────────────────────────────────────────────────

export function AdminResponsibilitiesAndRequirementsSection() {
  return (
    <div className="content-stack">
      <FachlicheZustaendigkeitenPanel />
      <div className="admin-section-divider" aria-hidden="true" />
      <AbteilungsanforderungenPanel />
    </div>
  );
}
