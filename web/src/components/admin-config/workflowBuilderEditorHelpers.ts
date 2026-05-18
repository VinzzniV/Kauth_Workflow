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

const CONDITION_OPERATOR_SHORT: Record<ConditionOperator, string> = {
  is_true: "= ja",
  is_false: "= nein",
  is_not_null: "ist gepflegt",
  is_null: "ist leer",
  eq: "=",
  neq: "≠",
};

function summarizeSingleCondition(parsed: ParsedCondition): string {
  if (!parsed.answerKey) return "Keine Bedingung";
  const opLabel = CONDITION_OPERATOR_SHORT[parsed.operator];
  const valueLabel = (() => {
    if (parsed.operator === "eq" || parsed.operator === "neq") {
      if (parsed.expectedValueBoolean !== null) {
        return parsed.expectedValueBoolean ? "ja" : "nein";
      }
      if (parsed.expectedValueText.trim()) return `"${parsed.expectedValueText}"`;
      if (parsed.expectedValueNumber.trim()) return parsed.expectedValueNumber;
      return "?";
    }
    return null;
  })();
  return valueLabel
    ? `${parsed.answerKey} ${opLabel} ${valueLabel}`
    : `${parsed.answerKey} ${opLabel}`;
}

export function summarizeCondition(text: string): string {
  const expr = parseConditionExpression(text);
  if (expr === "invalid") return "Ungültiger Ausdruck";
  if (expr.conditions.length === 0) return "Keine Bedingung";
  if (expr.conditions.length === 1) return summarizeSingleCondition(expr.conditions[0]!);
  const joiner = expr.logic === "OR" ? " ODER " : " UND ";
  return expr.conditions.map(summarizeSingleCondition).join(joiner);
}

// ─── Decision condition expression (AND/OR-Mehrbedingungen, Z21-S6b) ────────

export type DecisionConditionLogic = "AND" | "OR";

export type ParsedDecisionExpression = {
  logic: DecisionConditionLogic;
  conditions: ParsedCondition[];
};

function emptyParsedCondition(): ParsedCondition {
  return {
    answerKey: "",
    operator: "is_true",
    expectedValueText: "",
    expectedValueBoolean: null,
    expectedValueNumber: "",
  };
}

// Akzeptiert Single-Form ({answerKey, operator, ...}) und Multi-Form
// ({logic, conditions: [...]}). Single-Form wird zu einer 1-Element-Liste
// mit Logic="AND" gehoben. Backend-Parser akzeptiert beide Formen.
export function parseConditionExpression(text: string): ParsedDecisionExpression | "invalid" {
  if (!text.trim()) {
    return { logic: "AND", conditions: [emptyParsedCondition()] };
  }
  try {
    const parsed: unknown = JSON.parse(text);
    if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) return "invalid";
    const obj = parsed as Record<string, unknown>;

    if (Array.isArray(obj.conditions)) {
      const rawLogic = typeof obj.logic === "string" ? obj.logic.trim().toUpperCase() : "AND";
      if (rawLogic !== "AND" && rawLogic !== "OR") return "invalid";
      const conditions: ParsedCondition[] = [];
      for (const entry of obj.conditions) {
        const single = parseConditionObject(entry);
        if (single === null) return "invalid";
        conditions.push(single);
      }
      if (conditions.length === 0) return "invalid";
      return { logic: rawLogic, conditions };
    }

    const single = parseConditionObject(obj);
    if (single === null) return "invalid";
    return { logic: "AND", conditions: [single] };
  } catch {
    return "invalid";
  }
}

function parseConditionObject(value: unknown): ParsedCondition | null {
  if (!value || typeof value !== "object" || Array.isArray(value)) return null;
  const obj = value as Record<string, unknown>;
  const operator = typeof obj.operator === "string" ? obj.operator.trim().toLowerCase() : "";
  if (!isConditionOperator(operator)) return null;
  return {
    answerKey: typeof obj.answerKey === "string" ? obj.answerKey : "",
    operator,
    expectedValueText: typeof obj.expectedValueText === "string" ? obj.expectedValueText : "",
    expectedValueBoolean: typeof obj.expectedValueBoolean === "boolean" ? obj.expectedValueBoolean : null,
    expectedValueNumber:
      typeof obj.expectedValueNumber === "number" ? String(obj.expectedValueNumber) : "",
  };
}

// Schreibt Multi-Form nur, wenn mehrere Bedingungen vorliegen oder Logic="OR".
// Sonst kompakte Single-Form fuer maximale Backwards-Kompat im DB-JSON.
export function serializeConditionExpression(expr: ParsedDecisionExpression): string {
  const filled = expr.conditions.filter((c) => c.answerKey.trim());
  if (filled.length === 0) return "";
  if (filled.length === 1 && expr.logic === "AND") {
    return serializeCondition(filled[0]!);
  }
  const conditions = filled.map((c) => JSON.parse(serializeCondition(c)));
  return JSON.stringify({ logic: expr.logic, conditions });
}

// ─── Action mapping editor ──────────────────────────────────────────────────

// Slice 5 (Admin-Gated-Automation, Referenzuser-Mapping): neue Source `reference_user`.
// Im Gegensatz zu den anderen Sources traegt sie sowohl `property` (heute nur "groups")
// ALS AUCH `answerKey` (Verweis auf eine person_lookup-Form-Antwort).
export type MappingSource = "static" | "workflow" | "target_person" | "directory_identity" | "answer" | "reference_user";

export type MappingEntry =
  | { source: "static"; value: string }
  | { source: "workflow"; property: string }
  | { source: "target_person"; property: string }
  | { source: "directory_identity"; property: string }
  | { source: "answer"; answerKey: string }
  | { source: "reference_user"; property: string; answerKey: string };

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
      case "reference_user":
        return {
          source: "reference_user",
          property: typeof obj.property === "string" ? obj.property : "",
          answerKey: typeof obj.answerKey === "string" ? obj.answerKey : "",
        };
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
    case "reference_user":
      return { source: "reference_user", property: entry.property, answerKey: entry.answerKey };
  }
}
