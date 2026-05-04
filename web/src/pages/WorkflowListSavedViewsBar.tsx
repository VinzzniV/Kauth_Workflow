import type { RoleCapabilities } from "../auth/roleModel";
import {
  detectActiveSavedView,
  getSavedViewsForRole,
  type SavedView,
  type SavedViewFilterParams,
} from "./workflowListSavedViews";

type WorkflowListSavedViewsBarProps = {
  capabilities: RoleCapabilities;
  current: SavedViewFilterParams;
  onApply: (view: SavedView) => void;
};

export function WorkflowListSavedViewsBar({
  capabilities,
  current,
  onApply,
}: WorkflowListSavedViewsBarProps) {
  const views = getSavedViewsForRole(capabilities);
  if (views.length === 0) return null;

  const activeView = detectActiveSavedView(views, current);

  return (
    <div className="saved-views-bar" role="group" aria-label="Gespeicherte Ansichten">
      <span className="saved-views-bar-label">Ansichten</span>
      <div className="saved-views-bar-chips">
        {views.map((view) => {
          const isActive = activeView?.id === view.id;
          return (
            <button
              key={view.id}
              type="button"
              className={`saved-view-chip${isActive ? " saved-view-chip--active" : ""}`}
              aria-pressed={isActive}
              onClick={() => onApply(view)}
            >
              {view.label}
            </button>
          );
        })}
      </div>
    </div>
  );
}
