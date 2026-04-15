import { describe, expect, it } from "vitest";
import { canAccessFeature, deriveRoleCapabilities, getDefaultRoute } from "../src/auth/roleModel";

describe("roleModel", () => {
  it("grants supervisor access for department-lead capability override", () => {
    const capabilities = deriveRoleCapabilities(["auth_reader"], [], { canAccessSupervisorStep: true });

    expect(capabilities.canAccessSupervisorStep).toBe(true);
    expect(canAccessFeature(capabilities, "supervisorStep")).toBe(true);
  });

  it("routes department-lead users without manager role to supervisor page", () => {
    const capabilities = deriveRoleCapabilities([], [], { canAccessSupervisorStep: true });

    expect(getDefaultRoute(capabilities)).toBe("/supervisor");
  });
});
