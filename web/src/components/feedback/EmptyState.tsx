type Props = {
  title: string;
  description?: string;
  actionLabel?: string;
  onAction?: () => void;
};

export default function EmptyState({ title, description, actionLabel, onAction }: Props) {
  return (
    <section className="panel panel-muted" role="status" aria-live="polite">
      <h3 className="panel-title">{title}</h3>
      {description ? <p className="panel-text">{description}</p> : null}
      {actionLabel && onAction ? (
        <button type="button" className="btn btn-secondary" onClick={onAction}>
          {actionLabel}
        </button>
      ) : null}
    </section>
  );
}
