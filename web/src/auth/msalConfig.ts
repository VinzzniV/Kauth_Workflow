import { type Configuration, LogLevel, type RedirectRequest } from "@azure/msal-browser";

const clientId = import.meta.env.VITE_ENTRA_CLIENT_ID ?? "";
const tenantId = import.meta.env.VITE_ENTRA_TENANT_ID ?? "";
const redirectUri = import.meta.env.VITE_ENTRA_REDIRECT_URI ?? window.location.origin;

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
