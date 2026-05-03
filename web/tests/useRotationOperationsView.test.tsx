import type { ReactNode } from "react";
import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  isOperationallyOpen,
  isUpcomingAnchorDate,
  useRotationOperationsView,
} from "../src/hooks/useRotationOperationsView";
import { CurrentUserContext } from "../src/auth/useCurrentUser";
import { canAccessFeature, deriveRoleCapabilities, toRoleLabel } from "../src/auth/roleModel";
import * as taskApi from "../src/services/taskApi";
import { createRotationTask } from "./testUtils";

vi.mock("../src/services/taskApi", async () => {
  const actual = await vi.importActual<typeof import("../src/services/taskApi")>("../src/services/taskApi");
  return { ...actual, getMyTasks: vi.fn() };
});

const mockedGetMyTasks = vi.mocked(taskApi.getMyTasks);

type WrapperOptions = {
  scopedDepartmentId?: number | null;
};

function makeWrapper({ scopedDepartmentId = null }: WrapperOptions = {}) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const roleKeys = ["auth_worker"];
  const capabilities = deriveRoleCapabilities(roleKeys, []);
  const currentUser = {
    username: "tester",
    displayName: "Test User",
    email: "tester@example.com",
    roles: roleKeys,
    groups: [] as string[],
    permissions: [] as string[],
    permissionScopes: scopedDepartmentId !== null
      ? [{
          permissionKey: "tasks.execute_department",
          scope: "department",
          scopeDepartmentId: scopedDepartmentId,
          scopeDepartmentName: "IT",
        }]
      : [],
    directorySynced: false,
    departmentSource: "test",
    departmentOverrideActive: false,
  };

  return function Wrapper({ children }: { children: ReactNode }) {
    return (
      <QueryClientProvider client={queryClient}>
        <CurrentUserContext.Provider
          value={{
            status: "authenticated",
            currentUser,
            displayName: currentUser.displayName,
            roles: currentUser.roles,
            roleLabels: currentUser.roles.map(toRoleLabel),
            groups: currentUser.groups,
            permissions: currentUser.permissions,
            capabilities,
            defaultRoute: "/",
            canAccessFeature: (feature) => canAccessFeature(capabilities, feature),
            refreshCurrentUser: async () => undefined,
          }}
        >
          {children}
        </CurrentUserContext.Provider>
      </QueryClientProvider>
    );
  };
}

// Returns a yyyy-mm-dd string in the **local** calendar — matches the helper's
// midnight-today comparison regardless of test-machine timezone. ISO-string-then-slice
// would drift across the date boundary in non-UTC timezones.
function isoDateOffset(daysFromToday: number): string {
  const target = new Date();
  target.setHours(0, 0, 0, 0);
  target.setDate(target.getDate() + daysFromToday);
  const yyyy = target.getFullYear();
  const mm = String(target.getMonth() + 1).padStart(2, "0");
  const dd = String(target.getDate()).padStart(2, "0");
  return `${yyyy}-${mm}-${dd}`;
}

// ── Pure helpers ───────────────────────────────────────────────────────────────

describe("isOperationallyOpen", () => {
  it("returns true for open and in_progress", () => {
    const openTask = createRotationTask({ status: "open" }) as ReturnType<typeof createRotationTask>;
    const inProg = createRotationTask({ status: "in_progress" });
    expect(isOperationallyOpen(openTask as never)).toBe(true);
    expect(isOperationallyOpen(inProg as never)).toBe(true);
  });

  it("returns false for terminal statuses", () => {
    const done = createRotationTask({ status: "done" });
    const cancelled = createRotationTask({ status: "cancelled" });
    expect(isOperationallyOpen(done as never)).toBe(false);
    expect(isOperationallyOpen(cancelled as never)).toBe(false);
  });
});

describe("isUpcomingAnchorDate", () => {
  it("returns false for null anchor", () => {
    expect(isUpcomingAnchorDate(null, 14)).toBe(false);
  });

  it("returns false for malformed date", () => {
    expect(isUpcomingAnchorDate("not-a-date", 14)).toBe(false);
  });

  it("returns true for today within window", () => {
    expect(isUpcomingAnchorDate(isoDateOffset(0), 14)).toBe(true);
  });

  it("returns true for tomorrow within window", () => {
    expect(isUpcomingAnchorDate(isoDateOffset(1), 14)).toBe(true);
  });

  it("returns true for exact-window-edge day", () => {
    expect(isUpcomingAnchorDate(isoDateOffset(14), 14)).toBe(true);
  });

  it("returns false for one day past window", () => {
    expect(isUpcomingAnchorDate(isoDateOffset(15), 14)).toBe(false);
  });

  it("returns false for past dates", () => {
    expect(isUpcomingAnchorDate(isoDateOffset(-1), 14)).toBe(false);
  });

  it("treats window=0 as today-only", () => {
    expect(isUpcomingAnchorDate(isoDateOffset(0), 0)).toBe(true);
    expect(isUpcomingAnchorDate(isoDateOffset(1), 0)).toBe(false);
  });
});

// ── Hook integration ──────────────────────────────────────────────────────────

describe("useRotationOperationsView", () => {
  beforeEach(() => mockedGetMyTasks.mockReset());

  it("ignores non-rotation tasks", async () => {
    mockedGetMyTasks.mockResolvedValue([
      createRotationTask({ id: 1 }),
      // Non-rotation row: shape is loosely typed via createRotationTask but with workflow family
      {
        ...createRotationTask({ id: 2 }),
        taskFamily: "workflow",
        rotation: null,
      } as never,
    ]);

    const { result } = renderHook(() => useRotationOperationsView(), { wrapper: makeWrapper() });
    await waitFor(() => expect(result.current.rotationRows.length).toBe(1));
    expect(result.current.rotationRows[0]!.task.id).toBe(1);
  });

  it("filteredRows respects search across multiple fields", async () => {
    mockedGetMyTasks.mockResolvedValue([
      createRotationTask({ id: 1, title: "Hardware A" }, { displayName: "Anika Sattler" }),
      createRotationTask({ id: 2, title: "Hardware B" }, { displayName: "Bob Builder" }),
    ]);

    const { result } = renderHook(() => useRotationOperationsView(), { wrapper: makeWrapper() });
    await waitFor(() => expect(result.current.rotationRows.length).toBe(2));

    result.current.setters.setSearch("Bob");
    await waitFor(() => expect(result.current.filteredRows.length).toBe(1));
    expect(result.current.filteredRows[0]!.task.id).toBe(2);
  });

  it("filteredRows respects departmentFilter", async () => {
    mockedGetMyTasks.mockResolvedValue([
      createRotationTask({ id: 1 }, { departmentId: 9, departmentName: "IT" }),
      createRotationTask({ id: 2 }, { departmentId: 11, departmentName: "HR" }),
    ]);

    const { result } = renderHook(() => useRotationOperationsView(), { wrapper: makeWrapper() });
    await waitFor(() => expect(result.current.rotationRows.length).toBe(2));

    result.current.setters.setDepartmentFilter("11");
    await waitFor(() => expect(result.current.filteredRows.length).toBe(1));
    expect(result.current.filteredRows[0]!.rotation.departmentId).toBe(11);
  });

  it("onlyOpen excludes done/cancelled tasks", async () => {
    mockedGetMyTasks.mockResolvedValue([
      createRotationTask({ id: 1, status: "open" }),
      createRotationTask({ id: 2, status: "done" }),
      createRotationTask({ id: 3, status: "in_progress" }),
    ]);

    const { result } = renderHook(() => useRotationOperationsView(), { wrapper: makeWrapper() });
    await waitFor(() => expect(result.current.rotationRows.length).toBe(3));

    // default onlyOpen=true → only open + in_progress survive
    expect(result.current.filteredRows.map((r) => r.task.id).sort()).toEqual([1, 3]);

    result.current.setters.setOnlyOpen(false);
    await waitFor(() => expect(result.current.filteredRows.length).toBe(3));
  });

  it("inferredOwnDepartmentId resolves from currentUser.permissionScopes", async () => {
    mockedGetMyTasks.mockResolvedValue([]);

    const { result: scoped } = renderHook(() => useRotationOperationsView(), {
      wrapper: makeWrapper({ scopedDepartmentId: 42 }),
    });
    await waitFor(() => expect(scoped.current.inferredOwnDepartmentId).toBe(42));

    const { result: unscoped } = renderHook(() => useRotationOperationsView(), { wrapper: makeWrapper() });
    await waitFor(() => expect(unscoped.current.inferredOwnDepartmentId).toBeNull());
  });

  it("upcomingChanges aggregates by plan + anchor + trigger", async () => {
    const anchor = isoDateOffset(3);
    mockedGetMyTasks.mockResolvedValue([
      createRotationTask({ id: 1 }, { rotationPlanId: 7, anchorDate: anchor, triggerType: "enter" }),
      createRotationTask({ id: 2 }, { rotationPlanId: 7, anchorDate: anchor, triggerType: "enter" }),
      createRotationTask({ id: 3 }, { rotationPlanId: 7, anchorDate: anchor, triggerType: "exit" }),
    ]);

    const { result } = renderHook(() => useRotationOperationsView(), { wrapper: makeWrapper() });
    await waitFor(() => expect(result.current.upcomingChanges.length).toBe(2));

    const enter = result.current.upcomingChanges.find((c) => c.triggerType === "enter")!;
    const exit = result.current.upcomingChanges.find((c) => c.triggerType === "exit")!;
    expect(enter.taskCount).toBe(2);
    expect(enter.taskRefs.sort()).toEqual(["rot:1", "rot:2"]);
    expect(exit.taskCount).toBe(1);
    expect(exit.taskRefs).toEqual(["rot:3"]);
  });

  it("upcomingChanges drops tasks beyond changeWindowDays", async () => {
    mockedGetMyTasks.mockResolvedValue([
      createRotationTask({ id: 1 }, { rotationPlanId: 7, anchorDate: isoDateOffset(3) }),
      createRotationTask({ id: 2 }, { rotationPlanId: 8, anchorDate: isoDateOffset(60) }),
    ]);

    const { result } = renderHook(() => useRotationOperationsView(), { wrapper: makeWrapper() });
    await waitFor(() => expect(result.current.rotationRows.length).toBe(2));

    // default window=14 → only the 3-day-out task qualifies
    expect(result.current.upcomingChanges.map((c) => c.rotationPlanId)).toEqual([7]);
  });

  it("departmentSummaries counts per departmentId", async () => {
    mockedGetMyTasks.mockResolvedValue([
      createRotationTask({ id: 1, status: "open" }, { departmentId: 9, departmentName: "IT" }),
      createRotationTask({ id: 2, status: "in_progress" }, { departmentId: 9, departmentName: "IT" }),
      createRotationTask({ id: 3, status: "open" }, { departmentId: 11, departmentName: "HR" }),
    ]);

    const { result } = renderHook(() => useRotationOperationsView(), { wrapper: makeWrapper() });
    await waitFor(() => expect(result.current.departmentSummaries.length).toBe(2));

    const it = result.current.departmentSummaries.find((s) => s.departmentName === "IT")!;
    const hr = result.current.departmentSummaries.find((s) => s.departmentName === "HR")!;
    expect(it.count).toBe(2);
    expect(it.openCount).toBe(2);
    expect(hr.count).toBe(1);
    expect(hr.openCount).toBe(1);
  });

  it("personSummaries picks earliest nextAnchorDate per plan", async () => {
    mockedGetMyTasks.mockResolvedValue([
      createRotationTask(
        { id: 1, status: "open" },
        { rotationPlanId: 5, anchorDate: isoDateOffset(10), displayName: "Anika" },
      ),
      createRotationTask(
        { id: 2, status: "open" },
        { rotationPlanId: 5, anchorDate: isoDateOffset(2), displayName: "Anika" },
      ),
    ]);

    const { result } = renderHook(() => useRotationOperationsView(), { wrapper: makeWrapper() });
    await waitFor(() => expect(result.current.personSummaries.length).toBe(1));

    const summary = result.current.personSummaries[0]!;
    expect(summary.taskCount).toBe(2);
    expect(summary.openCount).toBe(2);
    expect(summary.nextAnchorDate).toBe(isoDateOffset(2));
  });
});
