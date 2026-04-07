import { AdminAnswerDefinitionSection } from "./AdminAnswerDefinitionSection";
import { AdminRoleAnswerDefaultsSection } from "./AdminRoleAnswerDefaultsSection";
import type { AdminWorkspaceSection } from "./adminWorkspaceModel";

type AdminFieldConfigurationWorkspaceSectionProps = {
  section: Extract<AdminWorkspaceSection, "answers" | "defaults">;
  onSelectSection: (section: AdminWorkspaceSection) => void;
  onNotice: (message: string | null) => void;
  onError: (message: string | null) => void;
};

const FIELD_CONFIGURATION_SEGMENTS: Array<{
  key: Extract<AdminWorkspaceSection, "answers" | "defaults">;
  label: string;
}> = [
  {
    key: "answers",
    label: "Felder definieren",
  },
  {
    key: "defaults",
    label: "Vorgaben je Rolle",
  },
];

export function AdminFieldConfigurationWorkspaceSection({
  section,
  onSelectSection,
  onNotice,
  onError,
}: AdminFieldConfigurationWorkspaceSectionProps) {
  const activeSegment = FIELD_CONFIGURATION_SEGMENTS.find((item) => item.key === section) ?? FIELD_CONFIGURATION_SEGMENTS[0];

  return (
    <div className="content-stack">
      <section className="panel panel-muted admin-workspace-segment-panel">
        <div className="admin-workspace-segment-head">
          <span className="admin-workspace-segment-label">Ansicht</span>
          <span className="admin-workspace-segment-value">{activeSegment.label}</span>
        </div>

        <div className="admin-entity-switcher" role="tablist" aria-label="Bereich innerhalb von Felder & Vorgaben wechseln">
          {FIELD_CONFIGURATION_SEGMENTS.map((item) => (
            <button
              key={item.key}
              type="button"
              role="tab"
              className={`admin-entity-switch ${section === item.key ? "active" : ""}`}
              aria-selected={section === item.key}
              onClick={() => onSelectSection(item.key)}
            >
              {item.label}
            </button>
          ))}
        </div>
      </section>

      {section === "defaults" ? (
        <AdminRoleAnswerDefaultsSection onNotice={onNotice} onError={onError} />
      ) : (
        <AdminAnswerDefinitionSection onNotice={onNotice} onError={onError} />
      )}
    </div>
  );
}
