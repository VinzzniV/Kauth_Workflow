import { beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("../src/services/api/client", () => ({
  requestJson: vi.fn(),
}));

import { requestJson } from "../src/services/api/client";
import { getAdminWorkflowActionDefinitions } from "../src/services/adminConfigApi";

const mockedRequestJson = vi.mocked(requestJson);

describe("adminConfigApi.getAdminWorkflowActionDefinitions", () => {
  beforeEach(() => {
    mockedRequestJson.mockReset();
  });

  it("maps backend action definitions into the frontend action catalog shape", async () => {
    mockedRequestJson.mockResolvedValueOnce([
      {
        id: 10,
        key: "CreateAdUser",
        name: "Create AD User",
        description: "Legt ein AD-Konto an.",
        handlerType: "create-ad-user",
        parameterSchema: { type: "object" },
        isActive: true,
        requiresApproval: false,
        isIdempotent: true,
        createdAt: "2026-04-08T10:00:00Z",
        updatedAt: "2026-04-08T10:00:00Z",
      },
    ]);

    const result = await getAdminWorkflowActionDefinitions();

    expect(mockedRequestJson).toHaveBeenCalledWith("/admin/config/action-definitions");
    expect(result).toEqual([
      {
        id: 10,
        actionKey: "CreateAdUser",
        displayName: "Create AD User",
        description: "Legt ein AD-Konto an.",
        handlerKey: "create-ad-user",
        isIdempotent: true,
        isActive: true,
        requiresApproval: false,
        inputSchema: { type: "object" },
      },
    ]);
  });
});
