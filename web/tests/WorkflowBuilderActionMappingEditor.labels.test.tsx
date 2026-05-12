import { fireEvent, render, screen, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { WorkflowBuilderActionMappingEditor } from "../src/components/admin-config/WorkflowBuilderActionMappingEditor";
import type { AdminAnswerDefinition, AdminWorkflowActionDefinition } from "../src/types/auth";
import type { AdminAutomationPropertyCatalog } from "../src/services/adminConfigApi";

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
    },
    required: ["firstName"],
  },
};

const ANSWERS: AdminAnswerDefinition[] = [];

const CATALOG: AdminAutomationPropertyCatalog = {
  sources: [
    {
      source: "workflow",
      label: "Workflow-Feld",
      properties: [
        { key: "firstName", label: "Vorname", kind: "business" },
        { key: "email", label: "E-Mail", kind: "business" },
        { key: "workflowId", label: "Workflow-ID (technisch)", kind: "technical" },
        { key: "definitionKey", label: "Definition-Schluessel (technisch)", kind: "technical" },
      ],
    },
    {
      source: "target_person",
      label: "Person (Ziel)",
      properties: [],
    },
    {
      source: "directory_identity",
      label: "Verzeichnis-Identität",
      properties: [],
    },
    { source: "answer", label: "Antwort aus Formular", properties: [] },
    { source: "static", label: "Statischer Wert", properties: [] },
  ],
};

describe("WorkflowBuilderActionMappingEditor — labels + optgroups", () => {
  it("renders Fachfelder optgroup before Technische Felder for workflow source", () => {
    render(
      <WorkflowBuilderActionMappingEditor
        actionKey="create_ad_user"
        actionDefinitions={[ACTION_DEF]}
        propertyCatalog={CATALOG}
        inputMappingText='{"firstName":{"source":"workflow","property":""}}'
        answerDefinitions={ANSWERS}
        onChange={vi.fn()}
      />
    );

    const select = screen.getAllByRole("combobox").find(
      (el) => el.tagName === "SELECT" && (el as HTMLSelectElement).querySelector("optgroup") !== null
    ) as HTMLSelectElement;
    expect(select).toBeTruthy();

    const groups = Array.from(select.querySelectorAll("optgroup"));
    expect(groups.map((g) => g.label)).toEqual(["Fachfelder", "Technische Felder"]);

    const businessLabels = Array.from(groups[0]!.querySelectorAll("option")).map((o) => o.textContent);
    expect(businessLabels).toEqual(["Vorname", "E-Mail"]);

    const technicalLabels = Array.from(groups[1]!.querySelectorAll("option")).map((o) => o.textContent);
    expect(technicalLabels).toEqual(["Workflow-ID (technisch)", "Definition-Schluessel (technisch)"]);
  });

  it("sends key (not label) when a property is chosen", () => {
    const onChange = vi.fn();
    render(
      <WorkflowBuilderActionMappingEditor
        actionKey="create_ad_user"
        actionDefinitions={[ACTION_DEF]}
        propertyCatalog={CATALOG}
        inputMappingText='{"firstName":{"source":"workflow","property":""}}'
        answerDefinitions={ANSWERS}
        onChange={onChange}
      />
    );

    const propertySelect = screen.getAllByRole("combobox").find(
      (el) => el.tagName === "SELECT" && (el as HTMLSelectElement).querySelector("optgroup") !== null
    ) as HTMLSelectElement;

    fireEvent.change(propertySelect, { target: { value: "workflowId" } });

    expect(onChange).toHaveBeenCalled();
    const parsed = JSON.parse(onChange.mock.calls[onChange.mock.calls.length - 1]![0]);
    expect(parsed.firstName).toEqual({ source: "workflow", property: "workflowId" });
  });

  it("shows an unknown gemapptes Property under Technische Felder with (unbekannt) suffix", () => {
    render(
      <WorkflowBuilderActionMappingEditor
        actionKey="create_ad_user"
        actionDefinitions={[ACTION_DEF]}
        propertyCatalog={CATALOG}
        inputMappingText='{"firstName":{"source":"workflow","property":"legacyField"}}'
        answerDefinitions={ANSWERS}
        onChange={vi.fn()}
      />
    );

    const propertySelect = screen.getAllByRole("combobox").find(
      (el) => el.tagName === "SELECT" && (el as HTMLSelectElement).querySelector("optgroup") !== null
    ) as HTMLSelectElement;

    const technicalGroup = Array.from(propertySelect.querySelectorAll("optgroup")).find(
      (g) => g.label === "Technische Felder"
    )!;

    const technicalLabels = Array.from(technicalGroup.querySelectorAll("option")).map((o) => o.textContent);
    expect(technicalLabels).toContain("legacyField (unbekannt)");
    expect(propertySelect.value).toBe("legacyField");
  });

  it("omits an empty optgroup when all properties are business-kind", () => {
    const businessOnly: AdminAutomationPropertyCatalog = {
      sources: [
        {
          source: "directory_identity",
          label: "Verzeichnis-Identität",
          properties: [
            { key: "mail", label: "E-Mail", kind: "business" },
          ],
        },
        { source: "workflow", label: "Workflow-Feld", properties: [] },
        { source: "target_person", label: "Person (Ziel)", properties: [] },
        { source: "answer", label: "Antwort aus Formular", properties: [] },
        { source: "static", label: "Statischer Wert", properties: [] },
      ],
    };

    render(
      <WorkflowBuilderActionMappingEditor
        actionKey="create_ad_user"
        actionDefinitions={[ACTION_DEF]}
        propertyCatalog={businessOnly}
        inputMappingText='{"firstName":{"source":"directory_identity","property":""}}'
        answerDefinitions={ANSWERS}
        onChange={vi.fn()}
      />
    );

    const propertySelect = screen.getAllByRole("combobox").find(
      (el) => el.tagName === "SELECT" && (el as HTMLSelectElement).querySelector("optgroup") !== null
    ) as HTMLSelectElement;
    const groups = Array.from(propertySelect.querySelectorAll("optgroup"));
    expect(groups.map((g) => g.label)).toEqual(["Fachfelder"]);
    expect(within(groups[0]!).getByText("E-Mail")).toBeTruthy();
  });
});
