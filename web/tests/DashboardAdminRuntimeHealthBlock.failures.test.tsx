import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi, beforeEach } from "vitest";
import DashboardAdminRuntimeHealthBlock from "../src/components/dashboard/DashboardAdminRuntimeHealthBlock";
import * as adminApi from "../src/services/adminApi";
import type { AdminRuntimeHealth } from "../src/types/auth";

function baseHealth(): AdminRuntimeHealth {
  return {
    generatedAt: "2026-05-12T10:00:00Z",
    overallSeverity: "ok",
    application: {
      severity: "ok",
      processStartedAt: "2026-05-12T08:00:00Z",
      uptimeSeconds: 7200,
      managedHeapBytes: 50_000_000,
      managedHeapHighThresholdBytes: 200_000_000,
      workingSetBytes: 80_000_000,
      threadPool: null,
    },
    dependencies: {
      severity: "ok",
      database: { severity: "ok", reachable: true, lastCheckedAt: "2026-05-12T10:00:00Z", latencyMs: 5, lastError: null },
      auth: { severity: "ok", mode: "dev-sim", reachability: "not_applicable", lastCheckedAt: "2026-05-12T10:00:00Z", latencyMs: null, lastError: null },
      mail: { severity: "ok", mode: "disabled", configurationStatus: "complete", lastProbeAt: null, lastProbeStatus: "never_run" },
    },
    directory: {
      severity: "ok",
      lastSyncAt: "2026-05-12T09:45:00Z",
      lastSyncStatus: "success",
      lastError: null,
      nextScheduledSyncAt: null,
      pendingImportsCount: 0,
    },
    storage: [],
    host: null,
    automationFailures: { severity: "ok", windowHours: 24, totalCount: 0, recentFailures: [] },
    notificationFailures: { severity: "ok", windowHours: 24, totalCount: 0, recentFailures: [] },
  };
}

function renderBlock() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <DashboardAdminRuntimeHealthBlock />
    </QueryClientProvider>
  );
}

describe("DashboardAdminRuntimeHealthBlock — failures", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("renders 'Keine in 24 h' when there are no failures", async () => {
    vi.spyOn(adminApi, "getAdminRuntimeHealth").mockResolvedValue(baseHealth());

    renderBlock();

    expect(await screen.findByText("Automation-Fehler")).toBeTruthy();
    expect(screen.getByText("Mail-Versand-Fehler")).toBeTruthy();
    expect(screen.getAllByText("Keine in 24 h").length).toBeGreaterThanOrEqual(2);
  });

  it("shows total count and renders details list when there are recent failures", async () => {
    const health = baseHealth();
    health.overallSeverity = "warning";
    health.automationFailures = {
      severity: "warning",
      windowHours: 24,
      totalCount: 3,
      recentFailures: [
        { id: 11, occurredAt: "2026-05-12T09:55:00Z", label: "create_ad_user", errorMessage: "Connection timeout" },
        { id: 10, occurredAt: "2026-05-12T09:30:00Z", label: "send_welcome_mail", errorMessage: null },
      ],
    };
    health.notificationFailures = {
      severity: "warning",
      windowHours: 24,
      totalCount: 1,
      recentFailures: [
        { id: 99, occurredAt: "2026-05-12T09:50:00Z", label: "workflow_created", errorMessage: "SMTP 550 rejected" },
      ],
    };
    vi.spyOn(adminApi, "getAdminRuntimeHealth").mockResolvedValue(health);

    renderBlock();

    expect(await screen.findByText("3 in 24 h")).toBeTruthy();
    expect(screen.getByText("1 in 24 h")).toBeTruthy();
    expect(screen.getByText("Letzte Fehler anzeigen")).toBeTruthy();
    expect(screen.getByText("create_ad_user")).toBeTruthy();
    expect(screen.getByText("Connection timeout")).toBeTruthy();
    expect(screen.getByText("workflow_created")).toBeTruthy();
    expect(screen.getByText("SMTP 550 rejected")).toBeTruthy();
  });

  it("renders '1 in 24 h' for exactly one failure", async () => {
    const health = baseHealth();
    health.automationFailures = {
      severity: "warning",
      windowHours: 24,
      totalCount: 1,
      recentFailures: [
        { id: 1, occurredAt: "2026-05-12T09:00:00Z", label: "create_mailbox", errorMessage: "x" },
      ],
    };
    vi.spyOn(adminApi, "getAdminRuntimeHealth").mockResolvedValue(health);

    renderBlock();

    expect(await screen.findByText("1 in 24 h")).toBeTruthy();
  });
});
