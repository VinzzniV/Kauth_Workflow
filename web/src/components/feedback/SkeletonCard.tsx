type Props = {
  variant?: "workflow" | "task";
};

const VARIANT_WIDTHS: Record<NonNullable<Props["variant"]>, string[]> = {
  workflow: ["58%", "34%", "82%", "68%", "46%", "72%"],
  task: ["52%", "76%", "38%", "64%", "42%", "70%"],
};

export default function SkeletonCard({ variant = "workflow" }: Props) {
  const widths = VARIANT_WIDTHS[variant];

  return (
    <article className={`skeleton-card skeleton-card-${variant}`} aria-hidden="true">
      <div className="skeleton-card-head">
        <span className="skeleton-line" style={{ width: widths[0] }} />
        <span className="skeleton-pill" />
      </div>
      <div className="skeleton-stack">
        <span className="skeleton-line" style={{ width: widths[1] }} />
        <span className="skeleton-line" style={{ width: widths[2] }} />
      </div>
      <div className="skeleton-grid">
        <span className="skeleton-line" style={{ width: widths[3] }} />
        <span className="skeleton-line" style={{ width: widths[4] }} />
        <span className="skeleton-line" style={{ width: widths[5] }} />
        <span className="skeleton-line" style={{ width: widths[1] }} />
      </div>
    </article>
  );
}
