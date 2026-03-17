import type { WorkflowTaskStatus } from "../../types/workflow";
import { getTaskStatusClassName, getTaskStatusLabel } from "../../utils/taskStatus";

type Props = {
  status: WorkflowTaskStatus;
};

export default function TaskStatusPill({ status }: Props) {
  return <span className={`task-pill ${getTaskStatusClassName(status)}`}>{getTaskStatusLabel(status)}</span>;
}
