import type { WorkflowCancellationReasonCode } from "../../types/workflow";

export const WORKFLOW_CANCELLATION_REASON_OPTIONS: ReadonlyArray<{
  code: WorkflowCancellationReasonCode;
  label: string;
}> = [
  { code: "entry_cancelled", label: "Eintritt abgesagt" },
  { code: "entry_postponed", label: "Eintritt verschoben" },
  { code: "wrong_person", label: "Falsche Person / Stammdaten" },
  { code: "started_by_mistake", label: "Versehentlich gestartet" },
  { code: "other", label: "Sonstiges" },
];
