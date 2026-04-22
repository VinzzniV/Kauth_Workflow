import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import RequirementsSelection from "../src/components/workflows/RequirementsSelection";
import { buildRequirementSelections } from "../src/utils/requirements";
import { createRequirementSnapshot } from "./testUtils";

describe("RequirementsSelection", () => {
  it("keeps backend snapshot visibility until local selections actually change", () => {
    const requirements = [
      createRequirementSnapshot({
        id: 1,
        key: "hardware_requested",
        title: "Hardware benötigt",
        inputType: "boolean",
        isVisible: true,
        value: {
          valueBoolean: false,
          valueText: null,
          valueNumber: null,
          selectedOptionId: null,
          selectedOptionKey: null,
          selectedOptionValue: null,
          selectedOptionLabel: null,
          selectedOptions: [],
        },
      }),
      createRequirementSnapshot({
        workflowRequirementId: 2,
        id: 2,
        key: "hardware_details",
        title: "Hardwaredetails",
        inputType: "text",
        isVisible: false,
        behavior: {
          visibilityDependencies: [
            {
              dependencyKey: "hardware_requested",
              kind: "boolean_true",
              expectedValue: null,
              missingResult: false,
            },
          ],
          validation: null,
          resetTargetsWhenNotTrue: [],
          singleSelectReset: null,
        },
      }),
    ];

    const { rerender } = render(
      <RequirementsSelection
        requirements={requirements}
        mode="edit"
        selections={buildRequirementSelections(requirements)}
        isLoading={false}
        error={null}
      />
    );

    expect(screen.getByText("Hardware benötigt")).toBeTruthy();
    expect(screen.queryByText("Hardwaredetails")).toBeNull();

    rerender(
      <RequirementsSelection
        requirements={requirements}
        mode="edit"
        selections={{
          ...buildRequirementSelections(requirements),
          1: {
            ...buildRequirementSelections(requirements)[1],
            valueBoolean: true,
          },
        }}
        isLoading={false}
        error={null}
      />
    );

    expect(screen.getByText("Hardwaredetails")).toBeTruthy();
  });

  it("renders boolean requirements as toggle cards and reveals dependent text fields", () => {
    const onToggleBoolean = vi.fn();
    const requirements = [
      createRequirementSnapshot({
        id: 1,
        workflowRequirementId: 1,
        key: "hardware_available",
        title: "Hardware vorhanden?",
        description: "Ist passende Hardware bereits vorhanden?",
        inputType: "boolean",
        category: "ausstattung",
        value: {
          valueBoolean: false,
          valueText: null,
          valueNumber: null,
          selectedOptionId: null,
          selectedOptionKey: null,
          selectedOptionValue: null,
          selectedOptionLabel: null,
          selectedOptions: [],
        },
      }),
      createRequirementSnapshot({
        id: 2,
        workflowRequirementId: 2,
        key: "hardware_takeover_details",
        title: "Zu übernehmende Hardware",
        inputType: "text",
        category: "ausstattung",
        isVisible: false,
        behavior: {
          visibilityDependencies: [
            {
              dependencyKey: "hardware_available",
              kind: "boolean_true",
              expectedValue: null,
              missingResult: false,
            },
          ],
          validation: null,
          resetTargetsWhenNotTrue: [],
          singleSelectReset: null,
        },
      }),
    ];

    const initialSelections = buildRequirementSelections(requirements);
    const { rerender } = render(
      <RequirementsSelection
        requirements={requirements}
        mode="edit"
        selections={initialSelections}
        onToggleBoolean={onToggleBoolean}
        isLoading={false}
        error={null}
      />
    );

    const toggleCard = screen.getByRole("button", { name: /Hardware vorhanden/i });
    expect(toggleCard.getAttribute("aria-pressed")).toBe("false");
    expect(screen.queryByText("Zu übernehmende Hardware")).toBeNull();

    fireEvent.click(toggleCard);
    expect(onToggleBoolean).toHaveBeenCalledWith(1, true);

    rerender(
      <RequirementsSelection
        requirements={requirements}
        mode="edit"
        selections={{
          ...initialSelections,
          1: {
            ...initialSelections[1],
            valueBoolean: true,
          },
        }}
        onToggleBoolean={onToggleBoolean}
        isLoading={false}
        error={null}
      />
    );

    expect(screen.getByText("Zu übernehmende Hardware")).toBeTruthy();
  });
});
