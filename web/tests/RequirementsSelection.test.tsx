import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
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
});
