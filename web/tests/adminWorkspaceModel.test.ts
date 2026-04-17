import { describe, expect, it } from "vitest";
import { normalizeAdminWorkspaceSection } from "../src/components/admin-config/adminWorkspaceModel";

describe("adminWorkspaceModel", () => {
  it("normalizes the removed operations section to system", () => {
    expect(normalizeAdminWorkspaceSection("operations")).toBe("system");
  });
});
