type Props = {
  isLoading: boolean;
  disabled: boolean;
  onSubmit: () => void;
};

export default function CreateWorkflowButton({ isLoading, disabled, onSubmit }: Props) {
  return (
    <div className="action-row">
      <button type="button" className="btn btn-primary" onClick={onSubmit} disabled={disabled || isLoading}>
        {isLoading ? "Onboarding wird gestartet..." : "Onboarding starten"}
      </button>
    </div>
  );
}
