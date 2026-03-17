// Verwaltet die Demo-Anmeldung und stellt den globalen Session-Zustand fuer das Frontend bereit.
import {
  useCallback,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import { identityProvider } from "./IdentityProvider";
import type { DemoLoginUserOption, Me } from "../types/auth";
import { AuthContext } from "./useAuth";
import type { AuthStatus } from "./useAuth";

export function AuthProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<AuthStatus>("loading");
  const [currentUser, setCurrentUser] = useState<Me | null>(null);
  const [demoUsers, setDemoUsers] = useState<DemoLoginUserOption[]>([]);
  const [usersLoading, setUsersLoading] = useState<boolean>(false);
  const [usersError, setUsersError] = useState<string | null>(null);
  const [loginError, setLoginError] = useState<string | null>(null);

  // Laedt die verfuegbaren Demo-Benutzer fuer die Login-Seite.
  const reloadUsers = useCallback(async () => {
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
    const token = identityProvider.getStoredToken();
    if (!token) {
      setCurrentUser(null);
      setStatus("unauthenticated");
      return;
    }

    setStatus("loading");

    try {
      const me = await identityProvider.getCurrentUser();
      setCurrentUser(me);
      setStatus("authenticated");
      setLoginError(null);
    } catch {
      identityProvider.setStoredToken(null);
      setCurrentUser(null);
      setStatus("unauthenticated");
    }
  }, []);

  // Fuehrt den Demo-Login aus und aktualisiert danach den aktuellen Benutzer.
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
      const message = error instanceof Error ? error.message : "Demo-Login fehlgeschlagen.";
      identityProvider.setStoredToken(null);
      setLoginError(message);
      setCurrentUser(null);
      setStatus("unauthenticated");
      return false;
    }
  }, []);

  // Loescht die lokale Session auch dann, wenn der Logout-Call im Demo-Modus fehlschlaegt.
  const logout = useCallback(async () => {
    try {
      await identityProvider.logout();
    } catch {
      // Ignore network/logout errors for demo sessions.
    } finally {
      identityProvider.setStoredToken(null);
      setCurrentUser(null);
      setStatus("unauthenticated");
      setLoginError(null);
    }
  }, []);

  // Beim App-Start werden sowohl Login-Optionen als auch eine evtl. bestehende Session geladen.
  useEffect(() => {
    void reloadUsers();
    void refreshMe();
  }, [refreshMe, reloadUsers]);

  useEffect(() => {
    const handleInvalidDemoAuth = () => {
      identityProvider.setStoredToken(null);
      setCurrentUser(null);
      setStatus("unauthenticated");
      setLoginError("Die Sitzung ist nicht mehr gültig. Bitte erneut anmelden.");
    };

    window.addEventListener("demo-auth-invalid", handleInvalidDemoAuth);
    return () => {
      window.removeEventListener("demo-auth-invalid", handleInvalidDemoAuth);
    };
  }, []);

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
