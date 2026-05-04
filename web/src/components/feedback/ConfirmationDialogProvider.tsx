import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from "react";
import {
  ConfirmationDialogContext,
  type ConfirmationDialogOptions,
  type ConfirmationRequest,
} from "./confirmationDialogContext";

function ConfirmationDialog({
  options,
  onClose,
}: {
  options: ConfirmationDialogOptions;
  onClose: (confirmed: boolean) => void;
}) {
  const dialogRef = useRef<HTMLElement | null>(null);
  const cancelButtonRef = useRef<HTMLButtonElement | null>(null);
  const confirmButtonRef = useRef<HTMLButtonElement | null>(null);

  useEffect(() => {
    const initialFocusTarget =
      options.tone === "danger"
        ? cancelButtonRef.current
        : confirmButtonRef.current ?? cancelButtonRef.current;
    initialFocusTarget?.focus();

    const getFocusableElements = () => {
      if (!dialogRef.current) {
        return [] as HTMLElement[];
      }

      return Array.from(
        dialogRef.current.querySelectorAll<HTMLElement>(
          'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
        )
      ).filter((element) => !element.hasAttribute("hidden"));
    };

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        onClose(false);
        return;
      }

      if (event.key !== "Tab") {
        return;
      }

      const focusableElements = getFocusableElements();
      if (focusableElements.length === 0) {
        return;
      }

      const firstElement = focusableElements[0];
      const lastElement = focusableElements[focusableElements.length - 1];
      const activeElement = document.activeElement;

      if (event.shiftKey) {
        if (activeElement === firstElement || !dialogRef.current?.contains(activeElement)) {
          event.preventDefault();
          lastElement?.focus();
        }
        return;
      }

      if (activeElement === lastElement) {
        event.preventDefault();
        firstElement?.focus();
      }
    };

    document.addEventListener("keydown", handleKeyDown);
    return () => document.removeEventListener("keydown", handleKeyDown);
  }, [onClose, options.tone]);

  return (
    <div className="confirmation-dialog-backdrop" onClick={() => onClose(false)}>
      <section
        ref={dialogRef}
        className={`confirmation-dialog${options.tone === "danger" ? " confirmation-dialog--danger" : ""}`}
        role={options.tone === "danger" ? "alertdialog" : "dialog"}
        aria-modal="true"
        aria-labelledby="confirmation-dialog-title"
        aria-describedby="confirmation-dialog-description"
        onClick={(event) => event.stopPropagation()}
      >
        <div className="panel-head">
          <h2 id="confirmation-dialog-title">{options.title}</h2>
          <p id="confirmation-dialog-description">{options.description}</p>
        </div>

        <div className="confirmation-dialog-actions">
          <button
            ref={cancelButtonRef}
            type="button"
            className="btn btn-secondary"
            onClick={() => onClose(false)}
          >
            {options.cancelLabel ?? "Abbrechen"}
          </button>
          <button
            ref={confirmButtonRef}
            type="button"
            className={`btn ${options.tone === "danger" ? "btn-danger" : "btn-primary"}`}
            onClick={() => onClose(true)}
          >
            {options.confirmLabel ?? "Bestätigen"}
          </button>
        </div>
      </section>
    </div>
  );
}

export function ConfirmationDialogProvider({ children }: { children: ReactNode }) {
  const [request, setRequest] = useState<ConfirmationRequest | null>(null);

  const confirm = useCallback((options: ConfirmationDialogOptions) => {
    return new Promise<boolean>((resolve) => {
      const previousActiveElement =
        typeof document !== "undefined" && document.activeElement instanceof HTMLElement
          ? document.activeElement
          : null;
      setRequest({ options, resolve, previousActiveElement });
    });
  }, []);

  const handleClose = useCallback((confirmed: boolean) => {
    setRequest((current) => {
      if (!current) {
        return current;
      }

      current.resolve(confirmed);
      const { previousActiveElement } = current;
      window.setTimeout(() => {
        if (previousActiveElement && previousActiveElement.isConnected) {
          previousActiveElement.focus();
        }
      }, 0);
      return null;
    });
  }, []);

  const contextValue = useMemo(() => confirm, [confirm]);

  return (
    <ConfirmationDialogContext.Provider value={contextValue}>
      {children}
      {request ? <ConfirmationDialog options={request.options} onClose={handleClose} /> : null}
    </ConfirmationDialogContext.Provider>
  );
}
