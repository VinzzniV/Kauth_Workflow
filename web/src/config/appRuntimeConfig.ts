type AppRuntimeConfig = {
  apiBase?: string;
  authMode?: string;
  entraClientId?: string;
  entraTenantId?: string;
  entraAudience?: string;
  entraRedirectUri?: string;
};

declare global {
  interface Window {
    __APP_CONFIG__?: AppRuntimeConfig;
  }
}

export type AuthMode = "dev-sim" | "entra";

function normalize(value?: string): string | undefined {
  const trimmed = value?.trim();
  return trimmed ? trimmed : undefined;
}

function readWindowConfig(): AppRuntimeConfig {
  if (typeof window === "undefined") {
    return {};
  }

  return window.__APP_CONFIG__ ?? {};
}

function hasWindowRuntimeConfig(): boolean {
  return typeof window !== "undefined" && typeof window.__APP_CONFIG__ !== "undefined";
}

function normalizeAuthMode(value?: string): AuthMode {
  const normalized = (normalize(value) ?? "dev-sim").toLowerCase();
  if (normalized === "dev-sim" || normalized === "entra") {
    return normalized;
  }

  throw new Error(`Unsupported auth mode '${value}'. Expected 'dev-sim' or 'entra'.`);
}

function validateAbsoluteRedirectUri(value: string): string {
  let url: URL;
  try {
    url = new URL(value);
  } catch {
    throw new Error(`ENTRA_REDIRECT_URI must be an absolute URL. Value '${value}' is invalid.`);
  }

  if (url.search || url.hash) {
    throw new Error("ENTRA_REDIRECT_URI must not contain query strings or fragments.");
  }

  return url.toString();
}

export function getApiBase(): string {
  const windowConfig = readWindowConfig();
  return normalize(windowConfig.apiBase) ?? normalize(import.meta.env.VITE_API_BASE) ?? "/api";
}

export function getAuthMode(): AuthMode {
  const windowConfig = readWindowConfig();
  return normalizeAuthMode(windowConfig.authMode ?? import.meta.env.VITE_AUTH_MODE);
}

export function getEntraClientId(): string {
  const windowConfig = readWindowConfig();
  return normalize(windowConfig.entraClientId) ?? normalize(import.meta.env.VITE_ENTRA_CLIENT_ID) ?? "";
}

export function getEntraTenantId(): string {
  const windowConfig = readWindowConfig();
  return normalize(windowConfig.entraTenantId) ?? normalize(import.meta.env.VITE_ENTRA_TENANT_ID) ?? "";
}

export function getEntraAudience(): string {
  const windowConfig = readWindowConfig();
  return normalize(windowConfig.entraAudience) ?? normalize(import.meta.env.VITE_ENTRA_AUDIENCE) ?? "";
}

export function getEntraRedirectUri(): string {
  const windowConfig = readWindowConfig();
  const authMode = getAuthMode();
  const configuredRedirectUri =
    normalize(windowConfig.entraRedirectUri) ?? normalize(import.meta.env.VITE_ENTRA_REDIRECT_URI);
  if (configuredRedirectUri) {
    return validateAbsoluteRedirectUri(configuredRedirectUri);
  }

  if (authMode === "entra" && hasWindowRuntimeConfig()) {
    throw new Error(
      "ENTRA_REDIRECT_URI must be explicitly configured in app-config.js when authMode=entra."
    );
  }

  if (typeof window === "undefined") {
    return "";
  }

  return validateAbsoluteRedirectUri(window.location.origin);
}
