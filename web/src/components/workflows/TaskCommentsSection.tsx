import { formatDateTime } from "../../utils/dateFormat";
import type { WorkflowTask } from "../../types/workflow";

type TaskCommentsSectionProps = {
  task: WorkflowTask;
  draftValue: string;
  isSaving: boolean;
  feedbackMessage: string | null;
  onDraftChange: (taskId: number, value: string) => void;
  onSubmit: (taskId: number) => Promise<void>;
};

export default function TaskCommentsSection({
  task,
  draftValue,
  isSaving,
  feedbackMessage,
  onDraftChange,
  onSubmit,
}: TaskCommentsSectionProps) {
  return (
    <details className="panel panel-muted" name={`task-comments-${task.id}`}>
      <summary className="panel-head" style={{ cursor: "pointer", listStyle: "none" }}>
        <div>
          <h3>Kommentare / Klärungen</h3>
          <p>{task.comments.length} Eintrag{task.comments.length === 1 ? "" : "e"} zur Aufgabe.</p>
        </div>
      </summary>

      {task.comments.length === 0 ? <p className="panel-note">Noch keine Kommentare vorhanden.</p> : null}

      {task.comments.length > 0 ? (
        <ol className="task-timeline">
          {task.comments.map((comment) => (
            <li key={comment.id} className="task-timeline-item">
              <div className="task-timeline-head">
                <strong>{comment.authorUserName ?? "Unbekannt"}</strong>
                <span className="chip">{formatDateTime(comment.createdAt)}</span>
              </div>
              <p className="panel-text">{comment.commentText}</p>
            </li>
          ))}
        </ol>
      ) : null}

      {feedbackMessage ? <p className="panel-note">{feedbackMessage}</p> : null}

      {task.canAddComment ? (
        <div className="toolbar-row task-actions-row">
          <label className="field grow">
            <span>Neuer Kommentar</span>
            <textarea
              value={draftValue}
              rows={3}
              maxLength={2000}
              disabled={isSaving}
              onChange={(event) => onDraftChange(task.id, event.target.value)}
              placeholder="Blocker, Rückfrage oder Kontext notieren"
            />
          </label>
          <button
            type="button"
            className="btn btn-secondary"
            disabled={isSaving || !draftValue.trim()}
            onClick={() => void onSubmit(task.id)}
          >
            Kommentar speichern
          </button>
        </div>
      ) : null}
    </details>
  );
}
