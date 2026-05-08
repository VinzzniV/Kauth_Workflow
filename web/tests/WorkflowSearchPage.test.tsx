import { render } from "@testing-library/react";
import { MemoryRouter, Navigate, Route, Routes, useLocation, useSearchParams } from "react-router-dom";
import { describe, expect, it } from "vitest";

// WorkflowSearchPage is gone — /search now redirects to /workflows via SearchParamsRedirect in App.tsx.
// These tests verify the redirect contract: params q, dept, type, status are forwarded.

function SearchParamsRedirect({ to }: { to: string }) {
  const [searchParams] = useSearchParams();
  const qs = searchParams.toString();
  return <Navigate to={qs ? `${to}?${qs}` : to} replace />;
}

function LocationSpy({ onLocation }: { onLocation: (path: string) => void }) {
  const location = useLocation();
  onLocation(location.pathname + location.search);
  return null;
}

function renderRedirect(initialPath: string) {
  let captured = "";
  render(
    <MemoryRouter initialEntries={[initialPath]}>
      <Routes>
        <Route path="/search" element={<SearchParamsRedirect to="/workflows" />} />
        <Route path="/workflows" element={<LocationSpy onLocation={(l) => { captured = l; }} />} />
      </Routes>
    </MemoryRouter>
  );
  return captured;
}

describe("WorkflowSearch redirect", () => {
  it("redirects /search to /workflows with no params", () => {
    expect(renderRedirect("/search")).toBe("/workflows");
  });

  it("preserves q param", () => {
    expect(renderRedirect("/search?q=alice")).toBe("/workflows?q=alice");
  });

  it("preserves dept param", () => {
    expect(renderRedirect("/search?dept=10")).toBe("/workflows?dept=10");
  });

  it("preserves type param", () => {
    expect(renderRedirect("/search?type=onboarding")).toBe("/workflows?type=onboarding");
  });

  it("preserves status param", () => {
    expect(renderRedirect("/search?status=in_progress")).toBe("/workflows?status=in_progress");
  });

  it("preserves multiple params", () => {
    expect(renderRedirect("/search?q=alice&dept=10&type=onboarding&status=in_progress")).toBe(
      "/workflows?q=alice&dept=10&type=onboarding&status=in_progress"
    );
  });
});
