import type { ReactNode } from "react";

export type PageHeaderVariant = "section" | "workspace" | "detail";

type Props = {
  /**
   * section   – Major landing areas (Übersicht, Administration).
   *             Largest title, highest visual weight.
   * workspace – Default. List/task/form pages (Vorgänge, Aufgaben, Neuer Vorgang).
   *             Standard working weight.
   * detail    – Entity-focused pages (Vorgangsdetail, Personenhistorie).
   *             Compact, carries an optional eyebrow label above the title.
   */
  variant?: PageHeaderVariant;
  /** Small context label above the title – used in detail headers only. */
  eyebrow?: string;
  title: string;
  description?: string;
  actions?: ReactNode;
};

export default function PageHeader({
  variant = "workspace",
  eyebrow,
  title,
  description,
  actions,
}: Props) {
  return (
    <header className={`page-header page-header--${variant}`}>
      <div className="page-header__main">
        {eyebrow ? <p className="page-header__eyebrow">{eyebrow}</p> : null}
        <h1 className="page-title">{title}</h1>
        {description ? <p className="page-description">{description}</p> : null}
      </div>
      {actions ? <div className="page-header__actions">{actions}</div> : null}
    </header>
  );
}
