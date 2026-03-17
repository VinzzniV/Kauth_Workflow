// Leitet aus dem Auth-Benutzer alle UI-relevanten Rollen-, Label- und Routing-Informationen ab.
import { useCallback, useMemo, type ReactNode } from "react";
import { useAuth } from "./useAuth";
import {
  canAccessFeature,
  deriveRoleCapabilities,
  getDefaultRoute,
  toRoleLabel,
  type AppFeature,
} from "./roleModel";
import { CurrentUserContext } from "./useCurrentUser";

export function CurrentUserProvider({ children }: { children: ReactNode }) {
  const { status, currentUser, refreshMe } = useAuth();

  // Diese abgeleiteten Werte werden im Routing, in der Navigation und in Guards mehrfach verwendet.
  const roles = useMemo(() => currentUser?.roles ?? [], [currentUser]);
  const groups = useMemo(() => currentUser?.groups ?? [], [currentUser]);
  const capabilities = useMemo(() => deriveRoleCapabilities(roles), [roles]);
  const defaultRoute = useMemo(() => getDefaultRoute(capabilities), [capabilities]);

  const canAccessFeatureSafe = useCallback(
    (feature: AppFeature) => canAccessFeature(capabilities, feature),
    [capabilities]
  );

  // Der Context liefert sowohl Rohdaten als auch bereits lesbar aufbereitete Benutzerinformationen.
  const roleLabels = useMemo(() => roles.map(toRoleLabel), [roles]);

  const value = useMemo(
    () => ({
      status,
      currentUser,
      displayName: currentUser?.displayName ?? "",
      roles,
      roleLabels,
      groups,
      capabilities,
      defaultRoute,
      canAccessFeature: canAccessFeatureSafe,
      refreshCurrentUser: refreshMe,
    }),
    [
      status,
      currentUser,
      roles,
      roleLabels,
      groups,
      capabilities,
      defaultRoute,
      canAccessFeatureSafe,
      refreshMe,
    ]
  );

  return <CurrentUserContext.Provider value={value}>{children}</CurrentUserContext.Provider>;
}
