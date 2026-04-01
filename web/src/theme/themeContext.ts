import { createContext } from "react";
import type { ThemeMode, ThemePreference } from "./theme";

export type ThemeContextValue = {
  theme: ThemeMode;
  preference: ThemePreference;
  setTheme: (mode: ThemeMode) => void;
  toggleTheme: () => void;
};

export const ThemeContext = createContext<ThemeContextValue | null>(null);
