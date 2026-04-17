import { beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("../src/auth/IdentityProvider", () => ({
  identityProvider: {
    getStoredToken: vi.fn().mockResolvedValue("test-token"),
  },
}));

vi.mock("../src/config/appRuntimeConfig", () => ({
  getApiBase: () => "http://api.test",
}));

import { reportClientLogEvent, reportUserVisibleError } from "../src/services/systemLogReporter";

describe("systemLogReporter", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    window.history.replaceState({}, "", "/admin/config?section=system");
  });

  it("deduplicates identical visible errors for 60 seconds", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(null, { status: 202 }));
    vi.stubGlobal("fetch", fetchMock);

    reportUserVisibleError({
      message: "Dedup test error",
      clientFunction: "SystemLogReporterTest",
    });
    reportUserVisibleError({
      message: "Dedup test error",
      clientFunction: "SystemLogReporterTest",
    });

    await Promise.resolve();
    await Promise.resolve();

    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it("does not report the log endpoint recursively", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(null, { status: 202 }));
    vi.stubGlobal("fetch", fetchMock);

    await reportClientLogEvent({
      message: "Recursive endpoint should be skipped",
      httpPath: "/client/log-events",
      clientFunction: "SystemLogReporterTest",
    });

    expect(fetchMock).not.toHaveBeenCalled();
  });
});
