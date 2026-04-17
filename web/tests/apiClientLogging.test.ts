import { beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("../src/auth/IdentityProvider", () => ({
  identityProvider: {
    getStoredToken: vi.fn().mockResolvedValue("api-token"),
    refreshAfterUnauthorized: vi.fn().mockResolvedValue(false),
    setStoredToken: vi.fn(),
  },
}));

vi.mock("../src/config/appRuntimeConfig", () => ({
  getApiBase: () => "http://api.test",
}));

import { requestJson } from "../src/services/api/client";

describe("requestJson logging", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    window.history.replaceState({}, "", "/admin/config?section=system");
  });

  it("reports failed api responses to the client log endpoint", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(
        new Response(JSON.stringify({ message: "Kaputt" }), {
          status: 500,
          headers: {
            "content-type": "application/json",
            "x-trace-id": "trace-500",
          },
        })
      )
      .mockResolvedValueOnce(new Response(null, { status: 202 }));
    vi.stubGlobal("fetch", fetchMock);

    await expect(requestJson("/workflows")).rejects.toThrow("Kaputt");

    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(fetchMock.mock.calls[1]?.[0]).toBe("http://api.test/client/log-events");

    const logRequest = fetchMock.mock.calls[1]?.[1] as RequestInit;
    const body = JSON.parse(String(logRequest.body));
    expect(body.source).toBe("api");
    expect(body.eventKey).toBe("request_failed");
    expect(body.userMessage).toBe("Kaputt");
    expect(body.httpPath).toBe("/workflows");
    expect(body.traceIdentifier).toBe("trace-500");
  });
});
