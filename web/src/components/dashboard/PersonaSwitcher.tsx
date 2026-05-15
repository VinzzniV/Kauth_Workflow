import { useCurrentUser } from "../../auth/useCurrentUser";
import type { DashboardPersona, RoleCapabilities } from "../../auth/roleModel";

type PersonaOption = { persona: DashboardPersona; label: string };

const PERSONA_LABELS: Record<DashboardPersona, string> = {
  admin: "Admin",
  hr: "HR",
  manager: "Abteilungsleitung",
  worker: "Fachbereich",
  reader: "Leser",
  generic: "Allgemein",
};

function buildPersonaOptions(capabilities: RoleCapabilities): PersonaOption[] {
  const options: PersonaOption[] = [];
  if (capabilities.hasAdminRole) options.push({ persona: "admin", label: PERSONA_LABELS.admin });
  if (capabilities.hasHrRole) options.push({ persona: "hr", label: PERSONA_LABELS.hr });
  if (capabilities.hasManagerRole) options.push({ persona: "manager", label: PERSONA_LABELS.manager });
  if (capabilities.hasWorkerRole) options.push({ persona: "worker", label: PERSONA_LABELS.worker });
  if (capabilities.hasReaderRole) options.push({ persona: "reader", label: PERSONA_LABELS.reader });
  return options;
}

export default function PersonaSwitcher() {
  const { capabilities, activeView, setActiveView } = useCurrentUser();

  if (!capabilities.hasMultipleRoles) return null;

  const options = buildPersonaOptions(capabilities);
  if (options.length < 2) return null;

  return (
    <div className="persona-switcher" role="group" aria-label="Ansicht wechseln">
      <span className="persona-switcher__label">Ansicht:</span>
      {options.map(({ persona, label }) => (
        <button
          key={persona}
          type="button"
          className={`btn ${activeView === persona ? "btn-primary" : "btn-secondary"}`}
          aria-pressed={activeView === persona}
          onClick={() => setActiveView(persona)}
        >
          {label}
        </button>
      ))}
      <span className="persona-switcher__hint">Nur Anzeige – keine Rechteänderung</span>
    </div>
  );
}
