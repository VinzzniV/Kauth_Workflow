import { createContext, useContext } from "react";
import type { AppFeature, DashboardPersona, RoleCapabilities } from "./roleModel";
import type { AuthStatus } from "./useAuth";
import type { Me } from "../types/auth";

type CurrentUserContextValue = {
  status: AuthStatus;
  currentUser: Me | null;
  displayName: string;
  roles: string[];
  roleLabels: string[];
  groups: string[];
  permissions: string[];
  capabilities: RoleCapabilities;
  defaultRoute: string;
  activeView: DashboardPersona;
  setActiveView: (persona: DashboardPersona) => void;
  canAccessFeature: (feature: AppFeature) => boolean;
  refreshCurrentUser: () => Promise<void>;
};

export const CurrentUserContext = createContext<CurrentUserContextValue | undefined>(undefined);

export function useCurrentUser() {
  const context = useContext(CurrentUserContext);
  if (!context) {
    throw new Error("useCurrentUser must be used within CurrentUserProvider.");
  }

  return context;
}
