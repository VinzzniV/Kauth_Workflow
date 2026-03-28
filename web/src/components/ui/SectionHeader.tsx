import type { ReactNode } from "react";

type SectionHeaderProps = {
  title: ReactNode;
  description?: ReactNode;
  meta?: ReactNode;
  className?: string;
  headingTag?: "h2" | "h3" | "h4";
};

export default function SectionHeader({
  title,
  description,
  meta,
  className,
  headingTag = "h2",
}: SectionHeaderProps) {
  const Heading = headingTag;
  const classes = ["panel-head", className].filter(Boolean).join(" ");

  return (
    <div className={classes}>
      <Heading>{title}</Heading>
      {description ? <p>{description}</p> : null}
      {meta ? <p className="panel-note">{meta}</p> : null}
    </div>
  );
}
