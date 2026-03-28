import { useMemo, useRef } from "react";
import {
  ADMIN_WORKSPACE_SECTION_META,
  type AdminWorkspaceSection,
  type AdminWorkspaceSectionGroup,
} from "./adminWorkspaceModel";

type AdminWorkspaceNavigationProps = {
  section: AdminWorkspaceSection;
  onSelectSection: (section: AdminWorkspaceSection) => void;
  getPanelId: (section: AdminWorkspaceSection) => string;
  getTabId: (section: AdminWorkspaceSection) => string;
};

const GROUP_ORDER: AdminWorkspaceSectionGroup[] = ["start", "configuration", "technical", "sensitive"];

export function AdminWorkspaceNavigation({
  section,
  onSelectSection,
  getPanelId,
  getTabId,
}: AdminWorkspaceNavigationProps) {
  const tabRefs = useRef<Record<string, HTMLButtonElement | null>>({});
  const orderedSections = useMemo(
    () =>
      GROUP_ORDER.flatMap((group) =>
        ADMIN_WORKSPACE_SECTION_META.filter((item) => item.group === group).map((item) => item.key)
      ),
    []
  );

  const handleTabKeyDown = (event: React.KeyboardEvent<HTMLButtonElement>, currentSection: AdminWorkspaceSection) => {
    const currentIndex = orderedSections.indexOf(currentSection);
    if (currentIndex === -1) {
      return;
    }

    let nextSection: AdminWorkspaceSection | null = null;

    if (event.key === "ArrowRight" || event.key === "ArrowDown") {
      nextSection = orderedSections[(currentIndex + 1) % orderedSections.length];
    } else if (event.key === "ArrowLeft" || event.key === "ArrowUp") {
      nextSection = orderedSections[(currentIndex - 1 + orderedSections.length) % orderedSections.length];
    } else if (event.key === "Home") {
      nextSection = orderedSections[0];
    } else if (event.key === "End") {
      nextSection = orderedSections[orderedSections.length - 1];
    }

    if (!nextSection) {
      return;
    }

    event.preventDefault();
    onSelectSection(nextSection);
    tabRefs.current[nextSection]?.focus();
  };

  return (
    <section className="panel panel-muted">
      <div className="panel-head">
        <h2>Administrationsbereiche</h2>
        <p>Wählen Sie den Bereich nach Zweck und Wirkung. Fachliche Pflege startet meist in Organisation, technische und breit wirksame Änderungen sind bewusst separat gruppiert.</p>
      </div>

      <div className="admin-workspace-groups" aria-label="Admin-Bereiche">
        {GROUP_ORDER.map((group) => {
          const groupItems = ADMIN_WORKSPACE_SECTION_META.filter((item) => item.group === group);
          if (groupItems.length === 0) {
            return null;
          }

          return (
            <section key={group} className="admin-workspace-group">
              <div className="admin-workspace-group-head">
                <h3>{groupItems[0].groupLabel}</h3>
                <p>{groupItems[0].groupDescription}</p>
              </div>

              <div className="admin-workspace-nav" role="tablist" aria-label={`${groupItems[0].groupLabel} auswählen`}>
                {groupItems.map((item) => (
                  <button
                    key={item.key}
                    type="button"
                    className={`admin-workspace-tab ${section === item.key ? "active" : ""}`}
                    id={getTabId(item.key)}
                    role="tab"
                    aria-selected={section === item.key}
                    aria-controls={getPanelId(item.key)}
                    tabIndex={section === item.key ? 0 : -1}
                    ref={(element) => {
                      tabRefs.current[item.key] = element;
                    }}
                    onKeyDown={(event) => {
                      handleTabKeyDown(event, item.key);
                    }}
                    onClick={() => onSelectSection(item.key)}
                  >
                    <span className="admin-workspace-tab-kicker">{item.impactLabel}</span>
                    <span className="admin-workspace-tab-title">{item.label}</span>
                    <span className="admin-workspace-tab-note">{item.description}</span>
                  </button>
                ))}
              </div>
            </section>
          );
        })}
      </div>
    </section>
  );
}
