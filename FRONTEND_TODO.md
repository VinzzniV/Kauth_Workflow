# FRONTEND_TODO.md

## Purpose

This file is the active frontend working contract.
Use it for frontend guardrails, review criteria and still-open UI work.
It is not the main architecture file and not the full product backlog.

---

## Read Order For Frontend Work

Before non-trivial frontend changes, read:
1. `DOCS_CONTROL.md`
2. `PROJECT_CONTEXT.md`
3. `FRONTEND_TODO.md`
4. `web/README.md`
5. `MEMORY.md`

---

## Frontend Goal

The UI should feel:
- clear
- calm
- task-oriented
- professional

The product is a workflow application, not a marketing page and not a dashboard toy.

---

## Core UI Rules

- Action over explanation: users should quickly see what they can do next.
- Reduce noise: remove text, panels and badges that do not help a decision.
- Keep one clear focus per screen or section.
- Preserve consistent interaction patterns across pages.
- Prefer simplification over adding compensating UI.

---

## Forbidden

- decorative panels without function
- explanatory text for obvious controls or states
- duplicated information across the same page
- inconsistent card patterns for similar content
- "visual improvement" changes without structural benefit
- nested cards unless there is a very strong existing pattern that already depends on it

---

## Allowed

- removing UI elements
- simplifying layouts
- merging redundant sections
- unifying components and wording
- making hierarchy and actions easier to scan

---

## Layout And Component Guardrails

- Prefer simple section-based layouts over box stacks.
- Keep visual hierarchy obvious: page title, key action, supporting content.
- Card usage should stay intentional:
  - primary card for the main action or decision
  - list card for compact repeated entries
  - stat card for short metric display
- Long prose blocks are usually a smell.

---

## Validation Checklist

After frontend changes, check:
- Is the next action obvious?
- Did the change remove noise instead of adding it?
- Are labels and interactions consistent with neighboring screens?
- Is there any duplicated information left on the page?
- Does the layout still work on desktop and mobile?

---

## Open Frontend Work

- No frontend-specific open item is tracked here right now.
- Add new entries only when they are genuinely still open and frontend-specific.
- Cross-cutting release work belongs in `TODO.md`.
