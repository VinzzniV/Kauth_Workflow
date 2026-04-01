// Verwaltet die Anmeldung und stellt den globalen Session-Zustand fuer das Frontend bereit.
import {
  useCallback,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import { identityProvider, isEntraMode } from "./IdentityProvider";
import { handleMsalRedirect } from "./EntraIdentityProvider";
import type { Me, SimulationLoginUserOption } from "../types/auth";
import { AuthContext } from "./useAuth";
import type { AuthStatus } from "./useAuth";

const SIMULATION_USERS_REFRESH_EVENT = "sim-users-refresh";

export function AuthProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<AuthStatus>("loading");
  const [currentUser, setCurrentUser] = useState<Me | null>(null);
  const [simulationUsers, setSimulationUsers] = useState<SimulationLoginUserOption[]>([]);
  const [usersLoading, setUsersLoading] = useState<boolean>(false);
  const [usersError, setUsersError] = useState<string | null>(null);
  const [loginError, setLoginError] = useState<string | null>(null);

  // Laedt die verfuegbaren synchronisierten Benutzer fuer die Login-Seite im Dev-Simulationsmodus.
  const reloadUsers = useCallback(async () => {
    if (isEntraMode()) return;

    setUsersLoading(true);
    setUsersError(null);

    try {
      const users = await identityProvider.getLoginOptions();
      setSimulationUsers(users);
    } catch (error) {
      const message = error instanceof Error ? error.message : "Simulationsbenutzer konnten nicht geladen werden.";
      setUsersError(message);
      setSimulationUsers([]);
    } finally {
      setUsersLoading(false);
    }
  }, []);

  // Baut eine vorhandene Session aus dem gespeicherten Token wieder auf.
  const refreshMe = useCallback(async () => {
    const token = await Promise.resolve(identityProvider.getStoredToken());
    if (!token) {
      setCurrentUser(null);
      setStatus("unauthenticated");
      setLoginError(null);
      return;
    }

    setStatus("loading");

    try {
      const me = await identityProvider.getCurrentUser();
      setCurrentUser(me);
      setStatus("authenticated");
      setLoginError(null);
    } catch (error) {
      const message = error instanceof Error ? error.message : "Anmeldung fehlgeschlagen.";
      setCurrentUser(null);
      setStatus("unauthenticated");
      setLoginError(
        message === "Backend-Fehler (HTTP 401)."
          ? (isEntraMode()
            ? "Ihr Microsoft-Konto ist angemeldet, aber in dieser Anwendung nicht freigeschaltet."
            : "Der simulierte Benutzer ist in dieser Anwendung nicht freigeschaltet.")
          : message
      );
    }
  }, []);

  // Fuehrt den Login aus und aktualisiert danach den aktuellen Benutzer.
  const login = useCallback(async (userId: number) => {
    setLoginError(null);

    try {
      const response = await identityProvider.loginAsUser(userId);
      identityProvider.setStoredToken(response.token);
      const me = await identityProvider.getCurrentUser();
      setCurrentUser(me);
      setStatus("authenticated");
      return true;
    } catch (error) {
      const message = error instanceof Error ? error.message : "Login fehlgeschlagen.";
      identityProvider.setStoredToken(null);
      setLoginError(message);
      setCurrentUser(null);
      setStatus("unauthenticated");
      return false;
    }
  }, []);

  // Loescht die lokale Session.
  const logout = useCallback(async () => {
    try {
      await identityProvider.logout();
    } catch {
      // Ignore network/logout errors.
    } finally {
      identityProvider.setStoredToken(null);
      setCurrentUser(null);
      setStatus("unauthenticated");
      setLoginError(null);
    }
  }, []);

  // App-Start: Session wiederherstellen oder Entra-Redirect verarbeiten.
  useEffect(() => {
    const initialize = async () => {
      try {
        if (isEntraMode()) {
          // Handle the return from a Microsoft login redirect.
          const account = await handleMsalRedirect();
          if (account) {
            // User returned from Entra login — fetch their profile.
            await refreshMe();
          } else {
            setStatus("unauthenticated");
          }
        } else {
          // Dev simulation mode: load synchronized users and try to restore session.
          void reloadUsers();
          await refreshMe();
        }
      } catch (error) {
        const message =
          error instanceof Error ? error.message : "Anmeldung konnte nicht initialisiert werden.";
        setLoginError(message);
        setCurrentUser(null);
        setStatus("unauthenticated");
      }
    };
    void initialize();
  }, [refreshMe, reloadUsers]);

  useEffect(() => {
    const handleInvalidAuth = () => {
      identityProvider.setStoredToken(null);
      setCurrentUser(null);
      setStatus("unauthenticated");
      setLoginError("Die Sitzung ist nicht mehr gültig. Bitte erneut anmelden.");
    };

    const handleRefreshSimulationUsers = () => {
      void reloadUsers();
    };

    window.addEventListener("auth-invalid", handleInvalidAuth);
    window.addEventListener(SIMULATION_USERS_REFRESH_EVENT, handleRefreshSimulationUsers);
    return () => {
      window.removeEventListener("auth-invalid", handleInvalidAuth);
      window.removeEventListener(SIMULATION_USERS_REFRESH_EVENT, handleRefreshSimulationUsers);
    };
  }, [reloadUsers]);

  const value = useMemo(
    () => ({
      status,
      currentUser,
      simulationUsers,
      usersLoading,
      usersError,
      loginError,
      login,
      logout,
      reloadUsers,
      refreshMe,
    }),
    [status, currentUser, simulationUsers, usersLoading, usersError, loginError, login, logout, reloadUsers, refreshMe]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
