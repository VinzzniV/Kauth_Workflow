import { createContext } from "react";

type ConfirmationTone = "default" | "danger";

export type ConfirmationDialogOptions = {
  title: string;
  description: string;
  confirmLabel?: string;
  cancelLabel?: string;
  tone?: ConfirmationTone;
};

export type ConfirmationRequest = {
  options: ConfirmationDialogOptions;
  resolve: (value: boolean) => void;
  previousActiveElement: HTMLElement | null;
};

export const ConfirmationDialogContext = createContext<
  ((options: ConfirmationDialogOptions) => Promise<boolean>) | null
>(null);
