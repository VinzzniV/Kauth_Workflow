import { useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import EmptyState from "../components/feedback/EmptyState";
import LoadingState from "../components/feedback/LoadingState";
import PageHeader from "../components/layout/PageHeader";
import { usePeopleDirectory } from "../services/queries/peopleQueries";
import type { PersonDirectoryItem } from "../types/workflow";
import { formatDate } from "../utils/dateFormat";
import {
  formatDirectoryLinkStatusShort,
  formatEmploymentStatus,
  getEmploymentStatusClass,
} from "../utils/employmentStatus";

const SEARCH_DEBOUNCE_MS = 400;
const PAGE_SIZE = 50;

function PersonRow({ person }: { person: PersonDirectoryItem }) {
  return (
    <tr>
      <td>
        <Link to={`/people/${person.personId}`} className="table-link">
          {person.displayName}
        </Link>
      </td>
      <td>{person.departmentName ?? "–"}</td>
      <td>{person.roleName ?? "–"}</td>
      <td>
        <span className={getEmploymentStatusClass(person.employmentStatus)}>
          {formatEmploymentStatus(person.employmentStatus)}
        </span>
      </td>
      <td>{person.entryDate ? formatDate(person.entryDate) : "–"}</td>
      <td>{formatDirectoryLinkStatusShort(person.directoryLinkStatus)}</td>
    </tr>
  );
}

export default function PeopleDirectoryPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const initialSearch = searchParams.get("q") ?? "";
  const initialPage = Math.max(1, parseInt(searchParams.get("page") ?? "1", 10));
  const [search, setSearch] = useState<string>(initialSearch);
  const [debouncedSearch, setDebouncedSearch] = useState<string>(initialSearch);
  const [offset, setOffset] = useState((initialPage - 1) * PAGE_SIZE);

  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedSearch(search);
      setOffset(0);
    }, SEARCH_DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    const nextParams = new URLSearchParams();
    if (search.trim()) {
      nextParams.set("q", search.trim());
    }
    const pageNum = Math.floor(offset / PAGE_SIZE) + 1;
    if (pageNum > 1) {
      nextParams.set("page", String(pageNum));
    }
    if (nextParams.toString() !== searchParams.toString()) {
      setSearchParams(nextParams, { replace: true });
    }
  }, [search, offset, searchParams, setSearchParams]);

  const directoryQuery = usePeopleDirectory(debouncedSearch, offset, PAGE_SIZE);
  const items: PersonDirectoryItem[] = directoryQuery.data?.items ?? [];
  const total: number = directoryQuery.data?.total ?? 0;
  const isLoading = directoryQuery.isLoading;
  const isRefreshing = directoryQuery.isFetching;
  const error =
    directoryQuery.error instanceof Error
      ? directoryQuery.error.message
      : directoryQuery.error
        ? "Mitarbeiterliste konnte nicht geladen werden."
        : null;

  const totalPages = Math.ceil(total / PAGE_SIZE);
  const currentPage = Math.floor(offset / PAGE_SIZE) + 1;
  const hasPrev = offset > 0;
  const hasNext = offset + PAGE_SIZE < total;

  return (
    <main className="app-shell">
      <div className="page-container">
        <PageHeader
          variant="workspace"
          title="Mitarbeiter"
          description="Mitarbeitende suchen und Personalakten direkt aufrufen."
        />

        <section className="panel">
          <div className="panel-head">
            <h2>Suche</h2>
          </div>

          <div className="toolbar-row workflow-filter-bar">
            <label className="field compact grow">
              <span>Name oder Abteilung</span>
              <input
                type="text"
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="z. B. Mustermann, IT, Vertrieb"
                autoFocus
              />
            </label>

            <button
              type="button"
              className="btn btn-secondary"
              onClick={() => void directoryQuery.refetch()}
              disabled={isRefreshing}
            >
              {isRefreshing ? "Aktualisiere..." : "Aktualisieren"}
            </button>
          </div>
        </section>

        {isLoading ? (
          <LoadingState title="Mitarbeiterliste wird geladen..." />
        ) : error ? (
          <EmptyState
            title="Mitarbeiterliste konnte nicht geladen werden."
            description={error}
            actionLabel="Erneut versuchen"
            onAction={() => void directoryQuery.refetch()}
          />
        ) : items.length === 0 ? (
          <EmptyState
            title="Keine Einträge gefunden."
            description={
              debouncedSearch.trim()
                ? `Für „${debouncedSearch}" wurden keine Mitarbeitenden gefunden.`
                : "Es sind noch keine Mitarbeitenden im Verzeichnis erfasst."
            }
          />
        ) : (
          <section className="panel">
            <div className="panel-head">
              <h2>
                {total === 1
                  ? "1 Mitarbeiter"
                  : `${total} Mitarbeitende`}
              </h2>
              {totalPages > 1 && (
                <span className="panel-head-meta">
                  Seite {currentPage} von {totalPages}
                </span>
              )}
            </div>

            <div className="table-wrapper">
              <table className="data-table">
                <thead>
                  <tr>
                    <th scope="col">Name</th>
                    <th scope="col">Abteilung</th>
                    <th scope="col">Stelle</th>
                    <th scope="col">Status</th>
                    <th scope="col">Eintrittsdatum</th>
                    <th scope="col">Verzeichnis</th>
                  </tr>
                </thead>
                <tbody>
                  {items.map((person) => (
                    <PersonRow key={person.personId} person={person} />
                  ))}
                </tbody>
              </table>
            </div>

            {totalPages > 1 && (
              <div className="pagination-row">
                <button
                  type="button"
                  className="btn btn-secondary"
                  onClick={() => setOffset(offset - PAGE_SIZE)}
                  disabled={!hasPrev}
                >
                  Zurück
                </button>
                <span className="pagination-info">
                  {offset + 1}–{Math.min(offset + PAGE_SIZE, total)} von {total}
                </span>
                <button
                  type="button"
                  className="btn btn-secondary"
                  onClick={() => setOffset(offset + PAGE_SIZE)}
                  disabled={!hasNext}
                >
                  Weiter
                </button>
              </div>
            )}
          </section>
        )}
      </div>
    </main>
  );
}
