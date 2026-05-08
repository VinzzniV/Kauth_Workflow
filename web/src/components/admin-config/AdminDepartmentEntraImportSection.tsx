import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { queryKeys } from "../../services/queryKeys";
import {
  getDepartmentEntraJobTitles,
  importDepartmentPositionsFromEntra,
} from "../../services/adminApi";
import type { AdminRole } from "../../types/auth";

type Props = {
  departmentId: number;
  existingPositions: AdminRole[];
};

export function AdminDepartmentEntraImportSection({ departmentId, existingPositions }: Props) {
  const queryClient = useQueryClient();
  const [selectedTitles, setSelectedTitles] = useState<Set<string>>(new Set());
  const [resultBanner, setResultBanner] = useState<{ created: number; skipped: number } | null>(null);

  const { data: entraJobTitles, isLoading } = useQuery({
    queryKey: queryKeys.admin.departmentEntraJobTitles(departmentId),
    queryFn: () => getDepartmentEntraJobTitles(departmentId),
    staleTime: 60_000,
  });

  const existingNames = new Set(
    existingPositions.map((p) => p.roleName.trim().toLowerCase())
  );

  const importMutation = useMutation({
    mutationFn: (titles: string[]) => importDepartmentPositionsFromEntra(departmentId, titles),
    onSuccess: (result) => {
      setResultBanner(result);
      setSelectedTitles(new Set());
      void queryClient.invalidateQueries({ queryKey: queryKeys.admin.departmentAssignments() });
    },
  });

  if (isLoading) {
    return (
      <div className="panel-note">Entra-Stellenbezeichnungen werden geladen…</div>
    );
  }

  if (!entraJobTitles || entraJobTitles.length === 0) {
    return (
      <div className="panel-note">
        Keine Entra-Stellenbezeichnungen für diese Abteilung gefunden. Prüfe ob der Abteilungsname mit der
        Entra-Abteilungszuordnung übereinstimmt.
      </div>
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
    const importable = entraJobTitles!.filter(
      (t) => !existingNames.has(t.toLowerCase())
    );
    const allSelected = importable.every((t) => selectedTitles.has(t));
    if (allSelected) {
      setSelectedTitles(new Set());
    } else {
      setSelectedTitles(new Set(importable));
    }
  }

  const importable = entraJobTitles.filter((t) => !existingNames.has(t.toLowerCase()));
  const allImportableSelected =
    importable.length > 0 && importable.every((t) => selectedTitles.has(t));

  return (
    <section className="panel panel-muted">
      <div className="panel-head">
        <h3 className="panel-title">Stellen aus Entra importieren</h3>
        <p>
          Eindeutige Entra-Stellenbezeichnungen dieser Abteilung. Bereits vorhandene Stellen sind
          markiert und werden beim Import übersprungen.
        </p>
      </div>

      {resultBanner ? (
        <div className="panel-note">
          Import abgeschlossen: {resultBanner.created} Stelle{resultBanner.created !== 1 ? "n" : ""} angelegt,{" "}
          {resultBanner.skipped} übersprungen.{" "}
          <button
            type="button"
            className="btn-inline"
            onClick={() => setResultBanner(null)}
          >
            Schließen
          </button>
        </div>
      ) : null}

      {importable.length > 1 ? (
        <label className="checkbox-row">
          <input
            type="checkbox"
            checked={allImportableSelected}
            onChange={toggleAll}
          />
          <span>Alle auswählen</span>
        </label>
      ) : null}

      <div className="task-list">
        {entraJobTitles.map((title) => {
          const alreadyExists = existingNames.has(title.toLowerCase());
          return (
            <label key={title} className="checkbox-row">
              <input
                type="checkbox"
                checked={alreadyExists || selectedTitles.has(title)}
                disabled={alreadyExists}
                onChange={() => toggleTitle(title)}
              />
              <span>
                {title}
                {alreadyExists ? <span className="badge badge-muted"> bereits vorhanden</span> : null}
              </span>
            </label>
          );
        })}
      </div>

      <div className="action-row">
        <button
          type="button"
          className="btn btn-primary"
          disabled={selectedTitles.size === 0 || importMutation.isPending}
          onClick={() => {
            void importMutation.mutateAsync([...selectedTitles]);
          }}
        >
          {importMutation.isPending
            ? "Importieren…"
            : `${selectedTitles.size > 0 ? selectedTitles.size : ""} Stelle${selectedTitles.size !== 1 ? "n" : ""} importieren`}
        </button>
      </div>

      {importMutation.isError ? (
        <p className="panel-note">Import fehlgeschlagen. Bitte erneut versuchen.</p>
      ) : null}
    </section>
  );
}
