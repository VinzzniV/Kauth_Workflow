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

export function getApiBase(): string {
  const windowConfig = readWindowConfig();
  return normalize(windowConfig.apiBase) ?? normalize(import.meta.env.VITE_API_BASE) ?? "/api";
}

export function getAuthMode(): string {
  const windowConfig = readWindowConfig();
  return (normalize(windowConfig.authMode) ?? normalize(import.meta.env.VITE_AUTH_MODE) ?? "dev-sim").toLowerCase();
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
  const configuredRedirectUri =
    normalize(windowConfig.entraRedirectUri) ?? normalize(import.meta.env.VITE_ENTRA_REDIRECT_URI);
  if (configuredRedirectUri) {
    return configuredRedirectUri;
  }

  if (typeof window === "undefined") {
    return "";
  }

  return window.location.origin;
}
