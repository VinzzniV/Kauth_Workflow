import type { ReactNode } from "react";
import { act, renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  buildStationForm,
  createEmptyStationForm,
  normalizeStationPayload,
  useRotationStationForm,
} from "../src/hooks/useRotationStationForm";
import { ToastProvider } from "../src/components/feedback/ToastProvider";
import * as rotationApi from "../src/services/rotationApi";
import type { RotationStation } from "../src/types/rotation";

vi.mock("../src/services/rotationApi", async () => {
  const actual = await vi.importActual<typeof import("../src/services/rotationApi")>("../src/services/rotationApi");
  return {
    ...actual,
    createRotationStation: vi.fn(),
    updateRotationStation: vi.fn(),
    deleteRotationStation: vi.fn(),
    regenerateRotationGeneratedTasks: vi.fn(),
  };
});

const mockedCreate = vi.mocked(rotationApi.createRotationStation);
const mockedUpdate = vi.mocked(rotationApi.updateRotationStation);

function makeWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return function Wrapper({ children }: { children: ReactNode }) {
    return (
      <QueryClientProvider client={queryClient}>
        <ToastProvider>{children}</ToastProvider>
      </QueryClientProvider>
    );
  };
}

function makeStation(overrides: Partial<RotationStation> = {}): RotationStation {
  return {
    id: 100,
    rotationPlanId: 7,
    departmentId: 9,
    departmentName: "IT",
    orderIndex: 2,
    startDate: "2026-06-01",
    endDate: "2026-06-30",
    location: "Hauptbüro",
    notes: "Hardware-Setup",
    status: "planned",
    createdAt: "2026-05-01T08:00:00.000Z",
    updatedAt: "2026-05-01T08:00:00.000Z",
    ...overrides,
  };
}

// ── Pure helpers ───────────────────────────────────────────────────────────────

describe("createEmptyStationForm", () => {
  it("uses zero as default orderIndex", () => {
    const form = createEmptyStationForm();
    expect(form.orderIndex).toBe("0");
    expect(form.status).toBe("planned");
    expect(form.departmentId).toBe("");
  });

  it("respects explicit nextOrderIndex", () => {
    expect(createEmptyStationForm(5).orderIndex).toBe("5");
  });
});

describe("buildStationForm", () => {
  it("maps a station record to string-typed form state", () => {
    const form = buildStationForm(makeStation());
    expect(form.departmentId).toBe("9");
    expect(form.orderIndex).toBe("2");
    expect(form.startDate).toBe("2026-06-01");
    expect(form.endDate).toBe("2026-06-30");
    expect(form.location).toBe("Hauptbüro");
    expect(form.notes).toBe("Hardware-Setup");
    expect(form.status).toBe("planned");
  });

  it("normalizes null location/notes to empty strings", () => {
    const form = buildStationForm(makeStation({ location: null, notes: null }));
    expect(form.location).toBe("");
    expect(form.notes).toBe("");
  });
});

describe("normalizeStationPayload", () => {
  it("parses int fields and trims optional strings", () => {
    const payload = normalizeStationPayload({
      departmentId: "9",
      startDate: "2026-06-01",
      endDate: "2026-06-30",
      orderIndex: "3",
      location: "  Hauptbüro  ",
      notes: "  Setup  ",
      status: "active",
    });
    expect(payload.departmentId).toBe(9);
    expect(payload.orderIndex).toBe(3);
    // helper applies .trim() on the optional strings before sending to backend.
    expect(payload.location).toBe("Hauptbüro");
    expect(payload.notes).toBe("Setup");
    expect(payload.status).toBe("active");
  });

  it("returns undefined for blank optional fields after trim", () => {
    const payload = normalizeStationPayload({
      departmentId: "9",
      startDate: "2026-06-01",
      endDate: "2026-06-30",
      orderIndex: "0",
      location: "   ",
      notes: "",
      status: "planned",
    });
    expect(payload.location).toBeUndefined();
    expect(payload.notes).toBeUndefined();
  });

  it("coerces empty departmentId to 0 (caller relies on `<= 0` rejection)", () => {
    // Number("") === 0 in JS — not NaN. The handleSaveStation guard catches
    // this with `payload.departmentId <= 0`, so 0 acts as the sentinel value.
    const payload = normalizeStationPayload({
      departmentId: "",
      startDate: "2026-06-01",
      endDate: "2026-06-30",
      orderIndex: "0",
      location: "",
      notes: "",
      status: "planned",
    });
    expect(payload.departmentId).toBe(0);

    const garbage = normalizeStationPayload({
      departmentId: "abc",
      startDate: "2026-06-01",
      endDate: "2026-06-30",
      orderIndex: "0",
      location: "",
      notes: "",
      status: "planned",
    });
    expect(Number.isNaN(garbage.departmentId)).toBe(true);
  });
});

// ── Hook integration ──────────────────────────────────────────────────────────

describe("useRotationStationForm", () => {
  beforeEach(() => {
    mockedCreate.mockReset();
    mockedUpdate.mockReset();
  });

  it("openCreateStationForm seeds an empty form using orderedStationsCount", () => {
    const { result } = renderHook(
      () => useRotationStationForm({ numericPlanId: 7, personId: 11, orderedStationsCount: 4 }),
      { wrapper: makeWrapper() },
    );

    act(() => result.current.openCreateStationForm());
    expect(result.current.editingStationId).toBeNull();
    expect(result.current.stationForm.orderIndex).toBe("4");
    expect(result.current.stationForm.departmentId).toBe("");
  });

  it("openEditStationForm populates form from station and tracks id", () => {
    const { result } = renderHook(
      () => useRotationStationForm({ numericPlanId: 7, personId: 11, orderedStationsCount: 4 }),
      { wrapper: makeWrapper() },
    );

    act(() => result.current.openEditStationForm(makeStation()));
    expect(result.current.editingStationId).toBe(100);
    expect(result.current.stationForm.departmentId).toBe("9");
    expect(result.current.stationForm.location).toBe("Hauptbüro");
  });

  it("resetStationForm clears editing id and re-seeds empty form", () => {
    const { result } = renderHook(
      () => useRotationStationForm({ numericPlanId: 7, personId: 11, orderedStationsCount: 4 }),
      { wrapper: makeWrapper() },
    );

    act(() => result.current.openEditStationForm(makeStation()));
    act(() => result.current.resetStationForm());

    expect(result.current.editingStationId).toBeNull();
    expect(result.current.stationForm.departmentId).toBe("");
  });

  it("handleSaveStation rejects empty form without calling the API", async () => {
    const { result } = renderHook(
      () => useRotationStationForm({ numericPlanId: 7, personId: 11, orderedStationsCount: 0 }),
      { wrapper: makeWrapper() },
    );

    await act(async () => {
      await result.current.handleSaveStation();
    });

    expect(mockedCreate).not.toHaveBeenCalled();
    expect(mockedUpdate).not.toHaveBeenCalled();
    expect(result.current.isSavingStation).toBe(false);
  });

  it("handleSaveStation calls createRotationStation when no editing id", async () => {
    mockedCreate.mockResolvedValue(makeStation({ id: 200 }));

    const { result } = renderHook(
      () => useRotationStationForm({ numericPlanId: 7, personId: 11, orderedStationsCount: 0 }),
      { wrapper: makeWrapper() },
    );

    act(() => {
      result.current.setStationForm({
        departmentId: "9",
        startDate: "2026-06-01",
        endDate: "2026-06-30",
        orderIndex: "0",
        location: "",
        notes: "",
        status: "planned",
      });
    });

    await act(async () => {
      await result.current.handleSaveStation();
    });

    expect(mockedCreate).toHaveBeenCalledTimes(1);
    expect(mockedCreate).toHaveBeenCalledWith(7, expect.objectContaining({
      departmentId: 9,
      orderIndex: 0,
      status: "planned",
    }));
    expect(mockedUpdate).not.toHaveBeenCalled();
    await waitFor(() => expect(result.current.isSavingStation).toBe(false));
    // Form must reset after successful save
    expect(result.current.editingStationId).toBeNull();
    expect(result.current.stationForm.departmentId).toBe("");
  });

  it("handleSaveStation calls updateRotationStation when editing", async () => {
    mockedUpdate.mockResolvedValue(makeStation({ id: 100, status: "active" }));

    const { result } = renderHook(
      () => useRotationStationForm({ numericPlanId: 7, personId: 11, orderedStationsCount: 1 }),
      { wrapper: makeWrapper() },
    );

    act(() => result.current.openEditStationForm(makeStation()));
    act(() => {
      result.current.setStationForm((current) => ({ ...current, status: "active" }));
    });

    await act(async () => {
      await result.current.handleSaveStation();
    });

    expect(mockedUpdate).toHaveBeenCalledTimes(1);
    expect(mockedUpdate).toHaveBeenCalledWith(100, expect.objectContaining({ status: "active" }));
    expect(mockedCreate).not.toHaveBeenCalled();
  });
});
