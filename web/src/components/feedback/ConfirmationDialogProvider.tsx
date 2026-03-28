import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";

type ConfirmationTone = "default" | "danger";

export type ConfirmationDialogOptions = {
  title: string;
  description: string;
  confirmLabel?: string;
  cancelLabel?: string;
  tone?: ConfirmationTone;
};

type ConfirmationRequest = {
  options: ConfirmationDialogOptions;
  resolve: (value: boolean) => void;
};

const ConfirmationDialogContext = createContext<
  ((options: ConfirmationDialogOptions) => Promise<boolean>) | null
>(null);

function ConfirmationDialog({
  options,
  onClose,
}: {
  options: ConfirmationDialogOptions;
  onClose: (confirmed: boolean) => void;
}) {
  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        onClose(false);
      }
    };

    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [onClose]);

  return (
    <div className="confirmation-dialog-backdrop" onClick={() => onClose(false)}>
      <section
        className="confirmation-dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="confirmation-dialog-title"
        onClick={(event) => event.stopPropagation()}
      >
        <div className="panel-head">
          <h2 id="confirmation-dialog-title">{options.title}</h2>
          <p>{options.description}</p>
        </div>

        <div className="confirmation-dialog-actions">
          <button type="button" className="btn btn-secondary" onClick={() => onClose(false)}>
            {options.cancelLabel ?? "Abbrechen"}
          </button>
          <button
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
      setRequest({ options, resolve });
    });
  }, []);

  const handleClose = useCallback((confirmed: boolean) => {
    setRequest((current) => {
      if (!current) {
        return current;
      }

      current.resolve(confirmed);
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

export function useConfirmationDialog() {
  const context = useContext(ConfirmationDialogContext);

  if (!context) {
    throw new Error("useConfirmationDialog must be used within a ConfirmationDialogProvider.");
  }

  return context;
}
