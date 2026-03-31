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
import type { DemoLoginUserOption, Me } from "../types/auth";
import { AuthContext } from "./useAuth";
import type { AuthStatus } from "./useAuth";

const DEMO_USERS_REFRESH_EVENT = "demo-users-refresh";

export function AuthProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<AuthStatus>("loading");
  const [currentUser, setCurrentUser] = useState<Me | null>(null);
  const [demoUsers, setDemoUsers] = useState<DemoLoginUserOption[]>([]);
  const [usersLoading, setUsersLoading] = useState<boolean>(false);
  const [usersError, setUsersError] = useState<string | null>(null);
  const [loginError, setLoginError] = useState<string | null>(null);

  // Laedt die verfuegbaren Demo-Benutzer fuer die Login-Seite (nur im Demo-Modus).
  const reloadUsers = useCallback(async () => {
    if (isEntraMode()) return;

    setUsersLoading(true);
    setUsersError(null);

    try {
      const users = await identityProvider.getLoginOptions();
      setDemoUsers(users);
    } catch (error) {
      const message = error instanceof Error ? error.message : "Demo-Benutzer konnten nicht geladen werden.";
      setUsersError(message);
      setDemoUsers([]);
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
          ? "Ihr Microsoft-Konto ist angemeldet, aber in dieser Anwendung nicht freigeschaltet."
          : message
      );
    }
  }, []);

  // Fuehrt den Login aus und aktualisiert danach den aktuellen Benutzer.
  const login = useCallback(async (username: string) => {
    setLoginError(null);

    try {
      const response = await identityProvider.loginWithUsername(username);
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
          // Demo mode: load demo users and try to restore session.
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

    const handleRefreshDemoUsers = () => {
      void reloadUsers();
    };

    window.addEventListener("auth-invalid", handleInvalidAuth);
    // Keep backwards compatibility for demo mode event.
    window.addEventListener("demo-auth-invalid", handleInvalidAuth);
    window.addEventListener(DEMO_USERS_REFRESH_EVENT, handleRefreshDemoUsers);
    return () => {
      window.removeEventListener("auth-invalid", handleInvalidAuth);
      window.removeEventListener("demo-auth-invalid", handleInvalidAuth);
      window.removeEventListener(DEMO_USERS_REFRESH_EVENT, handleRefreshDemoUsers);
    };
  }, [reloadUsers]);

  const value = useMemo(
    () => ({
      status,
      currentUser,
      demoUsers,
      usersLoading,
      usersError,
      loginError,
      login,
      logout,
      reloadUsers,
      refreshMe,
    }),
    [status, currentUser, demoUsers, usersLoading, usersError, loginError, login, logout, reloadUsers, refreshMe]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
