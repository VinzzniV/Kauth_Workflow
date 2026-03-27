type Props = {
  isLoading: boolean;
  disabled: boolean;
  label?: string;
  onSubmit: () => void;
};

export default function CreateWorkflowButton({ isLoading, disabled, label = "Neuer Vorgang", onSubmit }: Props) {
  return (
    <button type="button" className="btn btn-primary" onClick={onSubmit} disabled={disabled || isLoading}>
      {isLoading ? "Vorgang wird gestartet..." : label}
    </button>
  );
}
