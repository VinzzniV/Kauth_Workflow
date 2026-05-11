// P3-Lookup-Adapter (Z20-B1): Typeahead-Lookup mit Server-search/limit.
// Kein Pagination-Envelope — Server filtert und schneidet, gibt List<T> zurück.
export type LookupQueryOptions = {
  search?: string | null;
  limit?: number | null;
};

export function buildLookupQuery(options: LookupQueryOptions = {}): string {
  const params = new URLSearchParams();

  if (options.search?.trim()) {
    params.set("search", options.search.trim());
  }

  if (options.limit !== null && options.limit !== undefined) {
    params.set("limit", String(options.limit));
  }

  const query = params.toString();
  return query ? `?${query}` : "";
}
