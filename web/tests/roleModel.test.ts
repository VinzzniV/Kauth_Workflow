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

describe("getDefaultRoute — activePersona-Parameter (Z15-S2)", () => {
  it("routes admin to /admin/config when activePersona is admin", () => {
    const caps = deriveRoleCapabilities(["auth_admin", "auth_hr"]);
    expect(getDefaultRoute(caps, "admin")).toBe("/admin/config");
  });

  it("routes hr multi-role user to / when activePersona is hr (no persona-specific route)", () => {
    const caps = deriveRoleCapabilities(["auth_admin", "auth_hr"]);
    expect(getDefaultRoute(caps, "hr")).toBe("/");
  });

  it("routes multi-role manager to /supervisor when activePersona is manager", () => {
    const caps = deriveRoleCapabilities(["auth_admin", "auth_manager"]);
    expect(getDefaultRoute(caps, "manager")).toBe("/supervisor");
  });

  it("routes multi-role worker to /tasks/my when activePersona is worker", () => {
    const caps = deriveRoleCapabilities(["auth_admin", "auth_worker"]);
    expect(getDefaultRoute(caps, "worker")).toBe("/tasks/my");
  });

  it("falls back to capabilities.dashboardPersona when activePersona is not given (single-role admin)", () => {
    const caps = deriveRoleCapabilities(["auth_admin"]);
    expect(getDefaultRoute(caps)).toBe("/admin/config");
  });

  it("falls back to / when activePersona has no matching route and dashboard is accessible", () => {
    const caps = deriveRoleCapabilities(["auth_admin", "auth_hr"]);
    expect(getDefaultRoute(caps, "generic")).toBe("/");
  });
});
