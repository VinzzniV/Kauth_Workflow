import { describe, expect, it } from "vitest";
import { buildAdminOverviewWarnings, normalizeAdminWorkspaceSection } from "../src/components/admin-config/adminWorkspaceModel";

describe("adminWorkspaceModel", () => {
  it("normalizes the removed operations section to system logs", () => {
    expect(normalizeAdminWorkspaceSection("operations")).toBe("system_logs");
  });

  it("normalizes the legacy system section to system logs", () => {
    expect(normalizeAdminWorkspaceSection("system")).toBe("system_logs");
  });

  it("does not flag responsibilities as missing when display labels are present", () => {
    const warnings = buildAdminOverviewWarnings({
      departments: [],
      responsibilities: [
        {
          responsibilityId: 1,
          responsibilityKey: "ad",
          systemKey: "ad",
          responsibilityName: "AD",
          responsibilityType: "application",
          departmentId: null,
          departmentName: "IT",
          appUserId: null,
          appUserDisplayName: "Niederwieser, Vinzent",
          updatedAt: null,
        },
      ],
      eligibleSupervisorUsers: [],
      eligibleRequirementOwnerUsers: [],
      notificationEmailConfiguration: null,
    });

    expect(warnings.find((warning) => warning.category === "responsibility_user")).toBeUndefined();
    expect(warnings.find((warning) => warning.category === "responsibility_department")).toBeUndefined();
  });
});
