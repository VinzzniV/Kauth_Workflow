import { describe, expect, it } from "vitest";
import {
  buildNotificationDraft,
  hasNotificationDraftChanges,
} from "../src/hooks/useAdminNotificationEmailConfiguration";
import type { AdminNotificationEmailConfiguration } from "../src/types/auth";

function createNotificationConfiguration(
  overrides: Partial<AdminNotificationEmailConfiguration> = {}
): AdminNotificationEmailConfiguration {
  return {
    enabled: true,
    mode: "enabled",
    senderEmail: "notify@demo.local",
    frontendBaseUrl: "https://demo.local",
    testRecipientEmail: "test@demo.local",
    sandboxRedirectEmail: null,
    notifyOnWorkflowCreated: true,
    notifyOnTaskReady: true,
    notifyOnWorkflowCompleted: false,
    lastTestStatus: "never",
    lastTestAt: null,
    lastError: null,
    updatedAt: "2026-04-01T08:00:00.000Z",
    hasClientSecret: true,
    configurationStatus: "ready",
    configurationMessage: null,
    ...overrides,
  };
}

describe("useAdminNotificationEmailConfiguration helpers", () => {
  it("builds a stable empty draft for missing configuration", () => {
    expect(buildNotificationDraft(null)).toEqual({
      enabled: false,
      senderEmail: "",
      frontendBaseUrl: "",
      testRecipientEmail: "",
      sandboxRedirectEmail: "",
      notifyOnWorkflowCreated: true,
      notifyOnTaskReady: true,
      notifyOnWorkflowCompleted: true,
    });
  });

  it("treats an unchanged draft as clean", () => {
    const configuration = createNotificationConfiguration();

    expect(hasNotificationDraftChanges(buildNotificationDraft(configuration), configuration)).toBe(false);
  });

  it("detects normalized draft changes", () => {
    const configuration = createNotificationConfiguration({
      senderEmail: null,
      testRecipientEmail: null,
      sandboxRedirectEmail: null,
    });

    const unchangedDraft = buildNotificationDraft(configuration);
    const changedDraft = {
      ...unchangedDraft,
      senderEmail: " notify@demo.local ",
      testRecipientEmail: "test@demo.local",
    };

    expect(hasNotificationDraftChanges(unchangedDraft, configuration)).toBe(false);
    expect(hasNotificationDraftChanges(changedDraft, configuration)).toBe(true);
  });
});
