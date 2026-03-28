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
