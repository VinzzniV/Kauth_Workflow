type Props = {
  title?: string;
  description?: string;
};

export default function LoadingState({
  title = "Daten werden geladen...",
  description,
}: Props) {
  return (
    <section className="panel panel-muted" role="status" aria-live="polite">
      <div className="loading-row">
        <span className="loading-dot" />
        <div>
          <h3 className="panel-title">{title}</h3>
          {description ? <p className="panel-text">{description}</p> : null}
        </div>
      </div>
    </section>
  );
}
