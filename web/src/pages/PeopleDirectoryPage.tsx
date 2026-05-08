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
const NO_DEPT_KEY = "Ohne Abteilung";

// --- Avatar helpers ---

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

// --- Grouping ---

function groupByDepartment(
  items: PersonDirectoryItem[],
): [string, PersonDirectoryItem[]][] {
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

// --- Icons ---

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

// --- Sub-components ---

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

function PersonCard({ person }: { person: PersonDirectoryItem }) {
  return (
    <Link
      to={`/people/${person.personId}`}
      className="card-list"
      style={{
        textDecoration: "none",
        color: "inherit",
        display: "flex",
        flexDirection: "column",
        gap: "0.625rem",
        padding: "0.875rem",
      }}
    >
      <div style={{ display: "flex", alignItems: "center", gap: "0.625rem" }}>
        <PersonAvatar name={person.displayName} size={38} />
        <div style={{ minWidth: 0, display: "flex", flexDirection: "column", gap: "0.1rem" }}>
          <span
            className="table-link"
            style={{
              fontWeight: 600,
              fontSize: "0.875rem",
              overflow: "hidden",
              textOverflow: "ellipsis",
              whiteSpace: "nowrap",
            }}
          >
            {person.displayName}
          </span>
          {person.jobTitle ? (
            <span
              style={{
                fontSize: "0.75rem",
                color: "var(--text-secondary)",
                overflow: "hidden",
                textOverflow: "ellipsis",
                whiteSpace: "nowrap",
              }}
            >
              {person.jobTitle}
            </span>
          ) : (
            <span style={{ fontSize: "0.75rem", color: "var(--text-tertiary)" }}>
              Keine Stellenbezeichnung
            </span>
          )}
        </div>
      </div>

      <div
        style={{
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          gap: "0.5rem",
        }}
      >
        <span className={getEmploymentStatusClass(person.employmentStatus)}>
          {formatEmploymentStatus(person.employmentStatus)}
        </span>
        {person.entryDate && (
          <span style={{ fontSize: "0.7rem", color: "var(--text-tertiary)", flexShrink: 0 }}>
            ab {formatDate(person.entryDate)}
          </span>
        )}
      </div>
    </Link>
  );
}

function PersonRow({ person }: { person: PersonDirectoryItem }) {
  return (
    <tr>
      <td>
        <Link to={`/people/${person.personId}`} className="table-link">
          {person.displayName}
        </Link>
      </td>
      <td>{person.jobTitle ?? "–"}</td>
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

function DepartmentSection({
  name,
  people,
  viewMode,
}: {
  name: string;
  people: PersonDirectoryItem[];
  viewMode: "cards" | "table";
}) {
  const [expanded, setExpanded] = useState(false);

  return (
    <section className="panel" style={{ overflow: "hidden" }}>
      <button
        type="button"
        onClick={() => setExpanded((v) => !v)}
        aria-expanded={expanded}
        style={{
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          width: "100%",
          background: "none",
          border: "none",
          padding: 0,
          cursor: "pointer",
          color: "inherit",
        }}
      >
        <div style={{ display: "flex", alignItems: "center", gap: "0.625rem" }}>
          <h2
            style={{
              margin: 0,
              fontSize: "1rem",
              fontWeight: 600,
              color: "var(--text-primary)",
            }}
          >
            {name}
          </h2>
          <span className="panel-head-meta" style={{ fontWeight: 500 }}>
            {people.length} {people.length === 1 ? "Person" : "Personen"}
          </span>
        </div>
        <ChevronIcon expanded={expanded} />
      </button>

      {expanded && (
        <div style={{ marginTop: "1rem" }}>
          {viewMode === "cards" ? (
            <div
              style={{
                display: "grid",
                gridTemplateColumns: "repeat(auto-fill, minmax(195px, 1fr))",
                gap: "0.625rem",
              }}
            >
              {people.map((p) => (
                <PersonCard key={p.personId} person={p} />
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
                  </tr>
                </thead>
                <tbody>
                  {people.map((p) => (
                    <PersonRow key={p.personId} person={p} />
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}
    </section>
  );
}

// --- Page ---

export default function PeopleDirectoryPage() {
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

  const grouped = groupByDepartment(items);
  const departmentCount = grouped.filter(([k]) => k !== NO_DEPT_KEY).length;

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
                onChange={(e) => setSearch(e.target.value)}
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
              <span>
                {total === 1 ? "1 Mitarbeitende·r" : `${total} Mitarbeitende`}
              </span>
              {departmentCount > 0 && (
                <>
                  <span aria-hidden="true">·</span>
                  <span>
                    {departmentCount}{" "}
                    {departmentCount === 1 ? "Abteilung" : "Abteilungen"}
                  </span>
                </>
              )}
              {isRefreshing && (
                <>
                  <span aria-hidden="true">·</span>
                  <span>Wird aktualisiert…</span>
                </>
              )}
              {totalPages > 1 && (
                <>
                  <span aria-hidden="true">·</span>
                  <span>
                    Seite {currentPage} von {totalPages}
                  </span>
                </>
              )}
            </div>

            <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}>
              {grouped.map(([deptName, people]) => (
                <DepartmentSection
                  key={deptName}
                  name={deptName}
                  people={people}
                  viewMode={viewMode}
                />
              ))}
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
          </>
        )}
      </div>
    </main>
  );
}
