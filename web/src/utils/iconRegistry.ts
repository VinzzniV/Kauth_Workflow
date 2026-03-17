import fallbackIcon from "../assets/icons/react.svg";

type IconAssetModule = Record<string, string>;

type RequirementIconResult = {
  src: string;
  normalizedKey: string;
  isFallback: boolean;
};

export const DEFAULT_ICON_KEY = "permissions";

const iconAssetModules = import.meta.glob("../assets/icons/**/*.{svg,png,webp,ico}", {
  eager: true,
  import: "default",
}) as IconAssetModule;

function normalizeIconToken(value: string): string {
  return value
    .normalize("NFKD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "_")
    .replace(/^_+|_+$/g, "")
    .replace(/_+/g, "_");
}

function toFileBaseName(filePath: string): string {
  const fileName = filePath.split("/").pop() ?? "";
  return fileName.replace(/\.[^.]+$/, "");
}

function buildIconRegistry(): Record<string, string> {
  const registry: Record<string, string> = {};

  for (const [filePath, src] of Object.entries(iconAssetModules)) {
    if (typeof src !== "string") {
      continue;
    }

    const iconKey = normalizeIconToken(toFileBaseName(filePath));
    if (!iconKey) {
      continue;
    }

    registry[iconKey] = src;
  }

  return registry;
}

const iconRegistry = buildIconRegistry();
const FALLBACK_ICON_KEYS = ["fallback", "default", DEFAULT_ICON_KEY, "unknown"];

function resolveFallbackIconSource(): string {
  for (const key of FALLBACK_ICON_KEYS) {
    if (iconRegistry[key]) {
      return iconRegistry[key];
    }
  }

  return fallbackIcon;
}

const fallbackIconSource = resolveFallbackIconSource();

export function normalizeIconKey(iconKey: string): string {
  return normalizeIconToken((iconKey ?? "").trim());
}

export function coerceIconKey(iconKey?: string | null): string {
  const key = (iconKey ?? "").trim();
  return key.length > 0 ? key : DEFAULT_ICON_KEY;
}

export function resolveRequirementIcon(iconKey: string): string {
  return getRequirementIcon(iconKey).src;
}

export function getRequirementIcon(iconKey: string): RequirementIconResult {
  const normalizedKey = normalizeIconKey(iconKey);
  const src = normalizedKey ? iconRegistry[normalizedKey] : undefined;

  if (src) {
    return {
      src,
      normalizedKey,
      isFallback: false,
    };
  }

  return {
    src: fallbackIconSource,
    normalizedKey,
    isFallback: true,
  };
}
