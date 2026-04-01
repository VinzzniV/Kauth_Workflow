import { cleanup } from "@testing-library/react";
import { afterEach } from "vitest";

window.__APP_CONFIG__ = {
  authMode: "dev-sim",
  apiBase: "/api",
};

afterEach(() => {
  cleanup();
});
