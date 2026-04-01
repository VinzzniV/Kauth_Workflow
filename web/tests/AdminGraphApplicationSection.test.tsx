import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { AdminGraphApplicationSection } from "../src/components/admin-config/AdminGraphApplicationSection";

describe("AdminGraphApplicationSection", () => {
  it("renders runtime status without edit controls", () => {
    render(
      <AdminGraphApplicationSection
        graphApplicationConfiguration={{
          tenantId: "tenant-123",
          clientId: "client-456",
          hasClientSecret: true,
          updatedAt: null,
          configurationSource: "runtime",
          configurationStatus: "ready",
          configurationMessage: null,
        }}
      />
    );

    expect(screen.getByText("Konfigurationsquelle")).toBeTruthy();
    expect(screen.getByText("Runtime")).toBeTruthy();
    expect(screen.getByText("tenant-123")).toBeTruthy();
    expect(screen.getByText("client-456")).toBeTruthy();
    expect(screen.getByText(/ENTRA_CLIENT_SECRET/)).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Graph-Konfiguration speichern" })).toBeNull();
    expect(screen.queryByLabelText("Client Secret")).toBeNull();
  });
});
