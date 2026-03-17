type Props = {
  title?: string;
  description?: string;
};

export default function LoadingState({
  title = "Daten werden geladen...",
  description = "Bitte warten Sie einen kurzen Moment.",
}: Props) {
  return (
    <section className="panel panel-muted" role="status" aria-live="polite">
      <div className="loading-row">
        <span className="loading-dot" />
        <div>
          <h3 className="panel-title">{title}</h3>
          <p className="panel-text">{description}</p>
        </div>
      </div>
    </section>
  );
}
