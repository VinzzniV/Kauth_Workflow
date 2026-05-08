import { describe, expect, it } from "vitest";
import { deriveRoleCapabilities } from "../src/auth/roleModel";
import { deriveDefaultPersona } from "../src/hooks/useActiveView";

// useRoleAwareNavigation bezieht dashboardPersona jetzt aus activeView (useActiveView).
// Die Fallback-Kaskade aus Z15-S1 bestimmt, welche Persona ein Mehrrollen-Nutzer sieht.
// Dieser Test prueft die Kopplung ueber deriveDefaultPersona (die Quelle, die useActiveView
// ohne localStorage-Eintrag liefert).

describe("useRoleAwareNavigation — Persona-Override (Z15-S2)", () => {
  it("Mehrrollen-Admin+HR: Default-Persona ist admin (nicht generic)", () => {
    const caps = deriveRoleCapabilities(["auth_admin", "auth_hr"]);
    expect(caps.hasMultipleRoles).toBe(true);
    expect(deriveDefaultPersona(caps)).toBe("admin");
  });

  it("Mehrrollen-HR+Manager: Default-Persona ist hr", () => {
    const caps = deriveRoleCapabilities(["auth_hr", "auth_manager"]);
    expect(caps.hasMultipleRoles).toBe(true);
    expect(deriveDefaultPersona(caps)).toBe("hr");
  });

  it("Mehrrollen-Manager+Worker: Default-Persona ist manager", () => {
    const caps = deriveRoleCapabilities(["auth_manager", "auth_worker"]);
    expect(caps.hasMultipleRoles).toBe(true);
    expect(deriveDefaultPersona(caps)).toBe("manager");
  });

  it("Mehrrollen-Worker+Reader: Default-Persona ist worker", () => {
    const caps = deriveRoleCapabilities(["auth_worker", "auth_reader"]);
    expect(caps.hasMultipleRoles).toBe(true);
    expect(deriveDefaultPersona(caps)).toBe("worker");
  });

  it("Einzelrolle Admin: Default-Persona ist admin", () => {
    const caps = deriveRoleCapabilities(["auth_admin"]);
    expect(caps.hasMultipleRoles).toBe(false);
    expect(deriveDefaultPersona(caps)).toBe("admin");
  });

  it("Keine bekannte Rolle: Default-Persona ist generic", () => {
    const caps = deriveRoleCapabilities([]);
    expect(deriveDefaultPersona(caps)).toBe("generic");
  });
});
