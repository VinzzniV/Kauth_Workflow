import { useCallback, useEffect, useState } from "react";
import type { DashboardPersona, RoleCapabilities } from "../auth/roleModel";

const STORAGE_KEY_PREFIX = "kauth.activeView.";

const PERSONA_PRIORITY: DashboardPersona[] = ["admin", "hr", "manager", "worker", "reader", "generic"];

// Leitet die Default-Persona aus der Vorrangskette ab (admin > hr > manager > worker > reader > generic).
export function deriveDefaultPersona(capabilities: RoleCapabilities): DashboardPersona {
  if (capabilities.hasAdminRole) return "admin";
  if (capabilities.hasHrRole) return "hr";
  if (capabilities.hasManagerRole) return "manager";
  if (capabilities.hasWorkerRole) return "worker";
  if (capabilities.hasReaderRole) return "reader";
  return "generic";
}

// Prueft, ob eine gegebene Persona fuer die aktuellen Capabilities zulaessig ist.
export function isValidActiveView(value: unknown, capabilities: RoleCapabilities): value is DashboardPersona {
  if (typeof value !== "string") return false;
  if (!PERSONA_PRIORITY.includes(value as DashboardPersona)) return false;

  switch (value as DashboardPersona) {
    case "admin":
      return capabilities.hasAdminRole;
    case "hr":
      return capabilities.hasHrRole;
    case "manager":
      return capabilities.hasManagerRole;
    case "worker":
      return capabilities.hasWorkerRole;
    case "reader":
      return capabilities.hasReaderRole;
    case "generic":
      return true;
  }
}

function storageKey(personId: string): string {
  return `${STORAGE_KEY_PREFIX}${personId}`;
}

function readFromStorage(personId: string, capabilities: RoleCapabilities): DashboardPersona | null {
  // SSR-safe: localStorage ist nur clientseitig verfuegbar.
  if (typeof window === "undefined") return null;
  try {
    const raw = window.localStorage.getItem(storageKey(personId));
    if (!raw) return null;
    return isValidActiveView(raw, capabilities) ? raw : null;
  } catch {
    return null;
  }
}

function writeToStorage(personId: string, persona: DashboardPersona): void {
  if (typeof window === "undefined") return;
  try {
    window.localStorage.setItem(storageKey(personId), persona);
  } catch {
    // localStorage nicht verfuegbar — ignorieren.
  }
}

function resolveView(personId: string | null, capabilities: RoleCapabilities): DashboardPersona {
  const defaultPersona = deriveDefaultPersona(capabilities);
  if (!personId) return defaultPersona;
  return readFromStorage(personId, capabilities) ?? defaultPersona;
}

export type ActiveViewResult = {
  activeView: DashboardPersona;
  setActiveView: (persona: DashboardPersona) => void;
};

// Verwaltet die aktive Ansicht eines Benutzers mit Persistenz und Fallback-Kaskade.
// Steuert ausschliesslich Sicht-Konsumenten — niemals Rechte oder Capabilities.
export function useActiveView(personId: string | null, capabilities: RoleCapabilities): ActiveViewResult {
  const [activeView, setActiveViewState] = useState<DashboardPersona>(() =>
    resolveView(personId, capabilities)
  );

  // Re-sync wenn Person oder Capabilities wechseln (z. B. Rolle entzogen).
  useEffect(() => {
    setActiveViewState(resolveView(personId, capabilities));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [personId, capabilities]);

  const setActiveView = useCallback(
    (persona: DashboardPersona) => {
      if (!isValidActiveView(persona, capabilities)) return;
      setActiveViewState(persona);
      if (personId) {
        writeToStorage(personId, persona);
      }
    },
    [personId, capabilities]
  );

  return { activeView, setActiveView };
}
