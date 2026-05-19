// Kapselt die konkrete Auth-Quelle, damit der Rest der App nicht von der konkreten Login-Art abhaengt.
import {
  getDevSimAuthToken,
  getSimulationLoginUsers,
  getMe,
  setDevSimAuthToken,
  simulationLogin,
  simulationLogout,
} from "../services/authApi";
import { EntraIdentityProvider } from "./EntraIdentityProvider";
import { getAuthMode as getConfiguredAuthMode } from "../config/appRuntimeConfig";
import type { Me, SimulationLoginResponse, SimulationLoginUserOption } from "../types/auth";

// Slice AGA-N2: Interactive Re-Auth fuer Approval-Flow.
// Entra: MSAL-Popup mit `prompt: 'login'` + force-refreshen, damit der naechste
//        API-Call ein Access-Token mit frischem auth_time-Claim traegt.
// Dev-Sim: bewusster No-Op (kein echter IdP fuer auth_time-Frische).
export type InteractiveReauthOutcome =
  | { kind: "skipped-dev-sim" }
  | { kind: "succeeded" }
  | { kind: "cancelled" }
  | { kind: "failed"; reason: string };

export type IIdentityProvider = {
  readonly providerKind: string;
  getStoredToken: () => string | null | Promise<string | null>;
  setStoredToken: (token: string | null) => void;
  refreshAfterUnauthorized: () => Promise<boolean>;
  getCurrentUser: () => Promise<Me>;
  getLoginOptions: () => Promise<SimulationLoginUserOption[]>;
  loginAsUser: (userId: number) => Promise<SimulationLoginResponse>;
  logout: () => Promise<void>;
  triggerInteractiveReauth: () => Promise<InteractiveReauthOutcome>;
};

class DevSimulationIdentityProvider implements IIdentityProvider {
  public readonly providerKind = "dev-sim";

  public getStoredToken(): string | null {
    return getDevSimAuthToken();
  }

  public setStoredToken(token: string | null): void {
    setDevSimAuthToken(token);
  }

  public async refreshAfterUnauthorized(): Promise<boolean> {
    return false;
  }

  public getCurrentUser(): Promise<Me> {
    return getMe();
  }

  public getLoginOptions(): Promise<SimulationLoginUserOption[]> {
    return getSimulationLoginUsers();
  }

  public loginAsUser(userId: number): Promise<SimulationLoginResponse> {
    return simulationLogin(userId);
  }

  public logout(): Promise<void> {
    return simulationLogout();
  }

  public async triggerInteractiveReauth(): Promise<InteractiveReauthOutcome> {
    // Dev-Sim hat keinen echten IdP, gegen den ein `prompt: 'login'` laufen koennte.
    // Bewusster Fallback fuer lokale Entwicklung — die Backend-Schranke fuer
    // auth_time-Frische ist in dev-sim ebenfalls deaktiviert.
    return { kind: "skipped-dev-sim" };
  }
}

export function getAuthMode(): string {
  return getConfiguredAuthMode();
}

export function isEntraMode(): boolean {
  return getAuthMode() === "entra";
}

function createIdentityProvider(): IIdentityProvider {
  if (isEntraMode()) {
    return new EntraIdentityProvider();
  }
  return new DevSimulationIdentityProvider();
}

let cachedIdentityProvider: IIdentityProvider | null = null;

// Lazy-Proxy: bricht den Modul-Init-Cycle zu EntraIdentityProvider/client.ts.
// Der konkrete Provider wird erst beim ersten Property-Zugriff erstellt,
// wenn alle Module komplett geladen sind.
export const identityProvider: IIdentityProvider = new Proxy({} as IIdentityProvider, {
  get(_target, prop) {
    cachedIdentityProvider ??= createIdentityProvider();
    const value = Reflect.get(cachedIdentityProvider, prop);
    return typeof value === "function" ? value.bind(cachedIdentityProvider) : value;
  },
});
