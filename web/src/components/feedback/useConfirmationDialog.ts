import { useContext } from "react";
import { ConfirmationDialogContext } from "./confirmationDialogContext";

export function useConfirmationDialog() {
  const context = useContext(ConfirmationDialogContext);

  if (!context) {
    throw new Error("useConfirmationDialog must be used within a ConfirmationDialogProvider.");
  }

  return context;
}
