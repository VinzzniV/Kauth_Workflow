import {
  useCallback,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import {
  ToastContext,
  type ToastContextValue,
  type ToastItem,
  type ToastType,
} from "./toastContext";

const TOAST_LIMIT = 3;
const TOAST_EXIT_MS = 220;

function getToastDuration(type: ToastType): number {
  return type === "error" ? 6000 : 4000;
}

function ToastViewport({
  toasts,
  onDismiss,
}: {
  toasts: ToastItem[];
  onDismiss: (id: string) => void;
}) {
  return (
    <div className="toast-container" role="status" aria-live="polite" aria-atomic="false">
      {toasts.map((toast) => (
        <section
          key={toast.id}
          className={`toast toast-${toast.type}${toast.isLeaving ? " is-leaving" : ""}`}
        >
          <p className="toast-message">{toast.message}</p>
          <button
            type="button"
            className="toast-close"
            aria-label="Meldung schließen"
            onClick={() => onDismiss(toast.id)}
          >
            ×
          </button>
        </section>
      ))}
    </div>
  );
}

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<ToastItem[]>([]);

  const dismissToast = useCallback((id: string) => {
    let shouldScheduleRemoval = false;

    setToasts((current) =>
      current.map((toast) => {
        if (toast.id !== id) {
          return toast;
        }

        if (!toast.isLeaving) {
          shouldScheduleRemoval = true;
        }

        return toast.isLeaving ? toast : { ...toast, isLeaving: true };
      })
    );

    if (shouldScheduleRemoval) {
      window.setTimeout(() => {
        setToasts((current) => current.filter((toast) => toast.id !== id));
      }, TOAST_EXIT_MS);
    }
  }, []);

  const enqueueToast = useCallback(
    (type: ToastType, message: string) => {
      const toast: ToastItem = {
        id: `${Date.now()}-${Math.random().toString(36).slice(2, 9)}`,
        message,
        type,
        isLeaving: false,
      };

      setToasts((current) => {
        const activeToasts = current.filter((item) => !item.isLeaving);
        const nextActiveToasts = [...activeToasts, toast];
        return nextActiveToasts.slice(Math.max(0, nextActiveToasts.length - TOAST_LIMIT));
      });
    },
    []
  );

  useEffect(() => {
    const timeoutIds = toasts
      .filter((toast) => !toast.isLeaving)
      .map((toast) =>
        window.setTimeout(() => {
          dismissToast(toast.id);
        }, getToastDuration(toast.type))
      );

    return () => {
      timeoutIds.forEach((timeoutId) => window.clearTimeout(timeoutId));
    };
  }, [dismissToast, toasts]);

  const contextValue = useMemo<ToastContextValue>(
    () => ({
      showSuccess: (message: string) => enqueueToast("success", message),
      showError: (message: string) => enqueueToast("error", message),
      showInfo: (message: string) => enqueueToast("info", message),
      dismissToast,
    }),
    [dismissToast, enqueueToast]
  );

  return (
    <ToastContext.Provider value={contextValue}>
      {children}
      <ToastViewport toasts={toasts} onDismiss={dismissToast} />
    </ToastContext.Provider>
  );
}
