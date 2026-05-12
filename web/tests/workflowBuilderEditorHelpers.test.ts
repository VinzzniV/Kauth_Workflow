import { describe, expect, it } from "vitest";
import {
  entryToJson,
  parseCondition,
  parseConfig,
  parseMapping,
  readEntry,
  serializeCondition,
  stringifyMapping,
  writeString,
} from "../src/components/admin-config/workflowBuilderEditorHelpers";

// ── WorkflowBuilderConditionEditor: parse/serialize ─────────────────────────

describe("conditionEditor parseCondition / serializeCondition", () => {
  it("returns empty defaults for empty input", () => {
    const parsed = parseCondition("");
    expect(parsed).not.toBe("invalid");
    if (parsed === "invalid") return;
    expect(parsed.answerKey).toBe("");
    expect(parsed.operator).toBe("is_true");
  });

  it("parses a valid is_true condition", () => {
    const parsed = parseCondition('{"answerKey":"hardwareTakeover","operator":"is_true"}');
    expect(parsed).not.toBe("invalid");
    if (parsed === "invalid") return;
    expect(parsed.answerKey).toBe("hardwareTakeover");
    expect(parsed.operator).toBe("is_true");
    expect(parsed.expectedValueText).toBe("");
  });

  it("parses an eq condition with text value", () => {
    const parsed = parseCondition('{"answerKey":"role","operator":"eq","expectedValueText":"manager"}');
    expect(parsed).not.toBe("invalid");
    if (parsed === "invalid") return;
    expect(parsed.operator).toBe("eq");
    expect(parsed.expectedValueText).toBe("manager");
  });

  it("parses an eq condition with boolean value", () => {
    const parsed = parseCondition('{"answerKey":"flag","operator":"eq","expectedValueBoolean":true}');
    expect(parsed).not.toBe("invalid");
    if (parsed === "invalid") return;
    expect(parsed.expectedValueBoolean).toBe(true);
  });

  it("treats unknown operator as invalid", () => {
    expect(parseCondition('{"answerKey":"x","operator":"matches"}')).toBe("invalid");
  });

  it("treats non-object root as invalid", () => {
    expect(parseCondition('"hardwareTakeover == true"')).toBe("invalid");
    expect(parseCondition("[]")).toBe("invalid");
  });

  it("treats malformed JSON as invalid", () => {
    expect(parseCondition("hardwareTakeover == true")).toBe("invalid");
    expect(parseCondition("{not-json}")).toBe("invalid");
  });

  it("serializes empty answerKey to empty string", () => {
    const result = serializeCondition({
      answerKey: "",
      operator: "is_true",
      expectedValueText: "",
      expectedValueBoolean: null,
      expectedValueNumber: "",
    });
    expect(result).toBe("");
  });

  it("serializes is_true without expected value", () => {
    const result = serializeCondition({
      answerKey: "flag",
      operator: "is_true",
      expectedValueText: "",
      expectedValueBoolean: null,
      expectedValueNumber: "",
    });
    const parsed = JSON.parse(result);
    expect(parsed).toEqual({ answerKey: "flag", operator: "is_true" });
  });

  it("serializes eq with text value, omits boolean/number", () => {
    const result = serializeCondition({
      answerKey: "role",
      operator: "eq",
      expectedValueText: "manager",
      expectedValueBoolean: null,
      expectedValueNumber: "",
    });
    const parsed = JSON.parse(result);
    expect(parsed).toEqual({ answerKey: "role", operator: "eq", expectedValueText: "manager" });
  });

  it("serializes eq with boolean prefers boolean over text", () => {
    const result = serializeCondition({
      answerKey: "flag",
      operator: "eq",
      expectedValueText: "ignored",
      expectedValueBoolean: true,
      expectedValueNumber: "",
    });
    const parsed = JSON.parse(result);
    expect(parsed).toEqual({ answerKey: "flag", operator: "eq", expectedValueBoolean: true });
  });

  it("roundtrip: parse → serialize → parse keeps essence", () => {
    const original = '{"answerKey":"role","operator":"neq","expectedValueText":"intern"}';
    const parsed = parseCondition(original);
    expect(parsed).not.toBe("invalid");
    if (parsed === "invalid") return;
    const serialized = serializeCondition(parsed);
    const reparsed = parseCondition(serialized);
    expect(reparsed).not.toBe("invalid");
    if (reparsed === "invalid") return;
    expect(reparsed.answerKey).toBe("role");
    expect(reparsed.operator).toBe("neq");
    expect(reparsed.expectedValueText).toBe("intern");
  });
});

// ── WorkflowBuilderActionMappingEditor: parse/stringify/readEntry ──────────

describe("mappingEditor parseMapping / stringifyMapping / readEntry / entryToJson", () => {
  it("parseMapping returns empty object for empty string", () => {
    expect(parseMapping("")).toEqual({});
  });

  it("parseMapping returns null for invalid JSON", () => {
    expect(parseMapping("not-json")).toBeNull();
    expect(parseMapping("[1,2]")).toBeNull();
  });

  it("stringifyMapping returns empty for empty object", () => {
    expect(stringifyMapping({})).toBe("");
  });

  it("readEntry recognises workflow source", () => {
    const entry = readEntry({ userId: { source: "workflow", property: "workflowUid" } }, "userId");
    expect(entry).toEqual({ source: "workflow", property: "workflowUid" });
  });

  it("readEntry recognises answer source", () => {
    const entry = readEntry({ q: { source: "answer", answerKey: "myKey" } }, "q");
    expect(entry).toEqual({ source: "answer", answerKey: "myKey" });
  });

  it("readEntry maps `person` source to target_person", () => {
    const entry = readEntry({ p: { source: "person", property: "appUserId" } }, "p");
    expect(entry).toEqual({ source: "target_person", property: "appUserId" });
  });

  it("readEntry treats raw scalar as static value", () => {
    const entry = readEntry({ name: "Alice" }, "name");
    expect(entry).toEqual({ source: "static", value: "Alice" });
  });

  it("readEntry returns null for missing key", () => {
    expect(readEntry({}, "missing")).toBeNull();
  });

  it("entryToJson roundtrip for each source type", () => {
    expect(entryToJson({ source: "static", value: "x" })).toEqual({ source: "static", value: "x" });
    expect(entryToJson({ source: "workflow", property: "p" })).toEqual({ source: "workflow", property: "p" });
    expect(entryToJson({ source: "target_person", property: "p" })).toEqual({ source: "target_person", property: "p" });
    expect(entryToJson({ source: "directory_identity", property: "p" })).toEqual({ source: "directory_identity", property: "p" });
    expect(entryToJson({ source: "answer", answerKey: "k" })).toEqual({ source: "answer", answerKey: "k" });
  });

  it("full roundtrip: parse → readEntry → entryToJson → stringify → parse", () => {
    const original = '{"userId":{"source":"workflow","property":"workflowUid"},"name":{"source":"static","value":"Bob"}}';
    const mapping = parseMapping(original);
    expect(mapping).not.toBeNull();
    if (mapping === null) return;
    const userIdEntry = readEntry(mapping, "userId");
    const nameEntry = readEntry(mapping, "name");
    expect(userIdEntry).not.toBeNull();
    expect(nameEntry).not.toBeNull();

    const rebuilt = stringifyMapping({
      userId: entryToJson(userIdEntry!),
      name: entryToJson(nameEntry!),
    });
    const reparsed = parseMapping(rebuilt);
    expect(reparsed).toEqual({
      userId: { source: "workflow", property: "workflowUid" },
      name: { source: "static", value: "Bob" },
    });
  });
});

// ── WorkflowBuilderStepConfigEditor: parse/write ────────────────────────────

describe("stepConfigEditor parseConfig / writeString", () => {
  it("parseConfig returns empty object for empty input", () => {
    expect(parseConfig("")).toEqual({});
  });

  it("parseConfig returns null for invalid JSON", () => {
    expect(parseConfig("not-json")).toBeNull();
    expect(parseConfig("[]")).toBeNull();
  });

  it("writeString sets a value on empty config", () => {
    const result = writeString("", "responsibilityKey", "owner_a");
    expect(JSON.parse(result)).toEqual({ responsibilityKey: "owner_a" });
  });

  it("writeString preserves existing fields", () => {
    const initial = JSON.stringify({ responsibilityKey: "owner_a" });
    const result = writeString(initial, "legacyTemplateKey", "tpl_x");
    expect(JSON.parse(result)).toEqual({ responsibilityKey: "owner_a", legacyTemplateKey: "tpl_x" });
  });

  it("writeString deletes key when value is empty", () => {
    const initial = JSON.stringify({ responsibilityKey: "owner_a", legacyTemplateKey: "tpl_x" });
    const result = writeString(initial, "legacyTemplateKey", "");
    expect(JSON.parse(result)).toEqual({ responsibilityKey: "owner_a" });
  });

  it("writeString returns empty string when last key removed", () => {
    const initial = JSON.stringify({ responsibilityKey: "owner_a" });
    const result = writeString(initial, "responsibilityKey", "");
    expect(result).toBe("");
  });
});

// ── parseConditionExpression / serializeConditionExpression / summarizeCondition Multi ──

import {
  parseConditionExpression,
  serializeConditionExpression,
  summarizeCondition,
  type ParsedDecisionExpression,
} from "../src/components/admin-config/workflowBuilderEditorHelpers";

describe("conditionEditor multi-form (Z21-S6b)", () => {
  it("parses single-form as one-condition AND expression", () => {
    const parsed = parseConditionExpression('{"answerKey":"a","operator":"is_true"}');
    expect(parsed).not.toBe("invalid");
    if (parsed === "invalid") return;
    expect(parsed.logic).toBe("AND");
    expect(parsed.conditions).toHaveLength(1);
  });

  it("parses multi-form with logic AND", () => {
    const parsed = parseConditionExpression('{"logic":"AND","conditions":[{"answerKey":"a","operator":"is_true"},{"answerKey":"b","operator":"is_false"}]}');
    expect(parsed).not.toBe("invalid");
    if (parsed === "invalid") return;
    expect(parsed.logic).toBe("AND");
    expect(parsed.conditions).toHaveLength(2);
  });

  it("parses multi-form with logic OR", () => {
    const parsed = parseConditionExpression('{"logic":"OR","conditions":[{"answerKey":"a","operator":"is_true"}]}');
    expect(parsed).not.toBe("invalid");
    if (parsed === "invalid") return;
    expect(parsed.logic).toBe("OR");
  });

  it("rejects multi-form with empty conditions array", () => {
    expect(parseConditionExpression('{"logic":"AND","conditions":[]}')).toBe("invalid");
  });

  it("rejects multi-form with unknown logic", () => {
    expect(parseConditionExpression('{"logic":"XOR","conditions":[{"answerKey":"a","operator":"is_true"}]}')).toBe("invalid");
  });

  it("serialize keeps single-form when only one condition + AND", () => {
    const expr: ParsedDecisionExpression = {
      logic: "AND",
      conditions: [
        { answerKey: "a", operator: "is_true", expectedValueText: "", expectedValueBoolean: null, expectedValueNumber: "" },
      ],
    };
    const json = serializeConditionExpression(expr);
    const parsed = JSON.parse(json);
    expect(parsed.conditions).toBeUndefined();
    expect(parsed.answerKey).toBe("a");
  });

  it("serialize emits multi-form when two filled conditions", () => {
    const expr: ParsedDecisionExpression = {
      logic: "OR",
      conditions: [
        { answerKey: "a", operator: "is_true", expectedValueText: "", expectedValueBoolean: null, expectedValueNumber: "" },
        { answerKey: "b", operator: "is_false", expectedValueText: "", expectedValueBoolean: null, expectedValueNumber: "" },
      ],
    };
    const json = serializeConditionExpression(expr);
    const parsed = JSON.parse(json);
    expect(parsed.logic).toBe("OR");
    expect(parsed.conditions).toHaveLength(2);
  });

  it("serialize filters empty conditions", () => {
    const expr: ParsedDecisionExpression = {
      logic: "AND",
      conditions: [
        { answerKey: "a", operator: "is_true", expectedValueText: "", expectedValueBoolean: null, expectedValueNumber: "" },
        { answerKey: "", operator: "is_true", expectedValueText: "", expectedValueBoolean: null, expectedValueNumber: "" },
      ],
    };
    const json = serializeConditionExpression(expr);
    const parsed = JSON.parse(json);
    expect(parsed.answerKey).toBe("a");
    expect(parsed.conditions).toBeUndefined();
  });

  it("summarizeCondition joins multi-conditions with UND/ODER", () => {
    const andSummary = summarizeCondition('{"logic":"AND","conditions":[{"answerKey":"a","operator":"is_true"},{"answerKey":"b","operator":"is_false"}]}');
    expect(andSummary).toContain("UND");
    expect(andSummary).toContain("a");
    expect(andSummary).toContain("b");

    const orSummary = summarizeCondition('{"logic":"OR","conditions":[{"answerKey":"a","operator":"is_true"},{"answerKey":"b","operator":"is_false"}]}');
    expect(orSummary).toContain("ODER");
  });

  it("summarizeCondition handles single-form unchanged", () => {
    expect(summarizeCondition('{"answerKey":"a","operator":"is_true"}')).toContain("a");
  });

  it("summarizeCondition shows 'Ungültiger Ausdruck' for broken JSON", () => {
    expect(summarizeCondition("not-json")).toBe("Ungültiger Ausdruck");
  });
});
