import { beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("../src/services/api/client", () => ({
  requestJson: vi.fn(),
  getDemoAuthToken: vi.fn(),
  setDemoAuthToken: vi.fn(),
}));

import { requestJson } from "../src/services/api/client";
import { getStartableWorkflowDefinitions } from "../src/services/lookupApi";

const mockedRequestJson = vi.mocked(requestJson);

describe("lookupApi.getStartableWorkflowDefinitions", () => {
  beforeEach(() => {
    mockedRequestJson.mockReset();
  });

  it("loads startable workflow definitions from the definition-first endpoint", async () => {
    mockedRequestJson
      .mockResolvedValueOnce([
        {
          definitionKey: "onboarding",
          name: "Onboarding",
          description: "Neue Person anlegen",
          requiresTargetPerson: false,
          primaryLegacyProcessTypeKey: "onboarding",
          latestPublishedVersionNumber: 3,
        },
      ])
      .mockResolvedValueOnce([
        {
          definitionKey: "department_change",
          name: "Abteilungswechsel",
          description: "Bestehende Person versetzen",
          requiresTargetPerson: true,
          primaryLegacyProcessTypeKey: "department_change",
          latestPublishedVersionNumber: 2,
        },
      ]);

    const first = await getStartableWorkflowDefinitions();
    const second = await getStartableWorkflowDefinitions();

    expect(first[0]?.definitionKey).toBe("onboarding");
    expect(second[0]?.definitionKey).toBe("department_change");
    expect(mockedRequestJson).toHaveBeenCalledTimes(2);
    expect(mockedRequestJson).toHaveBeenNthCalledWith(1, "/workflow-definitions/startable");
    expect(mockedRequestJson).toHaveBeenNthCalledWith(2, "/workflow-definitions/startable");
  });
});
