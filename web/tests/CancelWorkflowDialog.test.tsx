import { fireEvent, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { CancelWorkflowDialog } from "../src/components/workflow-detail/CancelWorkflowDialog";
import { renderWithApp } from "./testUtils";

function setup({
  isSubmitting = false,
  errorMessage = null as string | null,
} = {}) {
  const onCancel = vi.fn();
  const onConfirm = vi.fn();
  renderWithApp(
    <CancelWorkflowDialog
      open
      isSubmitting={isSubmitting}
      errorMessage={errorMessage}
      onCancel={onCancel}
      onConfirm={onConfirm}
    />,
  );
  return { onCancel, onConfirm };
}

describe("CancelWorkflowDialog", () => {
  it("renders the title, description and reason options", () => {
    setup();
    expect(screen.getByRole("alertdialog", { name: /Workflow stornieren\?/i })).toBeTruthy();
    expect(screen.getByText(/Aufgaben werden mit-storniert/i)).toBeTruthy();
    const select = screen.getByLabelText(/^Grund/i) as HTMLSelectElement;
    expect(select).toBeTruthy();
    expect(select.querySelectorAll("option").length).toBeGreaterThan(3);
  });

  it("disables the submit button while no reason is selected", () => {
    setup();
    const submit = screen.getByRole("button", { name: /Vorgang stornieren/i }) as HTMLButtonElement;
    expect(submit.disabled).toBe(true);
  });

  it("enables submit when a reason other than 'Sonstiges' is picked", () => {
    setup();
    const select = screen.getByLabelText(/^Grund/i) as HTMLSelectElement;
    fireEvent.change(select, { target: { value: "entry_cancelled" } });
    const submit = screen.getByRole("button", { name: /Vorgang stornieren/i }) as HTMLButtonElement;
    expect(submit.disabled).toBe(false);
  });

  it("requires the detail field when reason is 'other'", () => {
    setup();
    const select = screen.getByLabelText(/^Grund/i) as HTMLSelectElement;
    fireEvent.change(select, { target: { value: "other" } });
    const submit = screen.getByRole("button", { name: /Vorgang stornieren/i }) as HTMLButtonElement;
    expect(submit.disabled).toBe(true);

    const detail = screen.getByLabelText(/Ergänzung/i) as HTMLTextAreaElement;
    fireEvent.change(detail, { target: { value: "Sonderfall im Detail" } });
    expect(submit.disabled).toBe(false);
  });

  it("invokes onConfirm with the chosen reason on submit", () => {
    const { onConfirm } = setup();
    const select = screen.getByLabelText(/^Grund/i) as HTMLSelectElement;
    fireEvent.change(select, { target: { value: "entry_postponed" } });
    const detail = screen.getByLabelText(/Ergänzung/i) as HTMLTextAreaElement;
    fireEvent.change(detail, { target: { value: "  Eintritt verschoben auf September.  " } });

    fireEvent.click(screen.getByRole("button", { name: /Vorgang stornieren/i }));

    expect(onConfirm).toHaveBeenCalledWith({
      reasonCode: "entry_postponed",
      reasonDetail: "Eintritt verschoben auf September.",
    });
  });

  it("omits empty reasonDetail when the textarea is blank", () => {
    const { onConfirm } = setup();
    const select = screen.getByLabelText(/^Grund/i) as HTMLSelectElement;
    fireEvent.change(select, { target: { value: "wrong_person" } });

    fireEvent.click(screen.getByRole("button", { name: /Vorgang stornieren/i }));

    expect(onConfirm).toHaveBeenCalledWith({
      reasonCode: "wrong_person",
      reasonDetail: undefined,
    });
  });

  it("displays an error message when provided", () => {
    setup({ errorMessage: "Backend lehnte den Storno ab." });
    expect(screen.getByRole("alert").textContent).toContain("Backend lehnte den Storno ab.");
  });

  it("disables the cancel button while submitting", () => {
    setup({ isSubmitting: true });
    const cancelButton = screen.getByRole("button", { name: /Abbrechen/i }) as HTMLButtonElement;
    expect(cancelButton.disabled).toBe(true);
    const submit = screen.getByRole("button", { name: /Wird storniert/i }) as HTMLButtonElement;
    expect(submit.disabled).toBe(true);
  });
});
