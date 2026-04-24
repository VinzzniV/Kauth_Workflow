import React from "react";
import ReactDOM from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import { QueryClientProvider } from "@tanstack/react-query";
import { AuthProvider } from "./auth/AuthContext";
import { CurrentUserProvider } from "./auth/CurrentUserContext";
import { ConfirmationDialogProvider } from "./components/feedback/ConfirmationDialogProvider";
import { ToastProvider } from "./components/feedback/ToastProvider";
import App from "./App";
import { queryClient } from "./services/queryClient";
import { ThemeProvider } from "./theme/ThemeProvider";
import { applyThemeToDocument, resolveInitialTheme } from "./theme/theme";
import "./index.css";

function renderBootstrapError(error: unknown) {
  const root = document.getElementById("root");
  if (!root) {
    return;
  }

  const message = error instanceof Error ? `${error.name}: ${error.message}` : String(error);
  root.innerHTML = `
    <main style="min-height:100vh;display:grid;place-items:center;padding:24px;background:#f6f3ee;color:#1f2937;font-family:ui-sans-serif,system-ui,sans-serif;">
      <section style="max-width:880px;width:100%;background:#ffffff;border:1px solid #d6d3d1;border-radius:16px;padding:24px;box-shadow:0 10px 30px rgba(15,23,42,.08);">
        <h1 style="margin:0 0 12px;font-size:24px;font-weight:700;">Frontend-Start fehlgeschlagen</h1>
        <p style="margin:0 0 12px;line-height:1.5;">Die Weboberfläche konnte nicht initialisiert werden. Die konkrete Browser-Ausnahme steht unten.</p>
        <pre style="margin:0;white-space:pre-wrap;word-break:break-word;background:#111827;color:#f9fafb;padding:16px;border-radius:12px;overflow:auto;">${message}</pre>
      </section>
    </main>
  `;
}

window.addEventListener("error", (event) => {
  renderBootstrapError(event.error ?? event.message);
});

window.addEventListener("unhandledrejection", (event) => {
  renderBootstrapError(event.reason);
});

try {
  applyThemeToDocument(resolveInitialTheme());

  ReactDOM.createRoot(document.getElementById("root")!).render(
    <React.StrictMode>
      <QueryClientProvider client={queryClient}>
        <AuthProvider>
          <CurrentUserProvider>
            <ThemeProvider>
              <ToastProvider>
                <ConfirmationDialogProvider>
                  <BrowserRouter>
                    <App />
                  </BrowserRouter>
                </ConfirmationDialogProvider>
              </ToastProvider>
            </ThemeProvider>
          </CurrentUserProvider>
        </AuthProvider>
      </QueryClientProvider>
    </React.StrictMode>
  );
} catch (error) {
  renderBootstrapError(error);
}
