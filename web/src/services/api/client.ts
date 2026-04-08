import { identityProvider } from "../../auth/IdentityProvider";
import { getApiBase } from "../../config/appRuntimeConfig";

type HttpMethod = "GET" | "POST" | "PUT" | "PATCH" | "DELETE";

export type RequestOptions = {
  method?: HttpMethod;
  body?: unknown;
};

export type ApiError = Error & {
  status?: number;
};

function resolveApiBase(): string {
  return getApiBase();
}

const API_BASE = resolveApiBase();
const DEV_SIM_AUTH_TOKEN_STORAGE_KEY = "lifecycle.devSim.authToken";

export function getDevSimAuthToken(): string | null {
  if (typeof window === "undefined") {
    return null;
  }

  const sessionToken = window.sessionStorage.getItem(DEV_SIM_AUTH_TOKEN_STORAGE_KEY);
  const localToken = window.localStorage.getItem(DEV_SIM_AUTH_TOKEN_STORAGE_KEY);
  const token = sessionToken ?? localToken;

  if (!token) {
    return null;
  }

  const normalized = token.trim();
  if (normalized.length === 0) {
    return null;
  }

  if (!sessionToken && localToken) {
    window.sessionStorage.setItem(DEV_SIM_AUTH_TOKEN_STORAGE_KEY, normalized);
    window.localStorage.removeItem(DEV_SIM_AUTH_TOKEN_STORAGE_KEY);
  }

  return normalized;
}

export function setDevSimAuthToken(token: string | null): void {
  if (typeof window === "undefined") {
    return;
  }

  if (!token || !token.trim()) {
    window.sessionStorage.removeItem(DEV_SIM_AUTH_TOKEN_STORAGE_KEY);
    window.localStorage.removeItem(DEV_SIM_AUTH_TOKEN_STORAGE_KEY);
    return;
  }

  window.sessionStorage.setItem(DEV_SIM_AUTH_TOKEN_STORAGE_KEY, token.trim());
  window.localStorage.removeItem(DEV_SIM_AUTH_TOKEN_STORAGE_KEY);
}

async function buildRequestHeaders(withJsonBody: boolean): Promise<HeadersInit> {
  const token = await Promise.resolve(identityProvider.getStoredToken());

  return {
    Accept: "application/json",
    ...(withJsonBody ? { "Content-Type": "application/json" } : {}),
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
  };
}

function toSafeErrorMessage(status: number, payload: unknown): string {
  if (typeof payload === "string" && payload.trim()) {
    return payload;
  }

  if (payload && typeof payload === "object" && "message" in payload) {
    const maybeMessage = (payload as { message?: unknown }).message;
    if (typeof maybeMessage === "string" && maybeMessage.trim()) {
      return maybeMessage;
    }
  }

  return `Backend-Fehler (HTTP ${status}).`;
}

export async function requestJson<T>(path: string, options: RequestOptions = {}): Promise<T> {
  return requestJsonInternal<T>(path, options, true);
}

async function requestJsonInternal<T>(
  path: string,
  options: RequestOptions,
  canRetryUnauthorized: boolean
): Promise<T> {
  const { method = "GET", body } = options;

  let response: Response;
  try {
    response = await fetch(`${API_BASE}${path}`, {
      method,
      headers: await buildRequestHeaders(Boolean(body)),
      ...(body ? { body: JSON.stringify(body) } : {}),
    });
  } catch (error) {
    const err = new Error("Backend ist nicht erreichbar.") as ApiError;
    err.cause = error;
    throw err;
  }

  const contentType = response.headers.get("content-type") ?? "";
  const isJson = contentType.includes("application/json");
  const payload = isJson ? await response.json().catch(() => null) : await response.text().catch(() => "");

  if (!response.ok) {
    if (response.status === 401 && typeof window !== "undefined") {
      if (canRetryUnauthorized) {
        try {
          const refreshed = await identityProvider.refreshAfterUnauthorized();
          if (refreshed) {
            return requestJsonInternal<T>(path, options, false);
          }
        } catch {
          // Fall through to standard invalid-auth handling.
        }
      }

      const tokenStillAvailable = await Promise.resolve(identityProvider.getStoredToken()).catch(() => null);
      if (!tokenStillAvailable) {
        identityProvider.setStoredToken(null);
        window.dispatchEvent(new Event("auth-invalid"));
      }
    }

    const err = new Error(toSafeErrorMessage(response.status, payload)) as ApiError;
    err.status = response.status;
    throw err;
  }

  return payload as T;
}
