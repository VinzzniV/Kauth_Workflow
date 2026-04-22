import type {
  AdminNotificationTemplate,
  AdminNotificationTemplatePreviewResponse,
  AdminNotificationTemplateRotationPlanPreviewTarget,
  AdminNotificationTemplateWorkflowPreviewTarget,
} from "../../types/auth";
import { formatTimestamp } from "./adminConfigHelpers";

type AdminNotificationTemplateSectionProps = {
  notificationTemplates: AdminNotificationTemplate[];
  selectedTemplate: AdminNotificationTemplate | null;
  selectedTemplateKey: string | null;
  selectedTemplateSubjectDraft: string;
  selectedTemplateBodyDraft: string;
  hasSelectedTemplateChanges: boolean;
  workflowPreviewSearch: string;
  rotationPlanPreviewSearch: string;
  workflowPreviewTargets: AdminNotificationTemplateWorkflowPreviewTarget[];
  rotationPlanPreviewTargets: AdminNotificationTemplateRotationPlanPreviewTarget[];
  selectedWorkflowPreviewUid: string | null;
  selectedRotationPlanPreviewId: number | null;
  previewResponse: AdminNotificationTemplatePreviewResponse | null;
  selectedPreviewVariantIndex: number;
  isLoadingNotificationTemplates: boolean;
  isSavingNotificationTemplate: boolean;
  isLoadingPreviewTargets: boolean;
  isLoadingPreview: boolean;
  onSelectTemplate: (templateKey: string) => void;
  onSelectedTemplateSubjectChange: (value: string) => void;
  onSelectedTemplateBodyChange: (value: string) => void;
  onWorkflowPreviewSearchChange: (value: string) => void;
  onRotationPlanPreviewSearchChange: (value: string) => void;
  onSelectWorkflowPreviewTarget: (workflowUid: string | null) => void;
  onSelectRotationPlanPreviewTarget: (rotationPlanId: number | null) => void;
  onSaveSelectedTemplate: () => void | Promise<void>;
  onRenderPreview: () => void | Promise<void>;
  onSelectPreviewVariant: (index: number) => void;
};

export function AdminNotificationTemplateSection({
  notificationTemplates,
  selectedTemplate,
  selectedTemplateKey,
  selectedTemplateSubjectDraft,
  selectedTemplateBodyDraft,
  hasSelectedTemplateChanges,
  workflowPreviewSearch,
  rotationPlanPreviewSearch,
  workflowPreviewTargets,
  rotationPlanPreviewTargets,
  selectedWorkflowPreviewUid,
  selectedRotationPlanPreviewId,
  previewResponse,
  selectedPreviewVariantIndex,
  isLoadingNotificationTemplates,
  isSavingNotificationTemplate,
  isLoadingPreviewTargets,
  isLoadingPreview,
  onSelectTemplate,
  onSelectedTemplateSubjectChange,
  onSelectedTemplateBodyChange,
  onWorkflowPreviewSearchChange,
  onRotationPlanPreviewSearchChange,
  onSelectWorkflowPreviewTarget,
  onSelectRotationPlanPreviewTarget,
  onSaveSelectedTemplate,
  onRenderPreview,
  onSelectPreviewVariant,
}: AdminNotificationTemplateSectionProps) {
  const selectedVariant = previewResponse?.variants[selectedPreviewVariantIndex] ?? null;
  const isWorkflowPreview = selectedTemplate?.previewTargetType === "workflow";

  return (
    <section className="panel">
      <div className="panel-head">
        <h2>Konfiguration: Mail-Vorlagen</h2>
        <p>Pflegen Sie Betreff und Text pro Mailtyp und prüfen Sie die Vorschau auf Basis echter Vorgänge.</p>
      </div>

      <div className="admin-template-layout" style={{ display: "grid", gap: 16, gridTemplateColumns: "minmax(240px, 280px) minmax(0, 1fr)" }}>
        <aside className="panel panel-muted">
          <h3 className="panel-title">Mailtypen</h3>
          <div className="content-stack">
            {notificationTemplates.map((template) => (
              <button
                key={template.templateKey}
                type="button"
                className={`admin-workspace-tab ${selectedTemplateKey === template.templateKey ? "active" : ""}`}
                aria-pressed={selectedTemplateKey === template.templateKey}
                onClick={() => onSelectTemplate(template.templateKey)}
              >
                <span className="admin-workspace-tab-title">{template.displayName}</span>
                <span className="admin-workspace-tab-description">{template.triggerDescription}</span>
              </button>
            ))}
            {isLoadingNotificationTemplates && notificationTemplates.length === 0 ? (
              <p className="panel-note">Mail-Vorlagen werden geladen...</p>
            ) : null}
          </div>
        </aside>

        <div className="content-stack">
          {selectedTemplate ? (
            <>
              <section className="dashboard-card admin-system-config-card">
                <div>
                  <h2>{selectedTemplate.displayName}</h2>
                  <p>{selectedTemplate.triggerDescription}</p>
                </div>

                <div className="dashboard-grid">
                  <article className="dashboard-stat-card card-stat">
                    <div>
                      <h2>Trigger</h2>
                      <p>{selectedTemplate.previewTargetType === "workflow" ? "Workflow" : "Durchlaufplan"}</p>
                    </div>
                    <p className="panel-note">Zuletzt geändert: {formatTimestamp(selectedTemplate.updatedAt)}</p>
                  </article>

                  <article className="dashboard-stat-card card-stat">
                    <div>
                      <h2>Platzhalter</h2>
                      <p>{selectedTemplate.placeholders.length}</p>
                    </div>
                    <p className="panel-note">Nur diese Platzhalter sind in Betreff und Text erlaubt.</p>
                  </article>
                </div>

                <label className="field">
                  <span>Betreff</span>
                  <input
                    type="text"
                    value={selectedTemplateSubjectDraft}
                    onChange={(event) => onSelectedTemplateSubjectChange(event.target.value)}
                  />
                </label>

                <label className="field">
                  <span>Text</span>
                  <textarea
                    rows={12}
                    value={selectedTemplateBodyDraft}
                    onChange={(event) => onSelectedTemplateBodyChange(event.target.value)}
                  />
                </label>

                <div className="panel panel-muted">
                  <h3 className="panel-title">Erlaubte Platzhalter</h3>
                  <ul className="validation-list">
                    {selectedTemplate.placeholders.map((placeholder) => (
                      <li key={placeholder.key}>
                        <code>{`{{${placeholder.key}}}`}</code> - {placeholder.description}
                      </li>
                    ))}
                  </ul>
                </div>

                <div className="action-row">
                  <button
                    type="button"
                    className="btn btn-primary"
                    disabled={!hasSelectedTemplateChanges || isSavingNotificationTemplate}
                    onClick={() => { void onSaveSelectedTemplate(); }}
                  >
                    {isSavingNotificationTemplate ? "Speichern..." : "Vorlage speichern"}
                  </button>
                </div>
              </section>

              <section className="dashboard-card admin-system-config-card">
                <div>
                  <h2>Preview mit echten Daten</h2>
                  <p>Die Vorschau ist nur verfügbar, wenn der ausgewählte Vorgang die Mail aktuell real auslösen würde.</p>
                </div>

                <label className="field compact">
                  <span>Suche</span>
                  <input
                    type="search"
                    value={isWorkflowPreview ? workflowPreviewSearch : rotationPlanPreviewSearch}
                    onChange={(event) =>
                      isWorkflowPreview
                        ? onWorkflowPreviewSearchChange(event.target.value)
                        : onRotationPlanPreviewSearchChange(event.target.value)
                    }
                    placeholder={isWorkflowPreview ? "Workflow suchen" : "Durchlaufplan suchen"}
                  />
                </label>

                <label className="field compact">
                  <span>{isWorkflowPreview ? "Workflow" : "Durchlaufplan"}</span>
                  <select
                    value={isWorkflowPreview ? selectedWorkflowPreviewUid ?? "" : String(selectedRotationPlanPreviewId ?? "")}
                    onChange={(event) =>
                      isWorkflowPreview
                        ? onSelectWorkflowPreviewTarget(event.target.value || null)
                        : onSelectRotationPlanPreviewTarget(event.target.value ? Number(event.target.value) : null)
                    }
                  >
                    {(isWorkflowPreview ? workflowPreviewTargets : rotationPlanPreviewTargets).map((target) => (
                      <option
                        key={isWorkflowPreview ? (target as AdminNotificationTemplateWorkflowPreviewTarget).workflowUid : (target as AdminNotificationTemplateRotationPlanPreviewTarget).rotationPlanId}
                        value={isWorkflowPreview ? (target as AdminNotificationTemplateWorkflowPreviewTarget).workflowUid : (target as AdminNotificationTemplateRotationPlanPreviewTarget).rotationPlanId}
                      >
                        {isWorkflowPreview
                          ? `${(target as AdminNotificationTemplateWorkflowPreviewTarget).displayName} | ${(target as AdminNotificationTemplateWorkflowPreviewTarget).processName}`
                          : `${(target as AdminNotificationTemplateRotationPlanPreviewTarget).title} | ${(target as AdminNotificationTemplateRotationPlanPreviewTarget).displayName}`}
                      </option>
                    ))}
                  </select>
                  {isLoadingPreviewTargets ? <small className="panel-note">Preview-Ziele werden geladen...</small> : null}
                </label>

                <div className="action-row">
                  <button
                    type="button"
                    className="btn btn-secondary"
                    disabled={isLoadingPreview}
                    onClick={() => { void onRenderPreview(); }}
                  >
                    {isLoadingPreview ? "Rendere Preview..." : "Preview laden"}
                  </button>
                </div>

                {previewResponse ? (
                  <div className="content-stack">
                    <div className="panel panel-muted">
                      <h3 className="panel-title">Preview-Status</h3>
                      <p>
                        {previewResponse.isCurrentlyTriggerable
                          ? "Aktuell triggerbar"
                          : previewResponse.blockingReason ?? "Aktuell nicht triggerbar"}
                      </p>
                      <p className="panel-note">
                        Ziel: {previewResponse.target.primaryLabel} | {previewResponse.target.secondaryLabel}
                      </p>
                    </div>

                    {previewResponse.variants.length > 1 ? (
                      <div className="action-row">
                        {previewResponse.variants.map((variant, index) => (
                          <button
                            key={`${variant.recipient.email}-${index}`}
                            type="button"
                            className={`btn ${selectedPreviewVariantIndex === index ? "btn-primary" : "btn-secondary"}`}
                            onClick={() => onSelectPreviewVariant(index)}
                          >
                            {variant.recipient.name}
                          </button>
                        ))}
                      </div>
                    ) : null}

                    {selectedVariant ? (
                      <>
                        <div className="panel panel-muted">
                          <h3 className="panel-title">Empfänger</h3>
                          <p>{selectedVariant.recipient.name} | {selectedVariant.recipient.email}</p>
                        </div>

                        <div className="panel panel-muted">
                          <h3 className="panel-title">Gerenderter Betreff</h3>
                          <p>{selectedVariant.renderedSubject}</p>
                        </div>

                        <div className="panel panel-muted">
                          <h3 className="panel-title">Gerenderter Text</h3>
                          <pre style={{ whiteSpace: "pre-wrap", margin: 0 }}>{selectedVariant.renderedTextBody}</pre>
                        </div>

                        <div className="panel panel-muted">
                          <h3 className="panel-title">Verwendete Platzhalter</h3>
                          <ul className="validation-list">
                            {selectedVariant.placeholderValues.map((placeholder) => (
                              <li key={placeholder.key}>
                                <code>{`{{${placeholder.key}}}`}</code> = {placeholder.value}
                              </li>
                            ))}
                          </ul>
                        </div>
                      </>
                    ) : null}
                  </div>
                ) : null}
              </section>
            </>
          ) : (
            <section className="panel panel-muted">
              <p className="panel-note">Wählen Sie links einen Mailtyp aus.</p>
            </section>
          )}
        </div>
      </div>
    </section>
  );
}
