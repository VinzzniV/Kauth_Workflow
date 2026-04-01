import { beforeEach, describe, expect, it } from "vitest";
import { getAuthMode, getEntraRedirectUri } from "../src/config/appRuntimeConfig";

describe("appRuntimeConfig", () => {
  beforeEach(() => {
    window.__APP_CONFIG__ = undefined;
  });

  it("rejects unsupported auth modes from runtime config", () => {
    window.__APP_CONFIG__ = {
      authMode: "demo",
    };

    expect(() => getAuthMode()).toThrow("Unsupported auth mode");
  });

  it("requires an explicit redirect URI for deployed entra mode", () => {
    window.__APP_CONFIG__ = {
      authMode: "entra",
      entraClientId: "client-id",
      entraTenantId: "tenant-id",
      entraAudience: "api://client-id",
    };

    expect(() => getEntraRedirectUri()).toThrow(
      "ENTRA_REDIRECT_URI must be explicitly configured in app-config.js when authMode=entra."
    );
  });

  it("accepts a valid explicit redirect URI for deployed entra mode", () => {
    window.__APP_CONFIG__ = {
      authMode: "entra",
      entraRedirectUri: "https://onboarding.example.local",
    };

    expect(getEntraRedirectUri()).toBe("https://onboarding.example.local/");
  });
});
