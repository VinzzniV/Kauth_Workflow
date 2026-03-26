// Kapselt die konkrete Auth-Quelle, damit der Rest der App nicht von der Demo-Implementierung abhaengt.
import {
  demoLogin,
  demoLogout,
  getDemoAuthToken,
  getDemoLoginUsers,
  getMe,
  setDemoAuthToken,
} from "../services/lifecycleApi";
import type { DemoLoginResponse, DemoLoginUserOption, Me } from "../types/auth";

export type IIdentityProvider = {
  readonly providerKind: string;
  getStoredToken: () => string | null;
  setStoredToken: (token: string | null) => void;
  getCurrentUser: () => Promise<Me>;
  getLoginOptions: () => Promise<DemoLoginUserOption[]>;
  loginWithUsername: (username: string) => Promise<DemoLoginResponse>;
  logout: () => Promise<void>;
};

class DemoIdentityProvider implements IIdentityProvider {
  public readonly providerKind = "demo";

  // Die Demo-Implementierung delegiert alle Aufrufe an den zentralen API-Service.
  public getStoredToken(): string | null {
    return getDemoAuthToken();
  }

  public setStoredToken(token: string | null): void {
    setDemoAuthToken(token);
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

// TODO(real-auth): swap this with an Entra/SSO provider implementing
// the same contract once company auth is introduced.
export const identityProvider: IIdentityProvider = new DemoIdentityProvider();
