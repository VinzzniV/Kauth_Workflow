// Module-level cache for stable/quasi-static API data.
// Cached promises are reused across component mounts and page navigations within a session.
// On fetch failure, the entry is removed so the next call retries.

const promiseCache = new Map<string, Promise<unknown>>();

export function getCachedRequest<T>(key: string, fetcher: () => Promise<T>): Promise<T> {
  const cached = promiseCache.get(key);
  if (cached !== undefined) {
    return cached as Promise<T>;
  }
  const promise = fetcher().catch((err: unknown) => {
    promiseCache.delete(key);
    throw err;
  });
  promiseCache.set(key, promise);
  return promise;
}

export function invalidateCachedRequest(key: string): void {
  promiseCache.delete(key);
}
