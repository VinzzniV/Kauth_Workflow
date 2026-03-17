import { createContext, useContext } from "react";
import type { DemoLoginUserOption, Me } from "../types/auth";

export type AuthStatus = "loading" | "authenticated" | "unauthenticated";

type AuthContextValue = {
  status: AuthStatus;
  currentUser: Me | null;
  demoUsers: DemoLoginUserOption[];
  usersLoading: boolean;
  usersError: string | null;
  loginError: string | null;
  login: (username: string) => Promise<boolean>;
  logout: () => Promise<void>;
  reloadUsers: () => Promise<void>;
  refreshMe: () => Promise<void>;
};

export const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used within AuthProvider.");
  }

  return context;
}
