import { Component, type ErrorInfo, type ReactNode } from "react";
import { reportUserVisibleError } from "../../services/systemLogReporter";

type AppErrorBoundaryProps = {
  children: ReactNode;
  /** Wird zur Identifikation in System-Logs verwendet (z.B. Routen-Pfad). */
  scope?: string;
  /** Wenn gesetzt, wird beim Wechsel des Wertes die Boundary resettet (z.B. `key={location.pathname}`). */
  resetKey?: string | number;
  /** Inline-Modus: zeigt nur ein Panel statt einer ganzen Seite — fuer Sub-Section-Boundaries. */
  inline?: boolean;
};

type AppErrorBoundaryState = {
  error: Error | null;
};

export class AppErrorBoundary extends Component<AppErrorBoundaryProps, AppErrorBoundaryState> {
  constructor(props: AppErrorBoundaryProps) {
    super(props);
    this.state = { error: null };
  }

  static getDerivedStateFromError(error: Error): AppErrorBoundaryState {
    return { error };
  }

  override componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    const componentStack = errorInfo.componentStack ?? "";
    reportUserVisibleError({
      message: `Render error: ${error.message}`,
      severity: "error",
      category: "ui",
      eventKey: "ui_render_error",
      source: "AppErrorBoundary",
      clientFunction: this.props.scope ?? "unknown",
      details: { stack: error.stack ?? null, componentStack },
    });
  }

  override componentDidUpdate(prev: AppErrorBoundaryProps) {
    if (prev.resetKey !== this.props.resetKey && this.state.error !== null) {
      this.setState({ error: null });
    }
  }

  private handleReset = () => {
    this.setState({ error: null });
  };

  override render() {
    if (this.state.error) {
      const errorPanel = (
        <section className="panel panel-error" role="alert">
          <div className="panel-head">
            <h2 className="panel-title">Etwas ist schiefgelaufen</h2>
          </div>
          <p className="panel-text">
            {this.props.inline
              ? "Dieser Bereich konnte nicht angezeigt werden."
              : "Diese Seite konnte nicht angezeigt werden."}{" "}
            Der Fehler wurde im System-Log protokolliert.
          </p>
          <pre className="panel-note text-error error-boundary-details">
            {this.state.error.message}
          </pre>
          <div className="error-boundary-actions">
            <button type="button" className="btn btn-secondary" onClick={this.handleReset}>
              Erneut versuchen
            </button>
            {!this.props.inline && (
              <button
                type="button"
                className="btn btn-primary"
                onClick={() => window.location.reload()}
              >
                Seite neu laden
              </button>
            )}
          </div>
        </section>
      );

      if (this.props.inline) {
        return errorPanel;
      }

      return (
        <main className="app-shell">
          <div className="page-container">{errorPanel}</div>
        </main>
      );
    }

    return this.props.children;
  }
}
