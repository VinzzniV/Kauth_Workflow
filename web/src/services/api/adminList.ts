export type AdminListPage<T> = {
  items: T[];
  total: number;
  limit: number;
  offset: number;
};

export type AdminListQueryOptions = {
  limit?: number | null;
  offset?: number | null;
  search?: string | null;
  sort?: string | null;
};

export function buildAdminListQuery(options: AdminListQueryOptions = {}): string {
  const params = new URLSearchParams();

  if (options.limit !== null && options.limit !== undefined) {
    params.set("limit", String(options.limit));
  }

  if (options.offset !== null && options.offset !== undefined) {
    params.set("offset", String(options.offset));
  }

  if (options.search?.trim()) {
    params.set("search", options.search.trim());
  }

  if (options.sort?.trim()) {
    params.set("sort", options.sort.trim());
  }

  const query = params.toString();
  return query ? `?${query}` : "";
}
