import type { HTMLAttributes, ReactNode } from "react";

type CardVariant = "primary" | "list" | "stat";
type CardElement = "article" | "div" | "section" | "li";

type CardProps = HTMLAttributes<HTMLElement> & {
  as?: CardElement;
  children: ReactNode;
  variant: CardVariant;
};

export default function Card({
  as = "article",
  children,
  className,
  variant,
  ...props
}: CardProps) {
  const Component = as;
  const classes = [`card-${variant}`, className].filter(Boolean).join(" ");

  return (
    <Component className={classes} {...props}>
      {children}
    </Component>
  );
}
