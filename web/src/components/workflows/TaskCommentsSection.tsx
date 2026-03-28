import { useEffect, useState } from "react";
import { formatDateTime } from "../../utils/dateFormat";
import type { WorkflowTask } from "../../types/workflow";

type TaskCommentsSectionProps = {
  task: WorkflowTask;
  draftValue: string;
  isSaving: boolean;
  onDraftChange: (taskId: number, value: string) => void;
  onSubmit: (taskId: number) => Promise<void>;
};

export default function TaskCommentsSection({
  task,
  draftValue,
  isSaving,
  onDraftChange,
  onSubmit,
}: TaskCommentsSectionProps) {
  const [isOpen, setIsOpen] = useState<boolean>(false);
  const trimmedDraft = draftValue.trim();
  const latestComment = task.comments.at(-1) ?? null;
  const commentCountLabel = `${task.comments.length} Eintrag${task.comments.length === 1 ? "" : "e"} zur Aufgabe.`;

  useEffect(() => {
    if (trimmedDraft || isSaving) {
      setIsOpen(true);
    }
  }, [isSaving, trimmedDraft]);

  return (
    <details
      className="panel panel-muted"
      name={`task-comments-${task.id}`}
      open={isOpen}
      onToggle={(event) => setIsOpen(event.currentTarget.open)}
    >
      <summary className="panel-head" style={{ cursor: "pointer", listStyle: "none" }}>
        <div className="task-comments-summary">
          <div>
            <h3>Kommentare / Klärungen</h3>
            <p>{commentCountLabel}</p>
          </div>
          <div className="task-comments-meta">
            {latestComment ? (
              <p className="panel-note">
                Letzte Aktivität: {latestComment.authorUserName ?? "Unbekannt"} ·{" "}
                {formatDateTime(latestComment.createdAt)}
              </p>
            ) : task.canAddComment ? (
              <p className="panel-note">Noch keine Kommentare. Rückfragen direkt an der Aufgabe dokumentieren.</p>
            ) : (
              <p className="panel-note">Keine Kommentare vorhanden.</p>
            )}
            {trimmedDraft ? <span className="chip">Entwurf offen</span> : null}
          </div>
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

      {task.canAddComment ? (
        <div className="task-comments-form">
          <div className="task-comments-form-head">
            <p className="panel-note">Kommentare sind für alle Beteiligten mit Aufgaben-Zugriff sichtbar.</p>
            <p className="panel-note task-comments-charcount">{trimmedDraft.length}/2000 Zeichen</p>
          </div>

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
              disabled={isSaving || !trimmedDraft}
              onClick={() => void onSubmit(task.id)}
            >
              {isSaving ? "Speichert..." : "Kommentar speichern"}
            </button>
          </div>
        </div>
      ) : null}
    </details>
  );
}
