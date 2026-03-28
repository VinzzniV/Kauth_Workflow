import type { WorkflowTaskSlaStatus } from "../../types/workflow";
import { getTaskSlaClassName, getTaskSlaLabel } from "../../utils/taskStatus";

type Props = {
  status: WorkflowTaskSlaStatus;
};

export default function TaskSlaPill({ status }: Props) {
  if (status === "none") {
    return null;
  }

  return (
    <span
      className={`task-sla-pill ${getTaskSlaClassName(status)}`}
      aria-label={`Friststatus: ${getTaskSlaLabel(status)}`}
      title={`Friststatus: ${getTaskSlaLabel(status)}`}
    >
      {getTaskSlaLabel(status)}
    </span>
  );
}
