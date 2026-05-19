// Slice AGA-N2: Re-Auth-Pfad der Approve-Mutation.
// Backend kann 401 reauth_required (auth_time zu alt) oder 422 reauth_unconfigured
// (Claim fehlt) liefern. Die Mutation muss bei 401 einen MSAL-Popup-Re-Auth
// triggern und das Token-Issue genau ein einziges Mal wiederholen; bei 422
// strukturiert eskalieren statt still durchzuwinken.

import type { ReactNode } from "react";
import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { beforeEach, describe, expect, it, vi } from "vitest";

import {
  ApproveReauthError,
  useApproveAutomationPlanMutation,
} from "../src/services/mutations/automationApprovalMutations";
import * as automationApprovalApi from "../src/services/automationApprovalApi";
import * as identityProviderModule from "../src/auth/IdentityProvider";
import type { ApiError } from "../src/services/api/client";

vi.mock("../src/services/automationApprovalApi", async () => {
  const actual = await vi.importActual<typeof import("../src/services/automationApprovalApi")>(
    "../src/services/automationApprovalApi",
  );
  return {
    ...actual,
    issueAutomationReauthToken: vi.fn(),
    approveAutomationPlan: vi.fn(),
  };
});

vi.mock("../src/auth/IdentityProvider", async () => {
  const actual = await vi.importActual<typeof import("../src/auth/IdentityProvider")>(
    "../src/auth/IdentityProvider",
  );
  return {
    ...actual,
    identityProvider: {
      providerKind: "entra",
      triggerInteractiveReauth: vi.fn(),
    },
    isEntraMode: vi.fn(() => true),
  };
});

const issueToken = vi.mocked(automationApprovalApi.issueAutomationReauthToken);
const approve = vi.mocked(automationApprovalApi.approveAutomationPlan);
const triggerInteractiveReauth = vi.mocked(
  identityProviderModule.identityProvider.triggerInteractiveReauth,
);
const isEntraMode = vi.mocked(identityProviderModule.isEntraMode);

function buildApiError(status: number, payload: unknown): ApiError {
  const err = new Error("api error") as ApiError;
  err.status = status;
  err.payload = payload;
  return err;
}

function makeWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  };
}

const INPUT = {
  workflowInstanceUid: "wf-1",
  nodeKey: "node-1",
  planHash: "hash-1",
};

describe("useApproveAutomationPlanMutation re-auth flow", () => {
  beforeEach(() => {
    issueToken.mockReset();
    approve.mockReset();
    triggerInteractiveReauth.mockReset();
    isEntraMode.mockReset();
    isEntraMode.mockReturnValue(true);
  });

  it("approves without popup when first reauth token succeeds", async () => {
    issueToken.mockResolvedValueOnce({ token: "tok-1", expiresAt: "2026-05-19T12:01:00Z" });
    approve.mockResolvedValueOnce({ approvalId: 7, firstJobId: 42 });

    const { result } = renderHook(() => useApproveAutomationPlanMutation("wf-1"), {
      wrapper: makeWrapper(),
    });
    const res = await result.current.mutateAsync(INPUT);

    expect(res.approvalId).toBe(7);
    expect(triggerInteractiveReauth).not.toHaveBeenCalled();
    expect(approve).toHaveBeenCalledWith(expect.objectContaining({ reauthToken: "tok-1" }));
  });

  it("triggers MSAL popup on 401 reauth_required, then retries token issue once", async () => {
    issueToken
      .mockRejectedValueOnce(
        buildApiError(401, {
          error: "reauth_required",
          reason: "auth_time_stale",
          maxAgeSeconds: 120,
          authTimeAgeSeconds: 400,
        }),
      )
      .mockResolvedValueOnce({ token: "tok-fresh", expiresAt: "2026-05-19T12:01:00Z" });
    triggerInteractiveReauth.mockResolvedValueOnce({ kind: "succeeded" });
    approve.mockResolvedValueOnce({ approvalId: 11, firstJobId: 99 });

    const { result } = renderHook(() => useApproveAutomationPlanMutation("wf-1"), {
      wrapper: makeWrapper(),
    });
    const res = await result.current.mutateAsync(INPUT);

    expect(triggerInteractiveReauth).toHaveBeenCalledTimes(1);
    expect(issueToken).toHaveBeenCalledTimes(2);
    expect(approve).toHaveBeenCalledWith(expect.objectContaining({ reauthToken: "tok-fresh" }));
    expect(res.approvalId).toBe(11);
  });

  it("escalates cancelled popup as ApproveReauthError with kind=cancelled", async () => {
    issueToken.mockRejectedValueOnce(
      buildApiError(401, { error: "reauth_required", reason: "auth_time_stale", maxAgeSeconds: 120, authTimeAgeSeconds: 400 }),
    );
    triggerInteractiveReauth.mockResolvedValueOnce({ kind: "cancelled" });

    const { result } = renderHook(() => useApproveAutomationPlanMutation("wf-1"), {
      wrapper: makeWrapper(),
    });

    await expect(result.current.mutateAsync(INPUT)).rejects.toMatchObject({
      detail: { kind: "cancelled" },
    });
    expect(approve).not.toHaveBeenCalled();
  });

  it("escalates 422 reauth_unconfigured without triggering popup", async () => {
    issueToken.mockRejectedValueOnce(
      buildApiError(422, {
        error: "reauth_unconfigured",
        reason: "auth_time_missing",
        hint: "auth_time-Claim fehlt im Token",
      }),
    );

    const { result } = renderHook(() => useApproveAutomationPlanMutation("wf-1"), {
      wrapper: makeWrapper(),
    });

    try {
      await result.current.mutateAsync(INPUT);
      expect.fail("Expected ApproveReauthError");
    } catch (err) {
      expect(err).toBeInstanceOf(ApproveReauthError);
      expect((err as ApproveReauthError).detail.kind).toBe("unconfigured");
    }
    expect(triggerInteractiveReauth).not.toHaveBeenCalled();
    expect(approve).not.toHaveBeenCalled();
  });

  it("escalates as still_stale if backend rejects token even after popup", async () => {
    const staleError = buildApiError(401, {
      error: "reauth_required",
      reason: "auth_time_stale",
      maxAgeSeconds: 120,
      authTimeAgeSeconds: 999,
    });
    issueToken.mockRejectedValueOnce(staleError).mockRejectedValueOnce(staleError);
    triggerInteractiveReauth.mockResolvedValueOnce({ kind: "succeeded" });

    const { result } = renderHook(() => useApproveAutomationPlanMutation("wf-1"), {
      wrapper: makeWrapper(),
    });

    await waitFor(() => result.current); // ensure rendered

    try {
      await result.current.mutateAsync(INPUT);
      expect.fail("Expected ApproveReauthError");
    } catch (err) {
      expect(err).toBeInstanceOf(ApproveReauthError);
      expect((err as ApproveReauthError).detail).toEqual(
        expect.objectContaining({ kind: "still_stale", maxAgeSeconds: 120 }),
      );
    }
  });

  it("does not call popup in dev-sim mode", async () => {
    isEntraMode.mockReturnValue(false);
    issueToken.mockResolvedValueOnce({ token: "tok-dev", expiresAt: "2026-05-19T12:01:00Z" });
    approve.mockResolvedValueOnce({ approvalId: 1, firstJobId: 1 });

    const { result } = renderHook(() => useApproveAutomationPlanMutation("wf-1"), {
      wrapper: makeWrapper(),
    });
    await result.current.mutateAsync(INPUT);

    expect(triggerInteractiveReauth).not.toHaveBeenCalled();
  });
});
