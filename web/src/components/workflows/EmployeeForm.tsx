import type { EmployeeFormData } from "../../types/workflow";

type Props = {
  value: EmployeeFormData;
  onChange: (field: keyof EmployeeFormData, value: string | number) => void;
  fieldErrors?: Partial<Record<keyof EmployeeFormData, string>>;
};

export default function EmployeeForm({ value, onChange, fieldErrors }: Props) {
  const currentDate = new Date();
  const today = `${currentDate.getFullYear()}-${String(currentDate.getMonth() + 1).padStart(2, "0")}-${String(
    currentDate.getDate()
  ).padStart(2, "0")}`;

  return (
    <section className="panel">
      <div className="panel-head">
        <h2>Daten der neuen Person</h2>
        <p>Erfassen Sie die Stammdaten für die neue Person.</p>
      </div>

      <div className="form-grid">
        <label className={`field ${fieldErrors?.firstName ? "field-invalid" : ""}`}>
          <span>Vorname</span>
          <input
            type="text"
            value={value.firstName}
            onChange={(event) => onChange("firstName", event.target.value)}
            placeholder="Max"
          />
          {fieldErrors?.firstName ? <small className="field-error">{fieldErrors.firstName}</small> : null}
        </label>

        <label className={`field ${fieldErrors?.lastName ? "field-invalid" : ""}`}>
          <span>Nachname</span>
          <input
            type="text"
            value={value.lastName}
            onChange={(event) => onChange("lastName", event.target.value)}
            placeholder="Mustermann"
          />
          {fieldErrors?.lastName ? <small className="field-error">{fieldErrors.lastName}</small> : null}
        </label>

        <label className={`field ${fieldErrors?.employeeNumber ? "field-invalid" : ""}`}>
          <span>Personalnummer</span>
          <input
            type="number"
            min={1}
            value={value.employeeNumber || ""}
            onChange={(event) => onChange("employeeNumber", Number(event.target.value || 0))}
            placeholder="10001"
          />
          {fieldErrors?.employeeNumber ? <small className="field-error">{fieldErrors.employeeNumber}</small> : null}
        </label>

        <label className={`field ${fieldErrors?.badgeNumber ? "field-invalid" : ""}`}>
          <span>Kartennummer</span>
          <input
            type="number"
            min={1}
            value={value.badgeNumber || ""}
            onChange={(event) => onChange("badgeNumber", Number(event.target.value || 0))}
            placeholder="60001"
          />
          {fieldErrors?.badgeNumber ? <small className="field-error">{fieldErrors.badgeNumber}</small> : null}
        </label>

        <label className="field">
          <span>Deadline</span>
          <input
            type="date"
            min={today}
            value={value.deadlineDate}
            onChange={(event) => onChange("deadlineDate", event.target.value)}
          />
          <small>Optional. Gilt als Zieltermin für den gesamten Vorgang.</small>
        </label>
      </div>
    </section>
  );
}
