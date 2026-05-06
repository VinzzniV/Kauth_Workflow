export type CursorPage<T> = {
  items: T[];
  nextCursor: string | null;
  hasMore: boolean;
};

export type CursorPageQueryOptions = {
  limit?: number | null;
  cursor?: string | null;
};

export function buildCursorPageQuery(options: CursorPageQueryOptions = {}): string {
  const params = new URLSearchParams();

  if (options.limit !== null && options.limit !== undefined) {
    params.set("limit", String(options.limit));
  }

  if (options.cursor?.trim()) {
    params.set("cursor", options.cursor.trim());
  }

  const query = params.toString();
  return query ? `?${query}` : "";
}
