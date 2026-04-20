import { describe, expect, it } from "vitest";
import { normalizeAdminWorkspaceSection } from "../src/components/admin-config/adminWorkspaceModel";

describe("adminWorkspaceModel", () => {
  it("normalizes the removed operations section to system logs", () => {
    expect(normalizeAdminWorkspaceSection("operations")).toBe("system_logs");
  });

  it("normalizes the legacy system section to system logs", () => {
    expect(normalizeAdminWorkspaceSection("system")).toBe("system_logs");
  });
});
