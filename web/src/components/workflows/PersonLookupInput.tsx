// Slice 5 (Admin-Gated-Automation, Referenzuser-Mapping): Searchable Lookup-Widget
// fuer person_lookup-Antworten. Speichert person.id im Form-State via onPersonSelect.
// Reuse usePeopleSearch (debounced via React-State + queryKey).

import { useEffect, useMemo, useState } from "react";
import { usePeopleSearch } from "../../services/queries/peopleQueries";

type Props = {
  requirementId: number;
  value: number | null;
  onChange: (requirementId: number, personId: number | null) => void;
  disabled?: boolean;
};

export default function PersonLookupInput({ requirementId, value, onChange, disabled }: Props) {
  const [query, setQuery] = useState("");
  const [debouncedQuery, setDebouncedQuery] = useState("");

  useEffect(() => {
    const handle = setTimeout(() => setDebouncedQuery(query), 300);
    return () => clearTimeout(handle);
  }, [query]);

  const searchEnabled = debouncedQuery.trim().length > 0 && !disabled;
  const results = usePeopleSearch(debouncedQuery, searchEnabled);

  const selectedLabel = useMemo(() => {
    if (value === null) return "";
    const fromResults = results.data?.find((p) => p.personId === value);
    return fromResults?.displayName ?? `Person #${value}`;
  }, [value, results.data]);

  return (
    <div className="person-lookup-input">
      {value !== null ? (
        <div className="person-lookup-selection">
          <span>Ausgewählt: <strong>{selectedLabel}</strong></span>
          {!disabled ? (
            <button
              type="button"
              className="btn btn-secondary btn-sm"
              onClick={() => onChange(requirementId, null)}
            >
              Entfernen
            </button>
          ) : null}
        </div>
      ) : null}
      <input
        type="search"
        className="input"
        placeholder="Person suchen (Name oder Personalnummer)…"
        value={query}
        onChange={(e) => setQuery(e.target.value)}
        disabled={disabled}
        aria-label="Referenzuser suchen"
      />
      {searchEnabled && results.isLoading ? (
        <p className="text-secondary">Suche läuft…</p>
      ) : null}
      {searchEnabled && results.data && results.data.length > 0 ? (
        <ul className="person-lookup-results">
          {results.data.map((p) => (
            <li key={p.personId}>
              <button
                type="button"
                className="person-lookup-result-item"
                onClick={() => {
                  onChange(requirementId, p.personId);
                  setQuery("");
                  setDebouncedQuery("");
                }}
                disabled={disabled}
              >
                <strong>{p.displayName}</strong>
                {p.employeeNumber !== null ? <span className="text-secondary"> · #{p.employeeNumber}</span> : null}
                {p.departmentName ? <span className="text-secondary"> · {p.departmentName}</span> : null}
              </button>
            </li>
          ))}
        </ul>
      ) : null}
      {searchEnabled && !results.isLoading && results.data && results.data.length === 0 ? (
        <p className="text-secondary">Keine Treffer.</p>
      ) : null}
    </div>
  );
}
