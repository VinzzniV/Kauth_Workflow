import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { WorkflowBuilderActionMappingEditor } from "../src/components/admin-config/WorkflowBuilderActionMappingEditor";
import type { AdminAnswerDefinition, AdminWorkflowActionDefinition } from "../src/types/auth";

const ACTION_DEF: AdminWorkflowActionDefinition = {
  id: 1,
  actionKey: "create_ad_user",
  displayName: "AD-User anlegen",
  description: "Legt einen Active Directory User an.",
  handlerKey: "AdUserHandler",
  isIdempotent: false,
  isActive: true,
  inputSchema: {
    properties: {
      firstName: { type: "string" },
      lastName: { type: "string" },
    },
    required: ["firstName"],
  },
};

const ANSWERS: AdminAnswerDefinition[] = [
  {
    id: 1,
    workflowDefinitionId: 1,
    answerKey: "comparison_user",
    title: "Vergleichs-User",
    category: "general",
    description: "",
    iconKey: null,
    inputType: "text",
    isRequired: false,
    sortOrder: 0,
    isActive: true,
  },
];

describe("WorkflowBuilderActionMappingEditor", () => {
  it("renders one row per inputSchema property and marks required params with an asterisk", () => {
    render(
      <WorkflowBuilderActionMappingEditor
        actionKey="create_ad_user"
        actionDefinitions={[ACTION_DEF]}
        propertyCatalog={null}
        inputMappingText=""
        answerDefinitions={ANSWERS}
        onChange={vi.fn()}
      />
    );

    screen.getByText("firstName");
    screen.getByText("lastName");
    // Required-Marker
    const firstName = screen.getByText("firstName").closest(".wf-mapping-param-name");
    expect(firstName?.textContent).toContain("*");
  });

  it("emits a static entry when the static input field changes", () => {
    const onChange = vi.fn();
    render(
      <WorkflowBuilderActionMappingEditor
        actionKey="create_ad_user"
        actionDefinitions={[ACTION_DEF]}
        propertyCatalog={null}
        inputMappingText=""
        answerDefinitions={ANSWERS}
        onChange={onChange}
      />
    );

    const sourceSelect = screen.getByLabelText("Quelle für firstName");
    expect((sourceSelect as HTMLSelectElement).value).toBe("static");

    // The static input has placeholder "Fester Wert" — find it via that
    const inputs = screen.getAllByPlaceholderText("Fester Wert");
    fireEvent.change(inputs[0]!, { target: { value: "Ada" } });

    expect(onChange).toHaveBeenCalled();
    const lastCall = onChange.mock.calls[onChange.mock.calls.length - 1]!;
    const parsed = JSON.parse(lastCall[0]);
    expect(parsed.firstName).toEqual({ source: "static", value: "Ada" });
  });

  it("switching source to workflow renders the property dropdown with default entry", () => {
    const onChange = vi.fn();
    render(
      <WorkflowBuilderActionMappingEditor
        actionKey="create_ad_user"
        actionDefinitions={[ACTION_DEF]}
        propertyCatalog={null}
        inputMappingText=""
        answerDefinitions={ANSWERS}
        onChange={onChange}
      />
    );

    fireEvent.change(screen.getByLabelText("Quelle für firstName"), { target: { value: "workflow" } });

    expect(onChange).toHaveBeenCalledTimes(1);
    const parsed = JSON.parse(onChange.mock.calls[0]![0]);
    expect(parsed.firstName).toEqual({ source: "workflow", property: "" });
  });

  it("switching source to answer + selecting answer emits answer-source mapping", () => {
    const onChange = vi.fn();
    render(
      <WorkflowBuilderActionMappingEditor
        actionKey="create_ad_user"
        actionDefinitions={[ACTION_DEF]}
        propertyCatalog={null}
        inputMappingText='{"firstName":{"source":"answer","answerKey":""}}'
        answerDefinitions={ANSWERS}
        onChange={onChange}
      />
    );

    fireEvent.change(screen.getByLabelText("Antwort-Key für firstName"), {
      target: { value: "comparison_user" },
    });

    expect(onChange).toHaveBeenCalledTimes(1);
    const parsed = JSON.parse(onChange.mock.calls[0]![0]);
    expect(parsed.firstName).toEqual({ source: "answer", answerKey: "comparison_user" });
  });

  it("falls back to raw textarea for invalid JSON inputMappingText", () => {
    render(
      <WorkflowBuilderActionMappingEditor
        actionKey="create_ad_user"
        actionDefinitions={[ACTION_DEF]}
        propertyCatalog={null}
        inputMappingText="not-json"
        answerDefinitions={ANSWERS}
        onChange={vi.fn()}
      />
    );

    screen.getByText(/kein gültiges JSON-Objekt/i);
  });

  it("renders custom-flagged param row that's outside the schema with remove-button", () => {
    render(
      <WorkflowBuilderActionMappingEditor
        actionKey="create_ad_user"
        actionDefinitions={[ACTION_DEF]}
        propertyCatalog={null}
        inputMappingText='{"customExtra":{"source":"static","value":"x"}}'
        answerDefinitions={ANSWERS}
        onChange={vi.fn()}
      />
    );

    screen.getByText("customExtra");
    // Custom badge is rendered for params not in inputSchema
    expect(screen.getByText("customExtra").closest(".wf-mapping-param-name")?.textContent).toContain("custom");
    // Remove button only appears for non-schema params
    screen.getByLabelText("Parameter entfernen");
  });
});
