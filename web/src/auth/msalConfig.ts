import { type Configuration, LogLevel, type RedirectRequest } from "@azure/msal-browser";
import { getEntraAudience, getEntraClientId, getEntraRedirectUri, getEntraTenantId } from "../config/appRuntimeConfig";

const clientId = getEntraClientId();
const tenantId = getEntraTenantId();
const audience = getEntraAudience();
const redirectUri = getEntraRedirectUri();

function resolveApiScope(): string | null {
  const normalizedAudience = audience.trim();
  if (normalizedAudience.length > 0) {
    return `${normalizedAudience.replace(/\/+$/, "")}/access_as_user`;
  }

  const normalizedClientId = clientId.trim();
  if (normalizedClientId.length > 0) {
    return `api://${normalizedClientId}/access_as_user`;
  }

  return null;
}

const apiScope = resolveApiScope();

export const msalConfig: Configuration = {
  auth: {
    clientId,
    authority: `https://login.microsoftonline.com/${tenantId}`,
    redirectUri,
    postLogoutRedirectUri: redirectUri,
  },
  cache: {
    cacheLocation: "sessionStorage",
  },
  system: {
    loggerOptions: {
      logLevel: LogLevel.Warning,
      loggerCallback: (_level, message, containsPii) => {
        if (!containsPii) {
          console.debug("[MSAL]", message);
        }
      },
    },
  },
};

export const loginRequest: RedirectRequest = {
  scopes: [
    "openid",
    "profile",
    "email",
    ...(apiScope ? [apiScope] : []),
  ],
};
