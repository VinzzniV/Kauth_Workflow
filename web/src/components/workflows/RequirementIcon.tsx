import { getRequirementIcon } from "../../utils/iconRegistry";

type Props = {
  iconKey: string;
  title: string;
  size?: "md" | "lg";
};

export default function RequirementIcon({ iconKey, title, size = "lg" }: Props) {
  const { src, isFallback } = getRequirementIcon(iconKey);
  const sizeClass = size === "md" ? "req-icon--md" : "req-icon--lg";
  const fallbackClass = isFallback ? "req-icon--fallback" : "";

  return (
    <span className={`req-icon ${sizeClass} ${fallbackClass}`} title={title}>
      <img src={src} alt={title} loading="lazy" decoding="async" />
    </span>
  );
}
