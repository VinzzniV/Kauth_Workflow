import { describe, expect, it } from "vitest";
import {
  applyRequirementBooleanEditorSelection,
  applyRequirementSingleSelectEditorSelection,
} from "../src/utils/requirementEditor";
import { buildRequirementSelections } from "../src/utils/requirements";
import { createRequirementSnapshot } from "./testUtils";

describe("requirementEditor", () => {
  it("applies configured reset targets for boolean editor selections", () => {
    const requirements = [
      createRequirementSnapshot({
        id: 1,
        key: "hardware_requested",
        inputType: "boolean",
        behavior: {
          visibilityDependencies: [],
          validation: null,
          resetTargetsWhenNotTrue: [
            {
              requirementKey: "hardware_note",
              clearBoolean: false,
              clearText: true,
              clearNumber: false,
              clearSelectedOption: false,
              clearSelectedOptions: false,
            },
          ],
          singleSelectReset: null,
        },
      }),
      createRequirementSnapshot({
        workflowRequirementId: 2,
        id: 2,
        key: "hardware_note",
        inputType: "text",
        value: {
          valueBoolean: null,
          valueText: "Notebook bereitstellen",
          valueNumber: null,
          selectedOptionId: null,
          selectedOptionKey: null,
          selectedOptionValue: null,
          selectedOptionLabel: null,
          selectedOptions: [],
        },
      }),
    ];

    const nextSelections = applyRequirementBooleanEditorSelection(
      requirements,
      buildRequirementSelections(requirements),
      1,
      false
    );

    expect(nextSelections[1]?.valueBoolean).toBe(false);
    expect(nextSelections[2]?.valueText).toBe("");
  });

  it("applies single-select editor resets when the selected option is not in the keep list", () => {
    const requirements = [
      createRequirementSnapshot({
        id: 1,
        key: "hardware_type",
        inputType: "select",
        options: [
          { id: 10, sourceOptionId: null, key: "laptop", value: "laptop", label: "Laptop", sortOrder: 1 },
          { id: 11, sourceOptionId: null, key: "none", value: "none", label: "Kein Gerät", sortOrder: 2 },
        ],
        behavior: {
          visibilityDependencies: [],
          validation: null,
          resetTargetsWhenNotTrue: [],
          singleSelectReset: {
            keepSelectedOptionValues: ["laptop"],
            targets: [
              {
                requirementKey: "hardware_note",
                clearBoolean: false,
                clearText: true,
                clearNumber: false,
                clearSelectedOption: false,
                clearSelectedOptions: false,
              },
            ],
          },
        },
      }),
      createRequirementSnapshot({
        workflowRequirementId: 2,
        id: 2,
        key: "hardware_note",
        inputType: "text",
        value: {
          valueBoolean: null,
          valueText: "Dockingstation nötig",
          valueNumber: null,
          selectedOptionId: null,
          selectedOptionKey: null,
          selectedOptionValue: null,
          selectedOptionLabel: null,
          selectedOptions: [],
        },
      }),
    ];

    const nextSelections = applyRequirementSingleSelectEditorSelection(
      requirements,
      buildRequirementSelections(requirements),
      1,
      11
    );

    expect(nextSelections[1]?.selectedOptionId).toBe(11);
    expect(nextSelections[2]?.valueText).toBe("");
  });
});
