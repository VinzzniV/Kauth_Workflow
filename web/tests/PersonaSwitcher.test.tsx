import { fireEvent, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import PersonaSwitcher, { buildPersonaOptions } from "../src/components/dashboard/PersonaSwitcher";
import { deriveRoleCapabilities } from "../src/auth/roleModel";
import { renderWithApp } from "./testUtils";

// --- buildPersonaOptions unit tests ---

describe("buildPersonaOptions", () => {
  it("returns only roles the user has", () => {
    const caps = deriveRoleCapabilities(["auth_admin", "auth_hr"]);
    const options = buildPersonaOptions(caps);
    expect(options.map((o) => o.persona)).toEqual(["admin", "hr"]);
  });

  it("returns all five roles when all are present", () => {
    const caps = deriveRoleCapabilities(["auth_admin", "auth_hr", "auth_manager", "auth_worker", "auth_reader"]);
    const options = buildPersonaOptions(caps);
    expect(options.map((o) => o.persona)).toEqual(["admin", "hr", "manager", "worker", "reader"]);
  });

  it("returns empty list for generic user", () => {
    const caps = deriveRoleCapabilities([]);
    expect(buildPersonaOptions(caps)).toEqual([]);
  });

  it("maintains priority order regardless of input order", () => {
    const caps = deriveRoleCapabilities(["auth_worker", "auth_manager"]);
    const options = buildPersonaOptions(caps);
    expect(options.map((o) => o.persona)).toEqual(["manager", "worker"]);
  });
});

// --- PersonaSwitcher component tests ---

describe("PersonaSwitcher", () => {
  it("renders nothing for a single-role user", () => {
    renderWithApp(<PersonaSwitcher />, { roleKeys: ["auth_hr"] });
    expect(screen.queryByRole("group", { name: "Ansicht wechseln" })).toBeNull();
  });

  it("renders nothing for a user with no roles", () => {
    renderWithApp(<PersonaSwitcher />, { roleKeys: [] });
    expect(screen.queryByRole("group", { name: "Ansicht wechseln" })).toBeNull();
  });

  it("renders the switcher group for a user with multiple roles", () => {
    renderWithApp(<PersonaSwitcher />, { roleKeys: ["auth_admin", "auth_hr"] });
    expect(screen.getByRole("group", { name: "Ansicht wechseln" })).toBeTruthy();
  });

  it("shows exactly the roles the user has", () => {
    renderWithApp(<PersonaSwitcher />, { roleKeys: ["auth_manager", "auth_worker"] });
    expect(screen.getByRole("button", { name: "Abteilungsleitung" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Fachbereich" })).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Admin" })).toBeNull();
    expect(screen.queryByRole("button", { name: "HR" })).toBeNull();
  });

  it("marks the active view with aria-pressed=true", () => {
    renderWithApp(<PersonaSwitcher />, {
      roleKeys: ["auth_admin", "auth_hr"],
      activeView: "hr",
    });
    expect(screen.getByRole("button", { name: "HR" }).getAttribute("aria-pressed")).toBe("true");
    expect(screen.getByRole("button", { name: "Admin" }).getAttribute("aria-pressed")).toBe("false");
  });

  it("marks the default (highest-priority) view as active when no override is given", () => {
    renderWithApp(<PersonaSwitcher />, { roleKeys: ["auth_admin", "auth_hr"] });
    expect(screen.getByRole("button", { name: "Admin" }).getAttribute("aria-pressed")).toBe("true");
    expect(screen.getByRole("button", { name: "HR" }).getAttribute("aria-pressed")).toBe("false");
  });

  it("calls setActiveView with the correct persona when a button is clicked", () => {
    const setActiveView = vi.fn();
    renderWithApp(<PersonaSwitcher />, {
      roleKeys: ["auth_admin", "auth_hr"],
      setActiveView,
    });

    fireEvent.click(screen.getByRole("button", { name: "HR" }));
    expect(setActiveView).toHaveBeenCalledOnce();
    expect(setActiveView).toHaveBeenCalledWith("hr");
  });

  it("calls setActiveView when clicking the already-active button", () => {
    const setActiveView = vi.fn();
    renderWithApp(<PersonaSwitcher />, {
      roleKeys: ["auth_admin", "auth_hr"],
      activeView: "admin",
      setActiveView,
    });

    fireEvent.click(screen.getByRole("button", { name: "Admin" }));
    expect(setActiveView).toHaveBeenCalledWith("admin");
  });
});
