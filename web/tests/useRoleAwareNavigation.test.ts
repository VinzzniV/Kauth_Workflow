import { describe, expect, it } from "vitest";
import { deriveRoleCapabilities } from "../src/auth/roleModel";
import { deriveDefaultPersona } from "../src/hooks/useActiveView";
import { deriveNavigationContext } from "../src/navigation/useRoleAwareNavigation";

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

describe("deriveNavigationContext — Mehrrollen darf nicht generic bleiben (Z15-S2 Follow-up)", () => {
  it("Persona admin → Admin-Kontext, nicht 'Ihre Arbeitsbereiche'", () => {
    const ctx = deriveNavigationContext("admin");
    expect(ctx.title).toBe("Offene Admin-Aufgaben");
    expect(ctx.title).not.toBe("Ihre Arbeitsbereiche");
  });

  it("Persona hr → HR-Kontext", () => {
    const ctx = deriveNavigationContext("hr");
    expect(ctx.title).toBe("HR-Übersicht");
  });

  it("Persona manager → Manager-Kontext", () => {
    const ctx = deriveNavigationContext("manager");
    expect(ctx.title).toBe("Vorgänge meiner Mitarbeitenden");
  });

  it("Persona worker → Worker-Kontext", () => {
    const ctx = deriveNavigationContext("worker");
    expect(ctx.title).toBe("Meine Aufgaben");
  });

  it("Persona reader → Leser-Kontext", () => {
    const ctx = deriveNavigationContext("reader");
    expect(ctx.title).toBe("Übersicht");
  });

  it("Mehrrollen Admin+HR: activeView=admin → Admin-Kontext (kein generischer Fallback)", () => {
    // Simuliert: Mehrrollen-Nutzer mit gespeicherter activeView=admin
    const caps = deriveRoleCapabilities(["auth_admin", "auth_hr"]);
    expect(caps.hasMultipleRoles).toBe(true);
    const activeView = deriveDefaultPersona(caps); // = "admin"
    const ctx = deriveNavigationContext(activeView);
    expect(ctx.title).toBe("Offene Admin-Aufgaben");
  });

  it("Mehrrollen HR+Manager: activeView=hr → HR-Kontext (kein generischer Fallback)", () => {
    const caps = deriveRoleCapabilities(["auth_hr", "auth_manager"]);
    expect(caps.hasMultipleRoles).toBe(true);
    const activeView = deriveDefaultPersona(caps); // = "hr"
    const ctx = deriveNavigationContext(activeView);
    expect(ctx.title).toBe("HR-Übersicht");
  });
});
