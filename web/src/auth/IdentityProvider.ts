// Kapselt die konkrete Auth-Quelle, damit der Rest der App nicht von der Demo-Implementierung abhaengt.
import {
  demoLogin,
  demoLogout,
  getDemoAuthToken,
  getDemoLoginUsers,
  getMe,
  setDemoAuthToken,
} from "../services/lifecycleApi";
import { EntraIdentityProvider } from "./EntraIdentityProvider";
import type { DemoLoginResponse, DemoLoginUserOption, Me } from "../types/auth";

export type IIdentityProvider = {
  readonly providerKind: string;
  getStoredToken: () => string | null | Promise<string | null>;
  setStoredToken: (token: string | null) => void;
  refreshAfterUnauthorized: () => Promise<boolean>;
  getCurrentUser: () => Promise<Me>;
  getLoginOptions: () => Promise<DemoLoginUserOption[]>;
  loginWithUsername: (username: string) => Promise<DemoLoginResponse>;
  logout: () => Promise<void>;
};

class DemoIdentityProvider implements IIdentityProvider {
  public readonly providerKind = "demo";

  public getStoredToken(): string | null {
    return getDemoAuthToken();
  }

  public setStoredToken(token: string | null): void {
    setDemoAuthToken(token);
  }

  public async refreshAfterUnauthorized(): Promise<boolean> {
    return false;
  }

  public getCurrentUser(): Promise<Me> {
    return getMe();
  }

  public getLoginOptions(): Promise<DemoLoginUserOption[]> {
    return getDemoLoginUsers();
  }

  public loginWithUsername(username: string): Promise<DemoLoginResponse> {
    return demoLogin(username);
  }

  public logout(): Promise<void> {
    return demoLogout();
  }
}

export function getAuthMode(): string {
  return (import.meta.env.VITE_AUTH_MODE ?? "demo").trim().toLowerCase();
}

export function isEntraMode(): boolean {
  return getAuthMode() === "entra";
}

function createIdentityProvider(): IIdentityProvider {
  if (isEntraMode()) {
    return new EntraIdentityProvider();
  }
  return new DemoIdentityProvider();
}

export const identityProvider: IIdentityProvider = createIdentityProvider();
