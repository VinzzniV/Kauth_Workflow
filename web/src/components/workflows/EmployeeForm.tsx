import { useId } from "react";
import type { EmployeeFormData } from "../../types/workflow";

type Props = {
  value: EmployeeFormData;
  onChange: (field: keyof EmployeeFormData, value: string | number) => void;
  fieldErrors?: Partial<Record<keyof EmployeeFormData, string>>;
};

export default function EmployeeForm({ value, onChange, fieldErrors }: Props) {
  // FE-5: explicit htmlFor/id paaren Label und Input — robuster fuer Screen-Reader.
  const firstNameId = useId();
  const lastNameId = useId();
  const employeeNumberId = useId();
  const badgeNumberId = useId();
  const deadlineId = useId();

  const currentDate = new Date();
  const today = `${currentDate.getFullYear()}-${String(currentDate.getMonth() + 1).padStart(2, "0")}-${String(
    currentDate.getDate()
  ).padStart(2, "0")}`;

  return (
    <section className="panel">
      <div className="panel-head">
        <h2>Daten der neuen Person</h2>
      </div>

      <div className="form-grid">
        <div className={`field ${fieldErrors?.firstName ? "field-invalid" : ""}`}>
          <label htmlFor={firstNameId}>Vorname</label>
          <input
            id={firstNameId}
            type="text"
            value={value.firstName}
            onChange={(event) => onChange("firstName", event.target.value)}
            placeholder="Max"
          />
          {fieldErrors?.firstName ? <small className="field-error">{fieldErrors.firstName}</small> : null}
        </div>

        <div className={`field ${fieldErrors?.lastName ? "field-invalid" : ""}`}>
          <label htmlFor={lastNameId}>Nachname</label>
          <input
            id={lastNameId}
            type="text"
            value={value.lastName}
            onChange={(event) => onChange("lastName", event.target.value)}
            placeholder="Mustermann"
          />
          {fieldErrors?.lastName ? <small className="field-error">{fieldErrors.lastName}</small> : null}
        </div>

        <div className={`field ${fieldErrors?.employeeNumber ? "field-invalid" : ""}`}>
          <label htmlFor={employeeNumberId}>Personalnummer</label>
          <input
            id={employeeNumberId}
            type="number"
            min={1}
            value={value.employeeNumber || ""}
            onChange={(event) => onChange("employeeNumber", Number(event.target.value || 0))}
            placeholder="10001"
          />
          {fieldErrors?.employeeNumber ? <small className="field-error">{fieldErrors.employeeNumber}</small> : null}
        </div>

        <div className={`field ${fieldErrors?.badgeNumber ? "field-invalid" : ""}`}>
          <label htmlFor={badgeNumberId}>Kartennummer</label>
          <input
            id={badgeNumberId}
            type="number"
            min={1}
            value={value.badgeNumber || ""}
            onChange={(event) => onChange("badgeNumber", Number(event.target.value || 0))}
            placeholder="60001"
          />
          {fieldErrors?.badgeNumber ? <small className="field-error">{fieldErrors.badgeNumber}</small> : null}
        </div>

        <div className="field">
          <label htmlFor={deadlineId}>Deadline</label>
          <input
            id={deadlineId}
            type="date"
            min={today}
            value={value.deadlineDate}
            onChange={(event) => onChange("deadlineDate", event.target.value)}
          />
        </div>
      </div>
    </section>
  );
}
