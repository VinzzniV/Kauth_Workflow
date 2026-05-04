import { fireEvent, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { useConfirmationDialog } from "../src/components/feedback/useConfirmationDialog";
import { renderWithApp } from "./testUtils";

function ConfirmationDialogHarness() {
  const confirm = useConfirmationDialog();

  return (
    <button
      type="button"
      onClick={() => {
        void confirm({
          title: "Workflow löschen?",
          description: "Der Ablauf wird dauerhaft entfernt.",
          confirmLabel: "Workflow löschen",
          cancelLabel: "Abbrechen",
          tone: "danger",
        });
      }}
    >
      Dialog öffnen
    </button>
  );
}

describe("ConfirmationDialogProvider", () => {
  it("renders the app confirmation dialog with destructive styling", async () => {
    renderWithApp(<ConfirmationDialogHarness />);

    fireEvent.click(screen.getByRole("button", { name: /Dialog öffnen/i }));

    expect(await screen.findByRole("alertdialog", { name: /Workflow löschen\?/i })).toBeTruthy();
    expect(screen.getByText(/dauerhaft entfernt/i)).toBeTruthy();
    expect(screen.getByRole("button", { name: /Abbrechen/i })).toBeTruthy();
    expect(screen.getByRole("button", { name: /Workflow löschen/i })).toBeTruthy();
  });
});
