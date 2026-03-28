export type ThemeMode = "light" | "dark";
export type ThemePreference = ThemeMode | null;

export const THEME_STORAGE_KEY = "onboarding-theme-mode";

export function getSystemTheme(): ThemeMode {
  if (typeof window === "undefined" || typeof window.matchMedia !== "function") {
    return "light";
  }

  return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
}

export function readStoredThemePreference(): ThemePreference {
  if (typeof window === "undefined") {
    return null;
  }

  try {
    const storedValue = window.localStorage.getItem(THEME_STORAGE_KEY);
    return storedValue === "light" || storedValue === "dark" ? storedValue : null;
  } catch {
    return null;
  }
}

export function writeStoredThemePreference(preference: ThemePreference) {
  if (typeof window === "undefined") {
    return;
  }

  try {
    if (preference === null) {
      window.localStorage.removeItem(THEME_STORAGE_KEY);
      return;
    }

    window.localStorage.setItem(THEME_STORAGE_KEY, preference);
  } catch {
    // Ignore storage failures and keep the theme in memory only.
  }
}

export function resolveInitialTheme(): ThemeMode {
  return readStoredThemePreference() ?? getSystemTheme();
}

export function applyThemeToDocument(theme: ThemeMode) {
  if (typeof document === "undefined") {
    return;
  }

  document.documentElement.dataset.theme = theme;
  document.documentElement.style.colorScheme = theme;
}
