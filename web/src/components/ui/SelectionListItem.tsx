import type { ButtonHTMLAttributes, ReactNode } from "react";

type SelectionListItemProps = Omit<ButtonHTMLAttributes<HTMLButtonElement>, "children"> & {
  title: ReactNode;
  meta?: ReactNode;
  secondaryMeta?: ReactNode;
  active?: boolean;
};

export default function SelectionListItem({
  title,
  meta,
  secondaryMeta,
  active = false,
  className,
  type = "button",
  ...props
}: SelectionListItemProps) {
  const classes = [
    "selection-list-item",
    active ? "selection-list-item--active" : "",
    className,
  ]
    .filter(Boolean)
    .join(" ");

  return (
    <button type={type} className={classes} aria-pressed={active} {...props}>
      <strong className="selection-list-item__title">{title}</strong>
      {meta ? <span className="selection-list-item__meta">{meta}</span> : null}
      {secondaryMeta ? <span className="selection-list-item__meta">{secondaryMeta}</span> : null}
    </button>
  );
}
