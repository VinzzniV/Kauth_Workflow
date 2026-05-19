import {
  PublicClientApplication,
  type AccountInfo,
  BrowserAuthError,
  InteractionRequiredAuthError,
  ServerError,
} from "@azure/msal-browser";
import { msalConfig, loginRequest } from "./msalConfig";
import type { IIdentityProvider, InteractiveReauthOutcome } from "./IdentityProvider";
import type { Me, SimulationLoginResponse, SimulationLoginUserOption } from "../types/auth";
import { requestJson } from "../services/api/client";

let msalInstance: PublicClientApplication | null = null;
let msalInitialized: Promise<void> | null = null;

// MSAL redirect promise must be handled once at app start.
let redirectHandled = false;

function getMsalInstance(): PublicClientApplication {
  msalInstance ??= new PublicClientApplication(msalConfig);
  return msalInstance;
}

async function ensureMsalInitialized(): Promise<void> {
  msalInitialized ??= getMsalInstance().initialize();
  await msalInitialized;
}

export async function handleMsalRedirect(): Promise<AccountInfo | null> {
  await ensureMsalInitialized();

  const instance = getMsalInstance();

  if (redirectHandled) return instance.getActiveAccount();
  redirectHandled = true;

  const response = await instance.handleRedirectPromise();
  if (response?.account) {
    instance.setActiveAccount(response.account);
    return response.account;
  }

  const accounts = instance.getAllAccounts();
  if (accounts.length > 0) {
    instance.setActiveAccount(accounts[0]);
    return accounts[0];
  }

  return null;
}

async function acquireToken(forceRefresh = false): Promise<string | null> {
  await ensureMsalInitialized();

  const instance = getMsalInstance();

  const account = instance.getActiveAccount();
  if (!account) return null;

  try {
    const result = await instance.acquireTokenSilent({
      ...loginRequest,
      account,
      forceRefresh,
    });
    return result.accessToken;
  } catch (error) {
    if (error instanceof InteractionRequiredAuthError) {
      // Do not trigger a fresh redirect here. A 401 from the API can also mean
      // "authenticated at Microsoft, but not authorized in this app".
      return null;
    }
    throw error;
  }
}

export class EntraIdentityProvider implements IIdentityProvider {
  public readonly providerKind = "entra";

  public async getStoredToken(): Promise<string | null> {
    return acquireToken();
  }

  public setStoredToken(token: string | null): void {
    void token;
    // MSAL manages its own token cache — this is a no-op for Entra mode.
  }

  public async refreshAfterUnauthorized(): Promise<boolean> {
    const token = await acquireToken(true);
    return Boolean(token);
  }

  public async getCurrentUser(): Promise<Me> {
    return requestJson<Me>("/me");
  }

  public async getLoginOptions(): Promise<SimulationLoginUserOption[]> {
    // Not applicable for Entra — return empty list.
    return [];
  }

  public async loginAsUser(userId: number): Promise<SimulationLoginResponse> {
    await ensureMsalInitialized();
    void userId;

    const instance = getMsalInstance();

    // Trigger the MSAL redirect flow. This navigates away from the SPA,
    // so the returned promise will not resolve in the current page load.
    await instance.loginRedirect(loginRequest);

    // This line is only reached if redirect didn't happen (shouldn't occur).
    throw new Error("Redirect to Microsoft login initiated.");
  }

  public async logout(): Promise<void> {
    await ensureMsalInitialized();

    const instance = getMsalInstance();

    await instance.logoutRedirect({
      postLogoutRedirectUri: msalConfig.auth.redirectUri as string,
    });
  }

  // Slice AGA-N2: Interaktiver Re-Auth fuer Approval-Flow.
  // Popup mit `prompt: 'login'` zwingt einen frischen Entra-Login (neuer
  // auth_time-Claim). Anschliessend forceRefresh, damit der naechste API-Call
  // den frischen Access-Token bekommt — sonst sieht das Backend weiter den
  // alten auth_time. Popup statt Redirect, damit der Approval-Dialog erhalten
  // bleibt.
  public async triggerInteractiveReauth(): Promise<InteractiveReauthOutcome> {
    await ensureMsalInitialized();
    const instance = getMsalInstance();

    try {
      const popupResponse = await instance.loginPopup({
        ...loginRequest,
        prompt: "login",
      });
      if (popupResponse.account) {
        instance.setActiveAccount(popupResponse.account);
      }
    } catch (error) {
      if (error instanceof BrowserAuthError) {
        if (error.errorCode === "user_cancelled" || error.errorCode === "popup_window_error") {
          return { kind: "cancelled" };
        }
      }
      if (error instanceof ServerError) {
        return { kind: "failed", reason: error.errorMessage || error.message };
      }
      const message = error instanceof Error ? error.message : String(error);
      return { kind: "failed", reason: message };
    }

    // forceRefresh erzwingt, dass der naechste Token aus dem frischen
    // Login (popup) verwendet wird — nicht ein gecachter alter Token.
    const refreshed = await acquireToken(true);
    if (!refreshed) {
      return { kind: "failed", reason: "no_token_after_reauth" };
    }
    return { kind: "succeeded" };
  }
}
