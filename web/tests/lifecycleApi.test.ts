import { describe, expect, it, vi, beforeEach } from "vitest";

vi.mock("../src/services/api/client", () => ({
  requestJson: vi.fn(),
  getDemoAuthToken: vi.fn(),
  setDemoAuthToken: vi.fn(),
}));

import { getProcessTypes } from "../src/services/lifecycleApi";
import { requestJson } from "../src/services/api/client";

const mockedRequestJson = vi.mocked(requestJson);

describe("lifecycleApi.getProcessTypes", () => {
  beforeEach(() => {
    mockedRequestJson.mockReset();
  });

  it("loads process types fresh on every call instead of reusing a shared cache", async () => {
    mockedRequestJson
      .mockResolvedValueOnce([{ key: "onboarding", name: "Onboarding", requiresTargetPerson: false }])
      .mockResolvedValueOnce([{ key: "department_change", name: "Abteilungswechsel", requiresTargetPerson: true }]);

    const first = await getProcessTypes();
    const second = await getProcessTypes();

    expect(first).toEqual([{ key: "onboarding", name: "Onboarding", requiresTargetPerson: false }]);
    expect(second).toEqual([{ key: "department_change", name: "Abteilungswechsel", requiresTargetPerson: true }]);
    expect(mockedRequestJson).toHaveBeenCalledTimes(2);
    expect(mockedRequestJson).toHaveBeenNthCalledWith(1, "/process-types");
    expect(mockedRequestJson).toHaveBeenNthCalledWith(2, "/process-types");
  });
});
