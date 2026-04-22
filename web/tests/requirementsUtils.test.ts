import { describe, expect, it } from "vitest";
import { buildRequirementSelections, toRequirementSelectionPayload } from "../src/utils/requirements";
import { createRequirementSnapshot } from "./testUtils";

describe("requirements utils", () => {
  it("defaults unanswered boolean snapshots to false in editor selections", () => {
    const requirements = [
      createRequirementSnapshot({
        id: 1,
        workflowRequirementId: 1,
        key: "hardware_requested",
        inputType: "boolean",
        value: {
          valueBoolean: null,
          valueText: null,
          valueNumber: null,
          selectedOptionId: null,
          selectedOptionKey: null,
          selectedOptionValue: null,
          selectedOptionLabel: null,
          selectedOptions: [],
        },
      }),
    ];

    const selections = buildRequirementSelections(requirements);

    expect(selections[1]?.valueBoolean).toBe(false);
  });

  it("serializes missing boolean selections as false", () => {
    const requirements = [
      createRequirementSnapshot({
        id: 1,
        workflowRequirementId: 1,
        key: "hardware_requested",
        inputType: "boolean",
      }),
    ];

    const payload = toRequirementSelectionPayload(requirements, {});

    expect(payload).toEqual([
      {
        requirementId: 1,
        valueBoolean: false,
      },
    ]);
  });
});
