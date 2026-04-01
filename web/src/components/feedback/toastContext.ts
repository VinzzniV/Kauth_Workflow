import { createContext } from "react";

export type ToastType = "success" | "error" | "info";

export type ToastItem = {
  id: string;
  message: string;
  type: ToastType;
  isLeaving: boolean;
};

export type ToastContextValue = {
  showSuccess: (message: string) => void;
  showError: (message: string) => void;
  showInfo: (message: string) => void;
  dismissToast: (id: string) => void;
};

export const ToastContext = createContext<ToastContextValue | null>(null);
