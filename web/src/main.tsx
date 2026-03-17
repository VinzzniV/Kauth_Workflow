import React from "react";
import ReactDOM from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import { AuthProvider } from "./auth/AuthContext";
import { CurrentUserProvider } from "./auth/CurrentUserContext";
import App from "./App";
import "./index.css";

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <AuthProvider>
      <CurrentUserProvider>
        <BrowserRouter>
          <App />
        </BrowserRouter>
      </CurrentUserProvider>
    </AuthProvider>
  </React.StrictMode>
);
