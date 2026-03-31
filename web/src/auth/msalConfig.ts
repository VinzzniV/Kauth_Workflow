import { type Configuration, LogLevel, type RedirectRequest } from "@azure/msal-browser";
import { getEntraClientId, getEntraRedirectUri, getEntraTenantId } from "../config/appRuntimeConfig";

const clientId = getEntraClientId();
const tenantId = getEntraTenantId();
const redirectUri = getEntraRedirectUri();

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
    ...(clientId ? [`api://${clientId}/access_as_user`] : []),
  ],
};
