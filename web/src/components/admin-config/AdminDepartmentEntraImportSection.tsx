import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { queryKeys } from "../../services/queryKeys";
import {
  getDepartmentEntraJobTitles,
  importDepartmentPositionsFromEntra,
} from "../../services/adminApi";
import type { AdminRole } from "../../types/auth";
import SelectionListItem from "../ui/SelectionListItem";

type Props = {
  departmentId: number;
  existingPositions: AdminRole[];
  onRefreshData?: () => Promise<void> | void;
};

type ImportBannerState = {
  created: number;
  skipped: number;
  attemptedTitles: string[];
};

export function AdminDepartmentEntraImportSection({
  departmentId,
  existingPositions,
  onRefreshData,
}: Props) {
  const queryClient = useQueryClient();
  const [selectedTitles, setSelectedTitles] = useState<Set<string>>(new Set());
  const [resultBanner, setResultBanner] = useState<ImportBannerState | null>(null);
  const [isRefreshingAfterImport, setIsRefreshingAfterImport] = useState(false);
  const [refreshError, setRefreshError] = useState<string | null>(null);

  const { data: entraJobTitles, isLoading } = useQuery({
    queryKey: queryKeys.admin.departmentEntraJobTitles(departmentId),
    queryFn: () => getDepartmentEntraJobTitles(departmentId),
    staleTime: 60_000,
  });

  const existingNames = useMemo(
    () => new Set(existingPositions.map((p) => p.roleName.trim().toLowerCase())),
    [existingPositions]
  );
  const importable = useMemo(
    () => (entraJobTitles ?? []).filter((t) => !existingNames.has(t.toLowerCase())),
    [entraJobTitles, existingNames]
  );
  const existingCount = (entraJobTitles?.length ?? 0) - importable.length;

  const importMutation = useMutation({
    mutationFn: (titles: string[]) => importDepartmentPositionsFromEntra(departmentId, titles),
    onSuccess: async (result, titles) => {
      setRefreshError(null);
      setResultBanner({
        ...result,
        attemptedTitles: [...titles],
      });
      setSelectedTitles(new Set());
      setIsRefreshingAfterImport(true);

      try {
        await Promise.all([
          queryClient.invalidateQueries({ queryKey: queryKeys.roles() }),
          queryClient.invalidateQueries({ queryKey: queryKeys.admin.roles() }),
          queryClient.invalidateQueries({ queryKey: queryKeys.admin.departmentAssignments() }),
          queryClient.invalidateQueries({ queryKey: queryKeys.admin.departmentEntraJobTitles(departmentId) }),
          Promise.resolve(onRefreshData?.()),
        ]);
      } catch {
        setRefreshError("Import erfolgreich, aber die Ansicht konnte nicht sofort aktualisiert werden.");
      } finally {
        setIsRefreshingAfterImport(false);
      }
    },
  });

  if (isLoading) {
    return (
      <div className="panel-note">Entra-Stellenbezeichnungen werden geladen…</div>
    );
  }

  if (!entraJobTitles || entraJobTitles.length === 0) {
    return (
      <section className="panel panel-muted admin-department-import">
        <div className="panel-head">
          <h3 className="panel-title">Stellen aus Entra importieren</h3>
          <p>Für diese Abteilung wurden aktuell keine eindeutigen Entra-Stellenbezeichnungen gefunden.</p>
        </div>
        <p className="panel-note">
          Prüfe, ob der Abteilungsname mit der Entra-Abteilungszuordnung übereinstimmt.
        </p>
      </section>
    );
  }

  function toggleTitle(title: string) {
    setSelectedTitles((prev) => {
      const next = new Set(prev);
      if (next.has(title)) {
        next.delete(title);
      } else {
        next.add(title);
      }
      return next;
    });
  }

  function toggleAll() {
    const allSelected = importable.every((t) => selectedTitles.has(t));
    if (allSelected) {
      setSelectedTitles(new Set());
    } else {
      setSelectedTitles(new Set(importable));
    }
  }
  const allImportableSelected =
    importable.length > 0 && importable.every((t) => selectedTitles.has(t));
  const selectedCount = selectedTitles.size;
  const importIsBusy = importMutation.isPending || isRefreshingAfterImport;

  return (
    <section className="panel panel-muted admin-department-import">
      <div className="panel-head">
        <h3 className="panel-title">Stellen aus Entra importieren</h3>
        <p>
          Eindeutige Entra-Stellenbezeichnungen dieser Abteilung. Importierbare Titel werden als Auswahlkarten
          angeboten, bereits vorhandene Stellen bleiben sichtbar als Referenz.
        </p>
      </div>

      <div className="admin-department-import__stats" aria-label="Importstatus">
        <span className="admin-department-badge admin-department-badge--brand">
          {importable.length} importierbar
        </span>
        <span className="admin-department-badge">
          {existingCount} bereits vorhanden
        </span>
        {selectedCount > 0 ? (
          <span className="admin-department-badge admin-department-badge--info">
            {selectedCount} ausgewählt
          </span>
        ) : null}
      </div>

      {resultBanner ? (
        <section className="panel panel-success panel-banner admin-department-import__banner" role="status">
          <div>
            <strong>
              {resultBanner.created} Stelle{resultBanner.created !== 1 ? "n" : ""} importiert
            </strong>
            <p className="panel-note">
              {resultBanner.skipped} übersprungen. Zuletzt verarbeitet: {resultBanner.attemptedTitles.join(", ")}.
            </p>
          </div>
          <button
            type="button"
            className="btn btn-secondary"
            onClick={() => setResultBanner(null)}
          >
            Ausblenden
          </button>
        </section>
      ) : null}

      {refreshError ? (
        <section className="panel panel-warning admin-department-import__banner" role="status">
          <p className="panel-text">{refreshError}</p>
        </section>
      ) : null}

      <div className="action-row admin-department-import__actions">
        <button
          type="button"
          className="btn btn-secondary"
          onClick={toggleAll}
          disabled={importable.length === 0 || importIsBusy}
        >
          {allImportableSelected ? "Auswahl leeren" : "Alle importierbaren wählen"}
        </button>
        <button
          type="button"
          className="btn btn-primary"
          disabled={selectedCount === 0 || importIsBusy}
          onClick={() => {
            void importMutation.mutateAsync([...selectedTitles]);
          }}
        >
          {importMutation.isPending
            ? "Importiere Stellen..."
            : isRefreshingAfterImport
              ? "Aktualisiere Ansicht..."
              : `${selectedCount > 0 ? selectedCount : ""} Stelle${selectedCount !== 1 ? "n" : ""} importieren`}
        </button>
      </div>

      <div className="selection-list admin-department-import__list">
        {entraJobTitles.map((title) => {
          const alreadyExists = existingNames.has(title.toLowerCase());
          const isSelected = selectedTitles.has(title);
          return (
            <SelectionListItem
              key={title}
              title={title}
              meta={
                alreadyExists
                  ? "Bereits als Stelle in dieser Abteilung vorhanden."
                  : isSelected
                    ? "Ausgewählt für den nächsten Import."
                    : "Zum Import auswählen."
              }
              secondaryMeta={alreadyExists ? "Wird beim Import übersprungen." : "Quelle: Entra"}
              active={!alreadyExists && isSelected}
              className={`admin-department-import-option${alreadyExists ? " admin-department-import-option--locked" : ""}`}
              disabled={alreadyExists || importIsBusy}
              onClick={() => toggleTitle(title)}
            />
          );
        })}
      </div>

      {importMutation.isError ? (
        <section className="panel panel-error admin-department-import__banner" role="alert">
          <p className="panel-text">Import fehlgeschlagen. Bitte erneut versuchen.</p>
        </section>
      ) : null}
    </section>
  );
}
