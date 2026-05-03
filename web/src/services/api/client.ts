import { identityProvider } from "../../auth/IdentityProvider";
import { getApiBase } from "../../config/appRuntimeConfig";
import { reportClientLogEvent } from "../systemLogReporter";

type HttpMethod = "GET" | "POST" | "PUT" | "PATCH" | "DELETE";

export function encodeId(id: number | string): string {
  return encodeURIComponent(String(id));
}

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

function summarizePayloadForLog(payload: unknown): unknown {
  if (typeof payload === "string") {
    const trimmed = payload.trim();
    return trimmed.length > 500 ? `${trimmed.slice(0, 500)}...` : trimmed;
  }

  return payload;
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
    void reportClientLogEvent({
      severity: "error",
      source: "frontend",
      category: "network",
      eventKey: "request_transport_failed",
      message: "Backend ist nicht erreichbar.",
      userMessage: "Backend ist nicht erreichbar.",
      clientFunction: "requestJson",
      httpMethod: method,
      httpPath: path,
      details: {
        cause: error instanceof Error ? error.message : String(error),
      },
    });
    const err = new Error("Backend ist nicht erreichbar.") as ApiError;
    err.cause = error;
    throw err;
  }

  const contentType = response.headers.get("content-type") ?? "";
  const isJson = contentType.includes("application/json");
  let payload: unknown;
  if (isJson) {
    try {
      payload = await response.json();
    } catch (parseError) {
      void reportClientLogEvent({
        severity: "warning",
        source: "api",
        category: "http",
        eventKey: "response_parse_failed",
        message: `${method} ${path} lieferte ungültiges JSON (HTTP ${response.status}).`,
        clientFunction: "requestJson",
        httpMethod: method,
        httpPath: path,
        httpStatus: response.status,
        details: {
          cause: parseError instanceof Error ? parseError.message : String(parseError),
        },
      });
      payload = null;
    }
  } else {
    try {
      payload = await response.text();
    } catch {
      payload = "";
    }
  }

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

    const errorMessage = toSafeErrorMessage(response.status, payload);
    void reportClientLogEvent({
      severity: response.status >= 500 ? "error" : "warning",
      source: "api",
      category: "http",
      eventKey: "request_failed",
      message: `${method} ${path} fehlgeschlagen (${response.status}).`,
      userMessage: errorMessage,
      clientFunction: "requestJson",
      httpMethod: method,
      httpPath: path,
      httpStatus: response.status,
      traceIdentifier:
        response.headers.get("x-trace-id")
        ?? response.headers.get("trace-id")
        ?? response.headers.get("request-id"),
      details: {
        responsePayload: summarizePayloadForLog(payload),
      },
    });

    const err = new Error(errorMessage) as ApiError;
    err.status = response.status;
    throw err;
  }

  return payload as T;
}
