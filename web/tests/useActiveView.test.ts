import { act, renderHook } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { deriveDefaultPersona, isValidActiveView, useActiveView } from "../src/hooks/useActiveView";
import type { RoleCapabilities } from "../src/auth/roleModel";

function makeLocalStorageMock() {
  let store: Record<string, string> = {};
  return {
    getItem: (key: string) => store[key] ?? null,
    setItem: (key: string, value: string) => { store[key] = value; },
    removeItem: (key: string) => { delete store[key]; },
    clear: () => { store = {}; },
    get length() { return Object.keys(store).length; },
    key: (i: number) => Object.keys(store)[i] ?? null,
  };
}

const localStorageMock = makeLocalStorageMock();
vi.stubGlobal("localStorage", localStorageMock);

// --- Fixtures ---

function makeCapabilities(overrides: Partial<RoleCapabilities> = {}): RoleCapabilities {
  return {
    roleKeys: [],
    permissionKeys: [],
    hasMultipleRoles: false,
    hasAdminRole: false,
    hasHrRole: false,
    hasManagerRole: false,
    hasWorkerRole: false,
    hasReaderRole: false,
    hasReadRole: false,
    hasProcessActorRole: false,
    canCreateWorkflow: false,
    canAccessSupervisorStep: false,
    canAccessTechnicalTasks: false,
    canManageAdminConfiguration: false,
    canAccessWorkflowOverview: false,
    dashboardPersona: "generic",
    ...overrides,
  };
}

const adminCaps = makeCapabilities({ hasAdminRole: true, hasReadRole: true });
const hrCaps = makeCapabilities({ hasHrRole: true, hasReadRole: true });
const managerCaps = makeCapabilities({ hasManagerRole: true, hasReadRole: true });
const workerCaps = makeCapabilities({ hasWorkerRole: true, hasReadRole: true });
const readerCaps = makeCapabilities({ hasReaderRole: true, hasReadRole: true });
const genericCaps = makeCapabilities();
const multiCaps = makeCapabilities({ hasAdminRole: true, hasHrRole: true, hasReadRole: true, hasMultipleRoles: true });

// --- deriveDefaultPersona ---

describe("deriveDefaultPersona", () => {
  it("returns admin for admin role", () => {
    expect(deriveDefaultPersona(adminCaps)).toBe("admin");
  });

  it("returns hr for hr role", () => {
    expect(deriveDefaultPersona(hrCaps)).toBe("hr");
  });

  it("returns manager for manager role", () => {
    expect(deriveDefaultPersona(managerCaps)).toBe("manager");
  });

  it("returns worker for worker role", () => {
    expect(deriveDefaultPersona(workerCaps)).toBe("worker");
  });

  it("returns reader for reader role", () => {
    expect(deriveDefaultPersona(readerCaps)).toBe("reader");
  });

  it("returns generic when no recognized role", () => {
    expect(deriveDefaultPersona(genericCaps)).toBe("generic");
  });

  it("admin takes precedence over hr in multi-role", () => {
    expect(deriveDefaultPersona(multiCaps)).toBe("admin");
  });

  it("hr takes precedence over manager", () => {
    const caps = makeCapabilities({ hasHrRole: true, hasManagerRole: true });
    expect(deriveDefaultPersona(caps)).toBe("hr");
  });
});

// --- isValidActiveView ---

describe("isValidActiveView", () => {
  it("accepts allowed persona for role", () => {
    expect(isValidActiveView("admin", adminCaps)).toBe(true);
    expect(isValidActiveView("hr", hrCaps)).toBe(true);
    expect(isValidActiveView("generic", genericCaps)).toBe(true);
  });

  it("rejects persona that requires a role the user does not have", () => {
    expect(isValidActiveView("admin", hrCaps)).toBe(false);
    expect(isValidActiveView("hr", workerCaps)).toBe(false);
    expect(isValidActiveView("manager", readerCaps)).toBe(false);
  });

  it("rejects unknown strings", () => {
    expect(isValidActiveView("superuser", adminCaps)).toBe(false);
    expect(isValidActiveView("", adminCaps)).toBe(false);
  });

  it("rejects non-string values", () => {
    expect(isValidActiveView(null, adminCaps)).toBe(false);
    expect(isValidActiveView(42, adminCaps)).toBe(false);
    expect(isValidActiveView(undefined, adminCaps)).toBe(false);
  });

  it("always accepts generic", () => {
    expect(isValidActiveView("generic", genericCaps)).toBe(true);
    expect(isValidActiveView("generic", adminCaps)).toBe(true);
  });
});

// --- useActiveView: Fallback-Kaskade + Persistenz ---

describe("useActiveView", () => {
  const PERSON_ID = "user-42";
  const STORAGE_KEY = `kauth.activeView.${PERSON_ID}`;

  beforeEach(() => {
    localStorageMock.clear();
    vi.restoreAllMocks();
  });

  afterEach(() => {
    localStorageMock.clear();
  });

  it("uses default persona when localStorage is empty", () => {
    const { result } = renderHook(() => useActiveView(PERSON_ID, adminCaps));
    expect(result.current.activeView).toBe("admin");
  });

  it("reads persisted persona from localStorage", () => {
    localStorage.setItem(STORAGE_KEY, "hr");
    const caps = makeCapabilities({ hasAdminRole: true, hasHrRole: true, hasReadRole: true });
    const { result } = renderHook(() => useActiveView(PERSON_ID, caps));
    expect(result.current.activeView).toBe("hr");
  });

  it("falls back to default when persisted persona is not allowed by current capabilities", () => {
    localStorage.setItem(STORAGE_KEY, "admin");
    // User no longer has admin role
    const { result } = renderHook(() => useActiveView(PERSON_ID, hrCaps));
    expect(result.current.activeView).toBe("hr");
  });

  it("falls back to default when localStorage contains an unknown value", () => {
    localStorage.setItem(STORAGE_KEY, "superuser");
    const { result } = renderHook(() => useActiveView(PERSON_ID, adminCaps));
    expect(result.current.activeView).toBe("admin");
  });

  it("falls back to generic when no role and no persisted value", () => {
    const { result } = renderHook(() => useActiveView(PERSON_ID, genericCaps));
    expect(result.current.activeView).toBe("generic");
  });

  it("uses default persona when personId is null", () => {
    const { result } = renderHook(() => useActiveView(null, managerCaps));
    expect(result.current.activeView).toBe("manager");
  });

  it("setActiveView updates state and persists to localStorage", () => {
    const caps = makeCapabilities({ hasAdminRole: true, hasHrRole: true, hasReadRole: true });
    const { result } = renderHook(() => useActiveView(PERSON_ID, caps));
    expect(result.current.activeView).toBe("admin");

    act(() => {
      result.current.setActiveView("hr");
    });

    expect(result.current.activeView).toBe("hr");
    expect(localStorage.getItem(STORAGE_KEY)).toBe("hr");
  });

  it("setActiveView ignores persona not allowed by capabilities", () => {
    const { result } = renderHook(() => useActiveView(PERSON_ID, hrCaps));
    act(() => {
      result.current.setActiveView("admin");
    });
    expect(result.current.activeView).toBe("hr");
    expect(localStorage.getItem(STORAGE_KEY)).toBeNull();
  });

  it("setActiveView does not write to localStorage when personId is null", () => {
    const { result } = renderHook(() => useActiveView(null, adminCaps));
    act(() => {
      result.current.setActiveView("admin");
    });
    expect(result.current.activeView).toBe("admin");
    expect(localStorageMock.length).toBe(0);
  });

  it("re-syncs when capabilities change and persisted persona becomes invalid", () => {
    const caps = makeCapabilities({ hasAdminRole: true, hasHrRole: true, hasReadRole: true });
    localStorage.setItem(STORAGE_KEY, "admin");

    let currentCaps = caps;
    const { result, rerender } = renderHook(() => useActiveView(PERSON_ID, currentCaps));
    expect(result.current.activeView).toBe("admin");

    // Simulate role change: admin removed
    currentCaps = makeCapabilities({ hasHrRole: true, hasReadRole: true });
    rerender();

    expect(result.current.activeView).toBe("hr");
  });

  it("handles localStorage read failure gracefully and uses default", () => {
    vi.spyOn(localStorageMock, "getItem").mockImplementation(() => {
      throw new Error("storage error");
    });
    const { result } = renderHook(() => useActiveView(PERSON_ID, adminCaps));
    expect(result.current.activeView).toBe("admin");
  });
});
