import {
  PublicClientApplication,
  type AccountInfo,
  InteractionRequiredAuthError,
} from "@azure/msal-browser";
import { msalConfig, loginRequest } from "./msalConfig";
import type { IIdentityProvider } from "./IdentityProvider";
import type { DemoLoginResponse, DemoLoginUserOption, Me } from "../types/auth";
import { requestJson } from "../services/api/client";

const msalInstance = new PublicClientApplication(msalConfig);
let msalInitialized: Promise<void> | null = null;

// MSAL redirect promise must be handled once at app start.
let redirectHandled = false;

async function ensureMsalInitialized(): Promise<void> {
  msalInitialized ??= msalInstance.initialize();
  await msalInitialized;
}

export async function handleMsalRedirect(): Promise<AccountInfo | null> {
  await ensureMsalInitialized();

  if (redirectHandled) return msalInstance.getActiveAccount();
  redirectHandled = true;

  const response = await msalInstance.handleRedirectPromise();
  if (response?.account) {
    msalInstance.setActiveAccount(response.account);
    return response.account;
  }

  const accounts = msalInstance.getAllAccounts();
  if (accounts.length > 0) {
    msalInstance.setActiveAccount(accounts[0]);
    return accounts[0];
  }

  return null;
}

async function acquireToken(forceRefresh = false): Promise<string | null> {
  await ensureMsalInitialized();

  const account = msalInstance.getActiveAccount();
  if (!account) return null;

  try {
    const result = await msalInstance.acquireTokenSilent({
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

  public setStoredToken(_token: string | null): void {
    // MSAL manages its own token cache — this is a no-op for Entra mode.
  }

  public async refreshAfterUnauthorized(): Promise<boolean> {
    const token = await acquireToken(true);
    return Boolean(token);
  }

  public async getCurrentUser(): Promise<Me> {
    return requestJson<Me>("/me");
  }

  public async getLoginOptions(): Promise<DemoLoginUserOption[]> {
    // Not applicable for Entra — return empty list.
    return [];
  }

  public async loginWithUsername(_username: string): Promise<DemoLoginResponse> {
    await ensureMsalInitialized();

    // Trigger the MSAL redirect flow. This navigates away from the SPA,
    // so the returned promise will not resolve in the current page load.
    await msalInstance.loginRedirect(loginRequest);

    // This line is only reached if redirect didn't happen (shouldn't occur).
    throw new Error("Redirect to Microsoft login initiated.");
  }

  public async logout(): Promise<void> {
    await ensureMsalInitialized();

    await msalInstance.logoutRedirect({
      postLogoutRedirectUri: msalConfig.auth.redirectUri as string,
    });
  }
}
