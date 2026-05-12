import { useEffect, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Link, useSearchParams } from "react-router-dom";
import { useToast } from "../components/feedback/useToast";
import EmptyState from "../components/feedback/EmptyState";
import LoadingState from "../components/feedback/LoadingState";
import PageHeader from "../components/layout/PageHeader";
import { importPeopleFromDirectory } from "../services/peopleApi";
import { queryKeys } from "../services/queryKeys";
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
const NO_DEPT_KEY = "Ohne Abteilung";

const AVATAR_COLORS: { bg: string; text: string }[] = [
  { bg: "#2563eb", text: "#ffffff" },
  { bg: "#16a34a", text: "#ffffff" },
  { bg: "#d97706", text: "#ffffff" },
  { bg: "#dc2626", text: "#ffffff" },
  { bg: "#7c3aed", text: "#ffffff" },
  { bg: "#0891b2", text: "#ffffff" },
  { bg: "#be185d", text: "#ffffff" },
];

function hashName(str: string): number {
  let h = 0;
  for (let i = 0; i < str.length; i++) {
    h = (str.charCodeAt(i) + ((h << 5) - h)) | 0;
  }
  return Math.abs(h);
}

function getAvatarColor(name: string) {
  return AVATAR_COLORS[hashName(name) % AVATAR_COLORS.length];
}

function getInitials(displayName: string): string {
  const parts = displayName.split(/[,\s]+/).filter(Boolean);
  if (parts.length >= 2) return (parts[0][0] + parts[1][0]).toUpperCase();
  return displayName.slice(0, 2).toUpperCase();
}

function getDirectoryEntryKey(person: PersonDirectoryItem): string {
  if (person.personId !== null) {
    return `person-${person.personId}`;
  }

  if (person.directoryIdentityId !== null) {
    return `directory-${person.directoryIdentityId}`;
  }

  return `entry-${person.displayName}`;
}

function isDirectoryOnlyEntry(person: PersonDirectoryItem): boolean {
  return person.personId === null && person.directoryIdentityId !== null;
}

function groupByDepartment(items: PersonDirectoryItem[]): [string, PersonDirectoryItem[]][] {
  const map = new Map<string, PersonDirectoryItem[]>();
  for (const item of items) {
    const key = item.departmentName ?? NO_DEPT_KEY;
    if (!map.has(key)) map.set(key, []);
    map.get(key)!.push(item);
  }

  return Array.from(map.entries()).sort(([a], [b]) => {
    if (a === NO_DEPT_KEY) return 1;
    if (b === NO_DEPT_KEY) return -1;
    return a.localeCompare(b, "de");
  });
}

function GridIcon() {
  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      viewBox="0 0 20 20"
      fill="currentColor"
      style={{ width: 16, height: 16 }}
      aria-hidden="true"
    >
      <path
        fillRule="evenodd"
        d="M4.25 2A2.25 2.25 0 002 4.25v2.5A2.25 2.25 0 004.25 9h2.5A2.25 2.25 0 009 6.75v-2.5A2.25 2.25 0 006.75 2h-2.5zm0 9A2.25 2.25 0 002 13.25v2.5A2.25 2.25 0 004.25 18h2.5A2.25 2.25 0 009 15.75v-2.5A2.25 2.25 0 006.75 11h-2.5zm6.5-9A2.25 2.25 0 008.5 4.25v2.5A2.25 2.25 0 0010.75 9h2.5A2.25 2.25 0 0015.5 6.75v-2.5A2.25 2.25 0 0013.25 2h-2.5zm0 9a2.25 2.25 0 00-2.25 2.25v2.5a2.25 2.25 0 002.25 2.25h2.5a2.25 2.25 0 002.25-2.25v-2.5a2.25 2.25 0 00-2.25-2.25h-2.5z"
        clipRule="evenodd"
      />
    </svg>
  );
}

function TableIcon() {
  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      viewBox="0 0 20 20"
      fill="currentColor"
      style={{ width: 16, height: 16 }}
      aria-hidden="true"
    >
      <path
        fillRule="evenodd"
        d="M.99 5.24A2.25 2.25 0 013.25 3h13.5A2.25 2.25 0 0119 5.25l.01 9.5A2.25 2.25 0 0116.76 17H3.26A2.267 2.267 0 011 14.74l-.01-9.5zm8.26 9.52v-.625a.75.75 0 00-.75-.75H3.25a.75.75 0 00-.75.75v.615c0 .414.336.75.75.75h5.373a.75.75 0 00.627-.74zm1.5 0a.75.75 0 00.627.74h5.373a.75.75 0 00.75-.75v-.615a.75.75 0 00-.75-.75H11.5a.75.75 0 00-.75.75v.625zm6.75-3.63v-.625a.75.75 0 00-.75-.75H11.5a.75.75 0 00-.75.75v.625c0 .414.336.75.75.75h5.25a.75.75 0 00.75-.75zm-8.25 0v-.625a.75.75 0 00-.75-.75H3.25a.75.75 0 00-.75.75v.625c0 .414.336.75.75.75H8.5a.75.75 0 00.75-.75zM17.5 7.5v-.625a.75.75 0 00-.75-.75H11.5a.75.75 0 00-.75.75V7.5c0 .414.336.75.75.75h5.25a.75.75 0 00.75-.75zm-8.25 0v-.625a.75.75 0 00-.75-.75H3.25a.75.75 0 00-.75.75V7.5c0 .414.336.75.75.75H8.5a.75.75 0 00.75-.75z"
        clipRule="evenodd"
      />
    </svg>
  );
}

function ChevronIcon({ expanded }: { expanded: boolean }) {
  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      viewBox="0 0 20 20"
      fill="currentColor"
      style={{
        width: 16,
        height: 16,
        flexShrink: 0,
        transform: expanded ? "rotate(0deg)" : "rotate(-90deg)",
        transition: "transform 200ms ease",
      }}
      aria-hidden="true"
    >
      <path
        fillRule="evenodd"
        d="M5.23 7.21a.75.75 0 011.06.02L10 11.168l3.71-3.938a.75.75 0 111.08 1.04l-4.25 4.5a.75.75 0 01-1.08 0l-4.25-4.5a.75.75 0 01.02-1.06z"
        clipRule="evenodd"
      />
    </svg>
  );
}

function PersonAvatar({ name, size = 36 }: { name: string; size?: number }) {
  const { bg, text } = getAvatarColor(name);
  return (
    <span
      aria-hidden="true"
      style={{
        display: "inline-flex",
        alignItems: "center",
        justifyContent: "center",
        width: size,
        height: size,
        borderRadius: "50%",
        background: bg,
        color: text,
        fontSize: Math.round(size * 0.38),
        fontWeight: 600,
        flexShrink: 0,
        letterSpacing: "0.03em",
        userSelect: "none",
      }}
    >
      {getInitials(name)}
    </span>
  );
}

function ImportDirectoryButton({
  directoryIdentityId,
  isImporting,
  onImport,
}: {
  directoryIdentityId: number | null;
  isImporting: boolean;
  onImport: (directoryIdentityId: number) => void;
}) {
  if (directoryIdentityId === null) {
    return null;
  }

  return (
    <button
      type="button"
      className="btn btn-secondary"
      onClick={() => onImport(directoryIdentityId)}
      disabled={isImporting}
    >
      {isImporting ? "Importiere..." : "Importieren"}
    </button>
  );
}

function PersonCard({
  person,
  onImport,
  isImporting,
}: {
  person: PersonDirectoryItem;
  onImport: (directoryIdentityId: number) => void;
  isImporting: boolean;
}) {
  const directoryOnly = isDirectoryOnlyEntry(person);
  const content = (
    <>
      <div className="people-card-row">
        <PersonAvatar name={person.displayName} size={38} />
        <div className="people-card-meta">
          <span className={`people-card-name${directoryOnly ? "" : " table-link"}`}>
            {person.displayName}
          </span>
          {person.jobTitle ? (
            <span className="people-card-jobtitle">{person.jobTitle}</span>
          ) : (
            <span className="people-card-jobtitle--missing">Keine Stellenbezeichnung</span>
          )}
        </div>
      </div>

      <div className="people-card-status-row">
        <span className={getEmploymentStatusClass(person.employmentStatus)}>
          {formatEmploymentStatus(person.employmentStatus)}
        </span>
        {person.entryDate ? (
          <span className="people-card-date">ab {formatDate(person.entryDate)}</span>
        ) : directoryOnly ? (
          <span className="people-card-date">Noch nicht importiert</span>
        ) : null}
      </div>

      {directoryOnly ? (
        <div className="people-card-directory-row">
          <span className="panel-note people-card-directory-note">Nur im Verzeichnis sichtbar</span>
          <ImportDirectoryButton
            directoryIdentityId={person.directoryIdentityId}
            isImporting={isImporting}
            onImport={onImport}
          />
        </div>
      ) : null}
    </>
  );

  if (person.personId !== null) {
    return (
      <Link to={`/people/${person.personId}`} className="card-list people-card people-card--link">
        {content}
      </Link>
    );
  }

  return <article className="card-list people-card">{content}</article>;
}

function PersonRow({
  person,
  onImport,
  isImporting,
}: {
  person: PersonDirectoryItem;
  onImport: (directoryIdentityId: number) => void;
  isImporting: boolean;
}) {
  const directoryOnly = isDirectoryOnlyEntry(person);

  return (
    <tr>
      <td>
        {person.personId !== null ? (
          <Link to={`/people/${person.personId}`} className="table-link">
            {person.displayName}
          </Link>
        ) : (
          <span>{person.displayName}</span>
        )}
      </td>
      <td>{person.jobTitle ?? "–"}</td>
      <td>
        <span className={getEmploymentStatusClass(person.employmentStatus)}>
          {formatEmploymentStatus(person.employmentStatus)}
        </span>
      </td>
      <td>{person.entryDate ? formatDate(person.entryDate) : "–"}</td>
      <td>{formatDirectoryLinkStatusShort(person.directoryLinkStatus)}</td>
      <td>
        {directoryOnly ? (
          <ImportDirectoryButton
            directoryIdentityId={person.directoryIdentityId}
            isImporting={isImporting}
            onImport={onImport}
          />
        ) : (
          "–"
        )}
      </td>
    </tr>
  );
}

function DepartmentSection({
  name,
  people,
  viewMode,
  onImport,
  importingDirectoryIdentityId,
}: {
  name: string;
  people: PersonDirectoryItem[];
  viewMode: "cards" | "table";
  onImport: (directoryIdentityId: number) => void;
  importingDirectoryIdentityId: number | null;
}) {
  const [expanded, setExpanded] = useState(false);

  return (
    <section className="panel people-department-section">
      <button
        type="button"
        onClick={() => setExpanded((v) => !v)}
        aria-expanded={expanded}
        className="people-department-toggle"
      >
        <div className="people-department-title-row">
          <h2 className="people-department-title">{name}</h2>
          <span className="panel-head-meta">
            {people.length} {people.length === 1 ? "Person" : "Personen"}
          </span>
        </div>
        <ChevronIcon expanded={expanded} />
      </button>

      {expanded ? (
        <div className="people-department-body">
          {viewMode === "cards" ? (
            <div className="people-department-cards">
              {people.map((person) => (
                <PersonCard
                  key={getDirectoryEntryKey(person)}
                  person={person}
                  onImport={onImport}
                  isImporting={importingDirectoryIdentityId === person.directoryIdentityId}
                />
              ))}
            </div>
          ) : (
            <div className="table-wrapper">
              <table className="data-table">
                <thead>
                  <tr>
                    <th scope="col">Name</th>
                    <th scope="col">Stelle</th>
                    <th scope="col">Status</th>
                    <th scope="col">Eintrittsdatum</th>
                    <th scope="col">Verzeichnis</th>
                    <th scope="col">Aktion</th>
                  </tr>
                </thead>
                <tbody>
                  {people.map((person) => (
                    <PersonRow
                      key={getDirectoryEntryKey(person)}
                      person={person}
                      onImport={onImport}
                      isImporting={importingDirectoryIdentityId === person.directoryIdentityId}
                    />
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      ) : null}
    </section>
  );
}

export default function PeopleDirectoryPage() {
  const queryClient = useQueryClient();
  const { showError, showInfo, showSuccess } = useToast();
  const [searchParams, setSearchParams] = useSearchParams();
  const initialSearch = searchParams.get("q") ?? "";
  const initialPage = Math.max(1, parseInt(searchParams.get("page") ?? "1", 10));
  const [search, setSearch] = useState<string>(initialSearch);
  const [debouncedSearch, setDebouncedSearch] = useState<string>(initialSearch);
  const [offset, setOffset] = useState((initialPage - 1) * PAGE_SIZE);
  const [viewMode, setViewMode] = useState<"cards" | "table">("cards");

  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedSearch(search);
      setOffset(0);
    }, SEARCH_DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    const nextParams = new URLSearchParams();
    if (search.trim()) nextParams.set("q", search.trim());
    const pageNum = Math.floor(offset / PAGE_SIZE) + 1;
    if (pageNum > 1) nextParams.set("page", String(pageNum));
    if (nextParams.toString() !== searchParams.toString()) {
      setSearchParams(nextParams, { replace: true });
    }
  }, [search, offset, searchParams, setSearchParams]);

  const directoryQuery = usePeopleDirectory(debouncedSearch, offset, PAGE_SIZE);
  const importMutation = useMutation({
    mutationFn: (directoryIdentityId: number) =>
      importPeopleFromDirectory([directoryIdentityId]),
    onSuccess: (result) => {
      const importedCount = result.createdCount + result.linkedCount;
      if (importedCount > 0) {
        showSuccess(
          importedCount === 1
            ? "Entra-Person wurde importiert."
            : `${importedCount} Entra-Personen wurden importiert.`,
        );
      } else {
        showInfo("Eintrag wurde uebersprungen oder war bereits verknuepft.");
      }

      void queryClient.invalidateQueries({ queryKey: queryKeys.people.directory("", 0).slice(0, 2) });
      void queryClient.invalidateQueries({ queryKey: ["admin", "unlinked-identities"] });
    },
    onError: () => {
      showError("Entra-Person konnte nicht importiert werden.");
    },
  });

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
  const realPeople = items.filter((p) => !isDirectoryOnlyEntry(p));
  const directoryOnlyPeople = items.filter(isDirectoryOnlyEntry);
  const grouped = groupByDepartment(realPeople);
  const departmentCount = grouped.filter(([department]) => department !== NO_DEPT_KEY).length;
  const directoryOnlyCount = directoryOnlyPeople.length;
  const importingDirectoryIdentityId = importMutation.isPending ? importMutation.variables ?? null : null;

  function handleImport(directoryIdentityId: number) {
    importMutation.mutate(directoryIdentityId);
  }

  return (
    <main className="app-shell">
      <div className="page-container">
        <PageHeader
          variant="workspace"
          title="Mitarbeiter"
          description="Mitarbeitende suchen und Personalakten direkt aufrufen."
          actions={
            <div style={{ display: "flex", gap: "0.25rem" }}>
              <button
                type="button"
                className={`btn ${viewMode === "cards" ? "btn-primary" : "btn-secondary"}`}
                onClick={() => setViewMode("cards")}
                title="Kartenansicht"
                aria-pressed={viewMode === "cards"}
              >
                <GridIcon />
              </button>
              <button
                type="button"
                className={`btn ${viewMode === "table" ? "btn-primary" : "btn-secondary"}`}
                onClick={() => setViewMode("table")}
                title="Tabellenansicht"
                aria-pressed={viewMode === "table"}
              >
                <TableIcon />
              </button>
            </div>
          }
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
            title="Keine Eintraege gefunden."
            description={
              debouncedSearch.trim()
                ? `Fuer "${debouncedSearch}" wurden keine Mitarbeitenden gefunden.`
                : "Es sind noch keine Mitarbeitenden im Verzeichnis erfasst."
            }
          />
        ) : (
          <>
            <div
              style={{
                display: "flex",
                alignItems: "center",
                gap: "0.5rem",
                color: "var(--text-secondary)",
                fontSize: "0.8rem",
              }}
            >
              <span>{total === 1 ? "1 Mitarbeitende*r" : `${total} Mitarbeitende`}</span>
              {departmentCount > 0 ? (
                <>
                  <span aria-hidden="true">·</span>
                  <span>
                    {departmentCount} {departmentCount === 1 ? "Abteilung" : "Abteilungen"}
                  </span>
                </>
              ) : null}
              {directoryOnlyCount > 0 ? (
                <>
                  <span aria-hidden="true">·</span>
                  <span>
                    {directoryOnlyCount} {directoryOnlyCount === 1 ? "nur in Entra" : "nur in Entra"}
                  </span>
                </>
              ) : null}
              {isRefreshing ? (
                <>
                  <span aria-hidden="true">·</span>
                  <span>Wird aktualisiert…</span>
                </>
              ) : null}
              {totalPages > 1 ? (
                <>
                  <span aria-hidden="true">·</span>
                  <span>
                    Seite {currentPage} von {totalPages}
                  </span>
                </>
              ) : null}
            </div>

            <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}>
              {grouped.map(([departmentName, people]) => (
                <DepartmentSection
                  key={departmentName}
                  name={departmentName}
                  people={people}
                  viewMode={viewMode}
                  onImport={handleImport}
                  importingDirectoryIdentityId={importingDirectoryIdentityId}
                />
              ))}
            </div>

            {directoryOnlyCount > 0 ? (
              <section className="panel" style={{ marginTop: "0.5rem" }}>
                <div className="panel-head">
                  <h2>Aus Entra noch nicht übernommen</h2>
                  <span className="panel-head-meta">{directoryOnlyCount} {directoryOnlyCount === 1 ? "Eintrag" : "Einträge"}</span>
                </div>
                <p className="panel-note">
                  Diese Personen sind in Entra vorhanden, wurden aber noch nicht als Mitarbeitende importiert.
                  Über „Importieren" wird ein vollständiger Personendatensatz angelegt.
                </p>
                {viewMode === "cards" ? (
                  <div
                    style={{
                      display: "grid",
                      gridTemplateColumns: "repeat(auto-fill, minmax(195px, 1fr))",
                      gap: "0.625rem",
                    }}
                  >
                    {directoryOnlyPeople.map((person) => (
                      <PersonCard
                        key={getDirectoryEntryKey(person)}
                        person={person}
                        onImport={handleImport}
                        isImporting={importingDirectoryIdentityId === person.directoryIdentityId}
                      />
                    ))}
                  </div>
                ) : (
                  <div className="table-wrapper">
                    <table className="data-table">
                      <thead>
                        <tr>
                          <th scope="col">Name</th>
                          <th scope="col">Stelle</th>
                          <th scope="col">Status</th>
                          <th scope="col">Eintrittsdatum</th>
                          <th scope="col">Verzeichnis</th>
                          <th scope="col">Aktion</th>
                        </tr>
                      </thead>
                      <tbody>
                        {directoryOnlyPeople.map((person) => (
                          <PersonRow
                            key={getDirectoryEntryKey(person)}
                            person={person}
                            onImport={handleImport}
                            isImporting={importingDirectoryIdentityId === person.directoryIdentityId}
                          />
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </section>
            ) : null}

            {totalPages > 1 ? (
              <div className="pagination-row">
                <button
                  type="button"
                  className="btn btn-secondary"
                  onClick={() => setOffset(offset - PAGE_SIZE)}
                  disabled={!hasPrev}
                >
                  Zurueck
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
            ) : null}
          </>
        )}
      </div>
    </main>
  );
}
