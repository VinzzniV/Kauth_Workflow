// Pure parse/serialize helpers + types fuer die Workflow-Builder-Editor-Komponenten.
// Liegt absichtlich getrennt von den .tsx-Dateien, damit React Fast Refresh fuer die
// Komponenten funktioniert (Regel react-refresh/only-export-components).

// ─── Step config (parseConfig / writeString) ────────────────────────────────

export function parseConfig(text: string): Record<string, unknown> | null {
  if (!text.trim()) return {};
  try {
    const parsed: unknown = JSON.parse(text);
    if (parsed && typeof parsed === "object" && !Array.isArray(parsed)) {
      return parsed as Record<string, unknown>;
    }
    return null;
  } catch {
    return null;
  }
}

export function writeString(currentText: string, key: string, nextValue: string): string {
  const config = parseConfig(currentText) ?? {};
  const trimmed = nextValue.trim();
  if (trimmed) {
    config[key] = nextValue;
  } else {
    delete config[key];
  }
  return Object.keys(config).length === 0 ? "" : JSON.stringify(config, null, 2);
}

// ─── Condition editor ───────────────────────────────────────────────────────

export type ConditionOperator = "is_true" | "is_false" | "is_not_null" | "is_null" | "eq" | "neq";

export const ALL_CONDITION_OPERATORS: ConditionOperator[] = [
  "is_true",
  "is_false",
  "is_not_null",
  "is_null",
  "eq",
  "neq",
];

export type ParsedCondition = {
  answerKey: string;
  operator: ConditionOperator;
  expectedValueText: string;
  expectedValueBoolean: boolean | null;
  expectedValueNumber: string;
};

export function parseCondition(text: string): ParsedCondition | "invalid" {
  if (!text.trim()) {
    return {
      answerKey: "",
      operator: "is_true",
      expectedValueText: "",
      expectedValueBoolean: null,
      expectedValueNumber: "",
    };
  }
  try {
    const parsed: unknown = JSON.parse(text);
    if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) return "invalid";
    const obj = parsed as Record<string, unknown>;
    const operator = typeof obj.operator === "string" ? obj.operator.trim().toLowerCase() : "";
    if (!isConditionOperator(operator)) return "invalid";
    return {
      answerKey: typeof obj.answerKey === "string" ? obj.answerKey : "",
      operator,
      expectedValueText: typeof obj.expectedValueText === "string" ? obj.expectedValueText : "",
      expectedValueBoolean: typeof obj.expectedValueBoolean === "boolean" ? obj.expectedValueBoolean : null,
      expectedValueNumber:
        typeof obj.expectedValueNumber === "number" ? String(obj.expectedValueNumber) : "",
    };
  } catch {
    return "invalid";
  }
}

export function serializeCondition(c: ParsedCondition): string {
  if (!c.answerKey.trim()) return "";
  const obj: Record<string, unknown> = {
    answerKey: c.answerKey.trim(),
    operator: c.operator,
  };
  const includeValue = c.operator === "eq" || c.operator === "neq";
  if (includeValue) {
    if (c.expectedValueBoolean !== null) {
      obj.expectedValueBoolean = c.expectedValueBoolean;
    } else if (c.expectedValueText.trim()) {
      obj.expectedValueText = c.expectedValueText;
    } else if (c.expectedValueNumber.trim()) {
      const n = Number(c.expectedValueNumber);
      if (Number.isFinite(n)) obj.expectedValueNumber = n;
    }
  }
  return JSON.stringify(obj);
}

export function isConditionOperator(value: string): value is ConditionOperator {
  return ALL_CONDITION_OPERATORS.includes(value as ConditionOperator);
}

// ─── Action mapping editor ──────────────────────────────────────────────────

export type MappingSource = "static" | "workflow" | "target_person" | "directory_identity" | "answer";

export type MappingEntry =
  | { source: "static"; value: string }
  | { source: "workflow"; property: string }
  | { source: "target_person"; property: string }
  | { source: "directory_identity"; property: string }
  | { source: "answer"; answerKey: string };

export function parseMapping(text: string): Record<string, unknown> | null {
  if (!text.trim()) return {};
  try {
    const parsed: unknown = JSON.parse(text);
    if (parsed && typeof parsed === "object" && !Array.isArray(parsed)) {
      return parsed as Record<string, unknown>;
    }
    return null;
  } catch {
    return null;
  }
}

export function stringifyMapping(mapping: Record<string, unknown>): string {
  if (Object.keys(mapping).length === 0) return "";
  return JSON.stringify(mapping, null, 2);
}

export function readEntry(mapping: Record<string, unknown>, paramKey: string): MappingEntry | null {
  const raw = mapping[paramKey];
  if (raw === undefined) return null;
  if (raw && typeof raw === "object" && !Array.isArray(raw) && "source" in raw) {
    const obj = raw as Record<string, unknown>;
    const source = String(obj.source ?? "");
    switch (source) {
      case "static":
        return { source: "static", value: typeof obj.value === "string" ? obj.value : obj.value != null ? String(obj.value) : "" };
      case "workflow":
        return { source: "workflow", property: typeof obj.property === "string" ? obj.property : "" };
      case "target_person":
      case "person":
        return { source: "target_person", property: typeof obj.property === "string" ? obj.property : "" };
      case "directory_identity":
        return { source: "directory_identity", property: typeof obj.property === "string" ? obj.property : "" };
      case "answer":
        return { source: "answer", answerKey: typeof obj.answerKey === "string" ? obj.answerKey : "" };
      default:
        return null;
    }
  }
  // raw scalar → treat as static
  return { source: "static", value: typeof raw === "string" ? raw : String(raw) };
}

export function entryToJson(entry: MappingEntry): Record<string, unknown> {
  switch (entry.source) {
    case "static":
      return { source: "static", value: entry.value };
    case "workflow":
      return { source: "workflow", property: entry.property };
    case "target_person":
      return { source: "target_person", property: entry.property };
    case "directory_identity":
      return { source: "directory_identity", property: entry.property };
    case "answer":
      return { source: "answer", answerKey: entry.answerKey };
  }
}
