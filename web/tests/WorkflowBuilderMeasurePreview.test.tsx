import { fireEvent, render, screen } from "@testing-library/react";
import { describe, it } from "vitest";
import { WorkflowBuilderMeasurePreview } from "../src/components/admin-config/WorkflowBuilderMeasurePreview";
import type {
  AdminAnswerDefinition,
  AdminResponsibilityOwner,
  AdminTaskSpec,
  AdminTaskSpecCondition,
  AdminTaskSpecDependency,
  AdminWorkflowDefinitionSummary,
} from "../src/types/auth";
import type {
  WorkflowBuilderNodeDraft,
  WorkflowBuilderVersionDraft,
} from "../src/hooks/adminWorkflowBuilderModel";

function makeSpec(
  partial: Partial<AdminTaskSpec> & Pick<AdminTaskSpec, "id" | "title" | "workflowDefinitionId">
): AdminTaskSpec {
  return {
    specKey: `spec_${partial.id}`,
    category: "general",
    description: "",
    iconKey: null,
    owningDepartmentId: null,
    defaultResponsibilityId: null,
    processAreaLabel: null,
    isDepartmentPhaseTask: true,
    isRequired: false,
    dueInDays: null,
    sortOrder: 0,
    isActive: true,
    createdAt: "2024-01-01T00:00:00Z",
    conditionCount: 0,
    dependencyCount: 0,
    ...partial,
  };
}

function makeDefinition(
  id: number,
  key: string,
  name: string
): AdminWorkflowDefinitionSummary {
  return { id, key, name, description: null, versions: [] };
}

function makeNode(nodeType: WorkflowBuilderNodeDraft["nodeType"]): WorkflowBuilderNodeDraft {
  return {
    id: "n1",
    nodeKey: "measure1",
    nodeType,
    title: "Maßnahmen",
    sortOrder: "1",
    positionX: null,
    positionY: null,
    configText: "",
    actions: [],
  };
}

function makeVersionDraft(primaryKey: string): WorkflowBuilderVersionDraft {
  return {
    name: "v1",
    description: "",
    primaryLegacyProcessTypeKey: primaryKey,
    nodes: [],
    edges: [],
  };
}

const DEFINITION = makeDefinition(10, "position_change", "Stellenwechsel");

const SPEC_1 = makeSpec({
  id: 100,
  title: "AD-Account anlegen",
  workflowDefinitionId: 10,
  isRequired: true,
  conditionCount: 1,
});
const SPEC_2 = makeSpec({
  id: 101,
  title: "IT-Schulung buchen",
  workflowDefinitionId: 10,
  dependencyCount: 1,
});

const CONDITION: AdminTaskSpecCondition = {
  id: 200,
  taskSpecId: 100,
  conditionGroup: 1,
  answerKey: "has_laptop",
  operator: "is_true",
  expectedValueText: null,
  expectedValueBoolean: null,
  expectedValueNumber: null,
};

const DEPENDENCY: AdminTaskSpecDependency = {
  id: 300,
  taskSpecId: 101,
  dependsOnTaskSpecId: 100,
  dependsOnSpecTitle: "AD-Account anlegen",
  requiredStatus: "done",
};

const ANSWER: AdminAnswerDefinition = {
  id: 1,
  workflowDefinitionId: 10,
  answerKey: "has_laptop",
  title: "Laptop verfügbar",
  category: "general",
  description: "",
  iconKey: null,
  inputType: "boolean",
  isRequired: false,
  sortOrder: 0,
  isActive: true,
};

const OWNER: AdminResponsibilityOwner = {
  responsibilityId: 5,
  responsibilityKey: "it_dept",
  systemKey: null,
  responsibilityName: "IT-Abteilung",
  responsibilityType: "department",
  departmentId: 1,
  departmentName: "IT",
  appUserId: null,
  appUserDisplayName: null,
};

describe("WorkflowBuilderMeasurePreview", () => {
  it("shows template count in collapsed toggle", () => {
    render(
      <WorkflowBuilderMeasurePreview
        node={makeNode("measure_provision")}
        versionDraft={makeVersionDraft("position_change")}
        workflowDefinitions={[DEFINITION]}
        taskTemplates={[SPEC_1, SPEC_2]}
        answerDefinitions={[ANSWER]}
        taskTemplateConditions={[CONDITION]}
        taskTemplateDependencies={[DEPENDENCY]}
        responsibilityOwners={[OWNER]}
      />
    );

    screen.getByText(/Geplante Maßnahmen \(2/i);
    screen.getByText(/davon 1 Pflicht/i);
  });

  it("shows condition and dependency labels when expanded", () => {
    render(
      <WorkflowBuilderMeasurePreview
        node={makeNode("measure_provision")}
        versionDraft={makeVersionDraft("position_change")}
        workflowDefinitions={[DEFINITION]}
        taskTemplates={[SPEC_1, SPEC_2]}
        answerDefinitions={[ANSWER]}
        taskTemplateConditions={[CONDITION]}
        taskTemplateDependencies={[DEPENDENCY]}
        responsibilityOwners={[OWNER]}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: /Geplante Maßnahmen/i }));

    // Condition label: "Laptop verfügbar ist ausgewählt"
    screen.getByText(/Laptop verfügbar ist ausgewählt/i);
    // Dependency label: "Wartet auf Abschluss von AD-Account anlegen"
    screen.getByText(/Wartet auf Abschluss von AD-Account anlegen/i);
  });

  it("shows empty hint when no matching definition key exists", () => {
    render(
      <WorkflowBuilderMeasurePreview
        node={makeNode("measure_provision")}
        versionDraft={makeVersionDraft("unknown_key")}
        workflowDefinitions={[DEFINITION]}
        taskTemplates={[SPEC_1]}
        answerDefinitions={[]}
        taskTemplateConditions={[]}
        taskTemplateDependencies={[]}
        responsibilityOwners={[]}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: /Geplante Maßnahmen/i }));

    screen.getByText(/keine aktiven Fachbereichs-Vorlagen geladen/i);
  });
});
