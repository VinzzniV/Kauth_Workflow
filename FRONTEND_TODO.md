# FRONTEND_TODO.md

## Working Rules for AI

Before starting any task:
1. Read `PROJECT_CONTEXT.md`, `DECISIONS.md`, `ENGINEERING_RULES.md`
2. Focus on frontend only unless explicitly required otherwise
3. Preserve business behavior
4. Do not move business logic from backend into frontend
5. Prefer simplification over adding more UI
6. Reduce noise before adding new elements
7. Show affected files first
8. Verify changes after implementation

General frontend rule:
- No “marketing text”
- No decorative explanation blocks without real guidance value
- Each page must have one clear primary purpose
- Each panel/card must be one of:
  - action
  - status
  - configuration
  - warning

---

# 🔴 MUST

---

## [DONE] [FE-D1] Redesign Admin information architecture around admin tasks

### Problem
The admin area is currently too dense, too technical, too text-heavy, and too close to the internal data model.

### Goal
Restructure the admin area around admin goals instead of internal structures.

### Required outcome
Admin navigation and landing area must feel like:
- people
- departments / responsibilities
- workflow templates / answers / defaults
- rights / groups / directory
- notification / system
- bulk changes

Instead of vague conceptual groupings such as:
- “high impact”
- “broad impact”
- “recommended start”
- “technical areas”

### Suggested scope
- `web/src/pages/AdminConfigPage.tsx`
- `web/src/components/admin-config/AdminWorkspaceNavigation.tsx`
- `web/src/components/admin-config/AdminOverviewWorkspaceSection.tsx`

### Constraints
- do not redesign the whole app
- do not add more explanation text
- keep routing and section behavior intact unless needed
- focus on structure and language first

### Acceptance criteria
- admin landing page feels task-oriented, not conceptual
- categories are clearer and more professional
- obvious filler wording is removed
- users can understand where to go without reading long text blocks

### Reasoning effort
High

### Best tool
Claude for structure, Codex for implementation

### Implementation plan (ready for Codex)

Apply steps in order — Step 1 first, TypeScript will flag all remaining consumer errors.

**Affected files:**
- `web/src/components/admin-config/adminWorkspaceModel.ts`
- `web/src/components/admin-config/AdminWorkspaceNavigation.tsx`
- `web/src/pages/AdminConfigPage.tsx`
- `web/src/components/admin-config/AdminOverviewWorkspaceSection.tsx`
- `web/src/styles/admin.css`

---

#### Step 1 — `adminWorkspaceModel.ts`

**1a. Shrink `AdminWorkspaceSectionMeta` type** — remove 6 fields:

Remove: `groupDescription`, `audienceLabel`, `audienceDescription`, `cautionLabel`, `cautionDescription`, `impactLabel`

New type:
```ts
export type AdminWorkspaceSectionMeta = {
  key: AdminWorkspaceSection;
  label: string;
  description: string;
  group: AdminWorkspaceSectionGroup;
  groupLabel: string;
};
```

**1b. Replace the entire `ADMIN_WORKSPACE_SECTION_META` array:**

```ts
export const ADMIN_WORKSPACE_SECTION_META: AdminWorkspaceSectionMeta[] = [
  {
    key: "overview",
    label: "Übersicht",
    description: "Gesamtzustand prüfen und offene Punkte angehen.",
    group: "start",
    groupLabel: "Einstieg",
  },
  {
    key: "organization",
    label: "Organisation",
    description: "Personen, Abteilungen und Zuständigkeiten pflegen.",
    group: "start",
    groupLabel: "Einstieg",
  },
  {
    key: "templates",
    label: "Aufgabenvorlagen",
    description: "Aufgabenlogik für neue Vorgänge steuern.",
    group: "configuration",
    groupLabel: "Vorlagen & Felder",
  },
  {
    key: "answers",
    label: "Antwortfelder",
    description: "Eingabefelder und Antwortlogik pflegen.",
    group: "configuration",
    groupLabel: "Vorlagen & Felder",
  },
  {
    key: "defaults",
    label: "Standardwerte",
    description: "Vorauswahlen für Rollen und Bereiche setzen.",
    group: "configuration",
    groupLabel: "Vorlagen & Felder",
  },
  {
    key: "access",
    label: "Zugriffe & Gruppen",
    description: "Rechte, Gruppen und Ausnahmen verwalten.",
    group: "technical",
    groupLabel: "Rechte & Verzeichnis",
  },
  {
    key: "directory",
    label: "Entra-Verzeichnis",
    description: "Entra-Gruppen synchronisieren und Rollen verknüpfen.",
    group: "technical",
    groupLabel: "Rechte & Verzeichnis",
  },
  {
    key: "system",
    label: "Benachrichtigungen & System",
    description: "E-Mail-Versand und Systemeinstellungen konfigurieren.",
    group: "technical",
    groupLabel: "Rechte & Verzeichnis",
  },
  {
    key: "operations",
    label: "Massenänderungen",
    description: "Serienaktionen mit breiter Wirkung ausführen.",
    group: "sensitive",
    groupLabel: "Massenänderungen",
  },
];
```

Note: `AdminWorkspaceSectionGroup` union type stays unchanged (`"start" | "configuration" | "technical" | "sensitive"`).

---

#### Step 2 — `AdminWorkspaceNavigation.tsx`

**2a. Remove the intro paragraph** inside `panel-head`:
```tsx
// REMOVE:
<p>Wählen Sie den Bereich nach Zweck und Wirkung. Fachliche Pflege startet meist in Organisation, technische und breit wirksame Änderungen sind bewusst separat gruppiert.</p>
```
Result:
```tsx
<div className="panel-head">
  <h2>Administrationsbereiche</h2>
</div>
```

**2b. Remove the group description paragraph** inside the group map:
```tsx
// REMOVE:
<p>{groupItems[0].groupDescription}</p>
```
Result:
```tsx
<div className="admin-workspace-group-head">
  <h3>{groupItems[0].groupLabel}</h3>
</div>
```

**2c. Remove the kicker span** from each tab button:
```tsx
// REMOVE:
<span className="admin-workspace-tab-kicker">{item.impactLabel}</span>
```
Each tab button then renders only:
```tsx
<span className="admin-workspace-tab-title">{item.label}</span>
<span className="admin-workspace-tab-note">{item.description}</span>
```

---

#### Step 3 — `AdminConfigPage.tsx`

Replace the entire spotlight panel (the `{!isLoading ? (...) : null}` block at lines ~542–563 that renders `.admin-workspace-spotlight`) with a minimal panel header:

```tsx
// OLD — remove this entire block:
{!isLoading ? (
  <section className={`panel ${section === "operations" ? "panel-caution" : "panel-muted"}`}>
    <div className="admin-workspace-spotlight">
      <div className="admin-workspace-spotlight-main">
        <p className="admin-workspace-spotlight-eyebrow">Aktueller Arbeitsbereich</p>
        <h2>{sectionMeta.label}</h2>
        <p>{sectionMeta.description}</p>
      </div>
      <div className="admin-workspace-spotlight-grid">
        <article className="admin-workspace-spotlight-card">
          <h3>{sectionMeta.audienceLabel}</h3>
          <p>{sectionMeta.audienceDescription}</p>
        </article>
        <article className="admin-workspace-spotlight-card admin-workspace-spotlight-card--caution">
          <h3>{sectionMeta.cautionLabel}</h3>
          <p>{sectionMeta.cautionDescription}</p>
        </article>
      </div>
    </div>
  </section>
) : null}

// NEW — replace with:
{!isLoading ? (
  <section className={`panel ${section === "operations" ? "panel-caution" : "panel-muted"}`}>
    <div className="panel-head">
      <h2>{sectionMeta.label}</h2>
      <p>{sectionMeta.description}</p>
    </div>
  </section>
) : null}
```

The `panel-caution` conditional for `operations` is preserved.

---

#### Step 4 — `AdminOverviewWorkspaceSection.tsx`

Three text-only removals, no structural or prop changes:

**4a.** Remove the description `<p>` from panel "Administrationsübersicht":
```tsx
// REMOVE:
<p>Hier sehen Sie zuerst den Gesamtzustand, priorisieren Warnhinweise und springen dann gezielt in den passenden Bereich.</p>
```

**4b.** In panel "Empfohlene Wege" — rename heading and remove description:
```tsx
// OLD:
<h2>Empfohlene Wege</h2>
<p>Diese Einstiege helfen neuen Admins, erst fachlich sauber zu arbeiten und technische Änderungen bewusst nur bei Bedarf zu öffnen.</p>

// NEW:
<h2>Schnellzugriff</h2>
```

**4c.** Remove the description `<p>` from panel "Zuerst prüfen":
```tsx
// REMOVE:
<p>Leere oder ungültige Zuordnungen werden hier gesammelt, damit neue Admins priorisiert mit echten Lücken starten können.</p>
```

---

#### Step 5 — `admin.css`

**5a. Delete spotlight-only rules entirely** (only used by `AdminConfigPage`, which no longer renders them after Step 3):
```css
/* DELETE — lines 92–120: */
.admin-workspace-spotlight { ... }
.admin-workspace-spotlight-main { ... }
.admin-workspace-spotlight-main h2 { ... }
.admin-workspace-spotlight-main p { ... }
.admin-workspace-spotlight-eyebrow { ... }
```

**5b. Split the 5 comma-selector rules** (lines 122–156) — remove the `admin-workspace-spotlight-*` selector from each, keep only the `admin-guidance-*` half (still used by `AdminBulkOperationsSection`, `AdminDirectorySyncSection`, `AdminTechnicalAccessSection`):

```css
/* Keep only: */
.admin-guidance-grid { ... }
.admin-guidance-card { ... }
.admin-guidance-card--caution { ... }
.admin-guidance-card h3 { ... }
.admin-guidance-card p { ... }
```

**5c. Delete two now-dead rules:**
```css
/* DELETE — <p> inside group-head is removed in Step 2b: */
.admin-workspace-group-head p { ... }

/* DELETE — kicker <span> is removed in Step 2c: */
.admin-workspace-tab-kicker { ... }
```

---

#### Verification

Run `npm run build` — must compile with no TypeScript errors. The removed type fields were only referenced in `AdminWorkspaceNavigation.tsx` (Steps 2b/2c) and `AdminConfigPage.tsx` (Step 3). No other file references them.

---

## [DONE] [FE-D2] Remove weak/filler wording across the frontend

### Problem
Many parts of the frontend still use descriptive text that feels like documentation, marketing, or internal commentary instead of direct UX guidance.

### Goal
Make all user-facing wording shorter, clearer, and more professional.

### Target areas
- page descriptions
- card subtitles
- helper texts
- admin landing texts
- section intros
- button labels
- warning/hint panels

### Rules
- if text only repeats what the UI already shows, remove it
- if text sounds like explanation of the obvious, remove it
- if text sounds motivational or decorative, remove it
- only keep text that reduces user uncertainty

### Suggested scope
Entire frontend, with priority on:
- admin screens
- dashboard
- wizard
- workflow detail
- task screens

### Acceptance criteria
- less text overall
- stronger clarity
- more direct language
- fewer redundant descriptions

### Reasoning effort
Medium

### Best tool
Codex

---

## [DONE] [FE-D3] Simplify admin landing page cards and hierarchy

### Problem
The admin overview currently feels overloaded and over-labelled. Cards contain too much framing language and not enough direct meaning.

### Goal
Make the admin landing page calm, direct, and scannable.

### Required outcome
- fewer card groups
- fewer labels
- stronger hierarchy
- no pseudo-strategic wording
- clearer split between:
  - where to start
  - what is risky
  - what is operational
  - what is system-level

### Suggested scope
- `web/src/components/admin-config/AdminOverviewWorkspaceSection.tsx`
- related admin CSS

### Constraints
- do not add more cards
- do not add more help text
- remove unnecessary wrappers where possible

### Acceptance criteria
- users can scan the admin landing page quickly
- cards look more consistent
- the page no longer feels over-explained

### Reasoning effort
Medium

### Best tool
Codex

---

## [DONE] [FE-D4] Refactor oversized admin workspace components

### Problem
Large admin workspace files are too big to maintain cleanly and are hard to evolve without UX regressions.

### Goal
Split oversized admin components by user task, not by arbitrary code chunks.

### Priority targets
- `web/src/components/admin-config/AdminOrganizationWorkspaceSection.tsx`
- `web/src/pages/AdminConfigPage.tsx`

### Rules
- split by admin task / section responsibility
- no behavior changes
- no large style redesign in same step
- keep props and flow understandable

### Acceptance criteria
- smaller components
- clearer responsibilities
- easier future UX improvements

### Reasoning effort
Medium

### Best tool
Codex

---

# 🟠 SHOULD

---

## [DONE] [FE-S1] Tighten dashboard consistency and reduce remaining noise

### Problem
Dashboard is much better now, but still not fully calm and system-like.

### Goal
Refine the dashboard so it feels more like a professional operational overview.

### Focus
- reduce remaining text noise
- check visual spacing and card consistency
- ensure “next action” dominates clearly
- make stat cards more uniform
- verify no unnecessary heading/subheading clutter remains

### Suggested scope
- `web/src/components/dashboard/DashboardOverview.tsx`
- `web/src/components/dashboard/dashboardInsights.ts`
- related dashboard styles

### Acceptance criteria
- clearer scanning
- less explanation, more action
- more visual consistency between cards/sections

### Reasoning effort
Medium

### Best tool
Codex

---

## [DONE] [FE-S2] Simplify process-type cards in workflow creation

### Problem
The wizard is improved, but process-type cards are still too text-heavy and visually busy.

### Goal
Make process selection faster to scan and easier to decide.

### Required outcome
- process cards should emphasize:
  - process name
  - whether it applies to new or existing person (if needed)
  - one short useful description
- remove or reduce secondary wording
- stronger selected state
- better visual balance across cards

### Suggested scope
- `web/src/pages/CreateWorkflowPage.tsx`
- related workflow styles

### Acceptance criteria
- process cards are easier to compare
- less reading required
- wizard feels lighter

### Reasoning effort
Medium

### Best tool
Codex

---

## [DONE] [FE-S3] Further improve workflow task-area prioritization

### Problem
Task areas are improved, but still need stronger prioritization inside open sections.

### Goal
Make relevant work stand out faster.

### Possible improvements
- stronger visual distinction for current area
- compact rendering for completed tasks
- clearer separation between actionable and informational tasks
- optional sticky or stronger “next action” anchor inside workflow detail

### Suggested scope
- `web/src/components/workflow-detail/WorkflowTaskAreasSection.tsx`
- `web/src/components/workflow-detail/workflowDetailModel.ts`
- related styles

### Acceptance criteria
- less scanning effort inside workflow detail
- easier identification of relevant tasks

### Reasoning effort
Medium

### Best tool
Codex

---

## [DONE] [FE-S4] Standardize page headers across frontend

### Problem
Page headers are too generic and contribute to inconsistent top-of-page rhythm.

### Goal
Make page headers more consistent and more intentional.

### Required outcome
- title usage becomes more consistent
- page description only exists when it adds real value
- spacing and rhythm at page start feel more unified

### Suggested scope
- `web/src/components/layout/PageHeader.tsx`
- all page usages

### Constraints
- do not force every page to have a description
- do not add decorative copy

### Acceptance criteria
- more consistent page openings
- less header noise

### Reasoning effort
Medium

### Best tool
Codex

---

## [DONE] [FE-S5] Review and simplify My Tasks page hierarchy

### Problem
The “Meine Aufgaben” page still contains explanation blocks and filter framing that may be heavier than needed.

### Goal
Make task work feel direct and operational.

### Focus
- reduce explanatory top text
- keep filters compact
- ensure users reach actionable task groups quickly
- avoid duplicate “next action” messaging if obvious from grouping

### Suggested scope
- `web/src/pages/MyTasksPage.tsx`

### Acceptance criteria
- less top-heavy layout
- stronger focus on actual tasks

### Reasoning effort
Low

### Best tool
Codex

---

## [DONE] [FE-S6] Review Workflow List vs Workflow Search separation again

### Problem
Both pages still risk conceptual overlap.

### Goal
Make the difference between browsing and searching clearly visible in structure and wording.

### Suggested scope
- `web/src/pages/WorkflowListPage.tsx`
- `web/src/pages/WorkflowSearchPage.tsx`
- navigation labels

### Acceptance criteria
- clearer difference in user intent
- less duplicated feeling

### Reasoning effort
High

### Best tool
Claude for scoping, Codex for implementation

### Scoping analysis

**Core distinction (functional, invisible to users today):**
- List (`/workflows`): Always loads results, paginated (20/page), has Responsibility filter, has operational callout
- Search (`/search`): Only loads when a filter is active, up to 1000 results, no Responsibility filter, empty by default

**Root causes of overlap:**
1. List page has a free-text input ("Schnellfilter") — looks identical to Search page's "Suche" input
2. List page panel heading is "Arbeitsüberblick" — sounds like a second dashboard, not a filter view
3. Search page panel has `<h2>Globale Suche</h2>` — redundant with the page title "Vorgänge gezielt suchen"
4. The `next-action-callout` on the List page mixes monitoring intent into a browsing/filtering page

**Navigation labels:** Already correct — "Laufende Vorgänge" vs "Vorgänge suchen". No nav changes needed.

### Implementation plan (ready for Codex)

**Affected files:**
- `web/src/pages/WorkflowListPage.tsx`
- `web/src/pages/WorkflowSearchPage.tsx`

No routing, no data fetching, no component logic changes. Wording and structure only.

---

#### Step 1 — `WorkflowListPage.tsx`

**1a. Remove the `next-action-callout` block** (lines ~189–201).

The "next action" pattern belongs to the dashboard. Its presence here makes the List page feel like a secondary dashboard rather than an operational filter view. Remove entirely:
```tsx
// REMOVE this block:
<div className="next-action-callout">
  <p className="next-action-label">Nächste nötige Aktion</p>
  <p className="next-action-text">
    {isReaderOnlyView
      ? "Freigegebene Workflow-Stände verfolgen."
      : "Fälle mit offener Abteilungsleitung oder Fachbereichen zuerst prüfen."}
  </p>
</div>
```

Also remove the `isReaderOnlyView` variable and its `useCurrentUser` import if no longer used after this removal. Check: `isReaderOnlyView` is only used in the callout — so also remove:
```tsx
// REMOVE if no longer referenced:
const isReaderOnlyView =
  capabilities.hasReaderRole && !capabilities.hasProcessActorRole && !capabilities.canManageAdminConfiguration;
```
And remove the `useCurrentUser` import line if `capabilities` is no longer used.

**1b. Rename the filter panel heading:**
```tsx
// OLD:
<h2>Arbeitsüberblick</h2>

// NEW:
<h2>Vorgänge filtern</h2>
```

**1c. Rename the text filter label and update placeholder:**
```tsx
// OLD:
<span>Schnellfilter</span>
...
placeholder="z. B. Name, Stelle oder ID im aktuellen Überblick"

// NEW:
<span>Suche in dieser Ansicht</span>
...
placeholder="Name, Stelle oder Personalnummer"
```

This signals that the text input narrows the current paginated view — not a global lookup.

**1d. Update the cross-link label:**
```tsx
// OLD:
<Link className="btn btn-secondary" to="/search">
  Zur globalen Suche
</Link>

// NEW:
<Link className="btn btn-secondary" to="/search">
  Gezielt suchen
</Link>
```

---

#### Step 2 — `WorkflowSearchPage.tsx`

**2a. Change the PageHeader title:**
```tsx
// OLD:
<PageHeader title="Vorgänge gezielt suchen" />

// NEW:
<PageHeader title="Vorgangssuche" />
```

**2b. Remove the redundant panel `<h2>`:**
```tsx
// OLD:
<div className="panel-head">
  <h2>Globale Suche</h2>
</div>

// REMOVE the entire panel-head div — the page title already establishes context.
```

**2c. Update the `panel-note` to signal the key behavioral difference:**
```tsx
// OLD:
<p className="panel-note">
  Suche nach Name, Personalnummer oder Workflow-ID.
</p>

// NEW:
<p className="panel-note">
  Findet Vorgänge unabhängig vom Status. Suche nach Name, Personalnummer oder Workflow-ID.
</p>
```

"Unabhängig vom Status" is the key phrase: it tells users why they'd come here instead of the List page.

**2d. Update the cross-link label:**
```tsx
// OLD:
<Link className="btn btn-secondary" to="/workflows">
  Zum Arbeitsüberblick
</Link>

// NEW:
<Link className="btn btn-secondary" to="/workflows">
  Zum Überblick
</Link>
```

---

#### Verification

- `npm run build` must pass with no TypeScript errors.
- Check that `isReaderOnlyView` and `useCurrentUser` are fully removed from `WorkflowListPage.tsx` if unused.
- No routing changes, no data changes, no component additions.

---

# 🟡 FUTURE

---

## [DONE] [FE-F1] Build a lightweight frontend UX system / page grammar

### Goal
Define and apply consistent frontend rules for:
- page starts
- card types
- panel types
- warnings
- actions
- descriptions
- empty states

### Why
The app now has enough screens that consistency must become explicit, not implicit.

### Acceptance criteria
- reusable UI grammar exists
- future pages become easier to build consistently

### Reasoning effort
High

### Best tool
Claude for design, Codex for implementation

### Design (by Claude) + Implementation plan (for Codex)

---

#### Design analysis

The app already has a well-structured CSS/component foundation. The grammar is mostly implicit — patterns exist but no single place defines them. The gaps are:

1. **No reference document** — future developers guess when to use `panel` vs `panel-muted`, or when to use `EmptyState` vs a manual panel.
2. **`panel-intro` is a dead class** — CSS is identical to `.panel`. Used in 2 files with no semantic effect. Should be eliminated.
3. **`app-header` is unused** — legacy CSS block, no TSX file uses it. Should be deleted.
4. **`EmptyState.description` is required** — forces redundant text in self-explanatory empty states.
5. **`LoadingState` has a filler default description** — "Bitte warten Sie einen kurzen Moment." is noise.
6. **Three card types exist but are unnamed** — metric card, list item card, selection card. Their rules are implicit.

The grammar lives in a comment block at the top of `components.css`. No new file needed. This is a reference, not prose documentation.

---

#### The grammar (source of truth, write as comment in components.css)

```
 * ═══════════════════════════════════════════════════════════════════
 * PAGE GRAMMAR — reference for building new pages consistently
 * ═══════════════════════════════════════════════════════════════════
 *
 * PAGE SHELL
 *   <main className="app-shell">
 *     <div className="page-container">
 *       <PageHeader title="..." />       ← always present, description optional
 *       ...panels...
 *     </div>
 *   </main>
 *
 * PANELS  (structural sections of a page)
 *   .panel              → action or configuration content (default)
 *   .panel-muted        → secondary, context, or status content
 *   .panel-success / .panel-warning / .panel-caution / .panel-error
 *                       → system feedback only, not structural
 *   Rule: each panel has exactly one purpose: action | status | config | warning
 *   Rule: every panel with a heading uses .panel-head inside
 *
 * FEEDBACK COMPONENTS
 *   <LoadingState title="..." />
 *     → use for any async loading, one per visible operation
 *   <EmptyState title="..." />
 *     → use for empty or error states; description only if title alone is unclear
 *   <EmptyState ... actionLabel onAction />
 *     → use when the error is retryable
 *   Toast (via useToast)
 *     → transient feedback after a user action (save, delete)
 *   .panel-error inline
 *     → persistent error inside an active form
 *
 * CARD TYPES  (inside grids, not standalone sections)
 *   Metric card:    .dashboard-stat-card   label / value / optional chip
 *   List item:      .dashboard-queue-item  title / detail / optional action
 *   Selection card: .process-type-card     interactive choice, name + description
 *
 * BUTTONS
 *   .btn-primary    → primary action, one per section maximum
 *   .btn-secondary  → secondary actions, navigation, toolbar buttons
 *   .btn-ghost      → destructive or low-priority actions
 *
 * FORMS
 *   System A: .field (label wraps input)
 *     → pages, wizard steps, filter toolbars
 *   System B: .form-input / .form-select / .form-textarea
 *     → admin-config components only
 * ═══════════════════════════════════════════════════════════════════
```

---

#### Implementation plan (ready for Codex)

**Affected files:**
- `web/src/styles/components.css`
- `web/src/styles/base.css`
- `web/src/components/feedback/EmptyState.tsx`
- `web/src/components/feedback/LoadingState.tsx`
- `web/src/pages/CreateWorkflowPage.tsx`
- `web/src/components/workflow-detail/WorkflowHeaderPanel.tsx`

Apply in this order.

---

##### Step 1 — `components.css`: add grammar comment block

At the very top of `components.css`, before `@layer components {`, insert the full grammar comment block exactly as written in the design above.

---

##### Step 2 — `components.css`: remove `.panel-intro`

Find and delete this CSS rule (it is identical to `.panel { background: var(--bg-card) }` — it adds nothing):

```css
/* DELETE: */
.panel-intro {
  background: var(--bg-card);
}
```

---

##### Step 3 — `base.css`: remove `.app-header` CSS block

The `.app-header` block (and its children: `.eyebrow`, `.app-header h1`, `.header-description`, `.header-nav`, `.header-controls`, `.header-userbox`, `.header-user-name`, `.header-user-meta`, `.header-logout`) is not used in any TSX file. Delete the entire block.

The comment line `/* ─── Legacy card-like page header container ─── */` goes with it.

---

##### Step 4 — `CreateWorkflowPage.tsx`: replace `panel-intro`

Find the one use of `panel panel-intro` and replace with just `panel`:

```tsx
// OLD:
<section className="panel panel-intro">

// NEW:
<section className="panel">
```

---

##### Step 5 — `WorkflowHeaderPanel.tsx`: replace `panel-intro`

Same change — the one use in this file:

```tsx
// OLD:
<section className="panel panel-intro">

// NEW:
<section className="panel">
```

---

##### Step 6 — `EmptyState.tsx`: make `description` optional

```tsx
// OLD:
type Props = {
  title: string;
  description: string;
  actionLabel?: string;
  onAction?: () => void;
};

export default function EmptyState({ title, description, actionLabel, onAction }: Props) {
  return (
    <section className="panel panel-muted" role="status" aria-live="polite">
      <h3 className="panel-title">{title}</h3>
      <p className="panel-text">{description}</p>
      ...

// NEW:
type Props = {
  title: string;
  description?: string;
  actionLabel?: string;
  onAction?: () => void;
};

export default function EmptyState({ title, description, actionLabel, onAction }: Props) {
  return (
    <section className="panel panel-muted" role="status" aria-live="polite">
      <h3 className="panel-title">{title}</h3>
      {description ? <p className="panel-text">{description}</p> : null}
      ...
```

Do NOT update any call sites — existing descriptions are all meaningful and stay as-is.

---

##### Step 7 — `LoadingState.tsx`: remove filler default description

```tsx
// OLD:
type Props = {
  title?: string;
  description?: string;
};

export default function LoadingState({
  title = "Daten werden geladen...",
  description = "Bitte warten Sie einen kurzen Moment.",
}: Props) {
  return (
    <section className="panel panel-muted" role="status" aria-live="polite">
      <div className="loading-row">
        <span className="loading-dot" />
        <div>
          <h3 className="panel-title">{title}</h3>
          <p className="panel-text">{description}</p>
        </div>
      </div>
    </section>
  );
}

// NEW:
type Props = {
  title?: string;
  description?: string;
};

export default function LoadingState({
  title = "Daten werden geladen...",
  description,
}: Props) {
  return (
    <section className="panel panel-muted" role="status" aria-live="polite">
      <div className="loading-row">
        <span className="loading-dot" />
        <div>
          <h3 className="panel-title">{title}</h3>
          {description ? <p className="panel-text">{description}</p> : null}
        </div>
      </div>
    </section>
  );
}
```

Call sites that explicitly pass a `description` prop are unaffected. Sites that relied on the default filler text will now show no description — which is the correct behavior.

---

##### Verification

Run `npm run build` — must compile with no TypeScript errors.

Check: no file references `.panel-intro` or `.app-header` after the change (grep for both).

No behavior changes expected — all changes are structural cleanup or making optional props optional.

---

## [DONE] [FE-F2] Introduce stronger responsive behavior for dense admin screens

### Goal
Improve responsive handling of complex admin/configuration layouts.

### Focus
- stacked layouts
- filter/tool rows
- section spacing
- card grids on smaller widths

### Reasoning effort
Medium

### Best tool
Codex

---

## [DONE] [FE-F3] Improve accessibility and semantic consistency

### Goal
Strengthen accessibility in:
- headings
- tabs
- navigation
- collapse controls
- status communication
- focus states

### Reasoning effort
Medium

### Best tool
Codex

---

# ✅ DONE CHECKLIST

Before marking a frontend task as done:

- [ ] Only relevant frontend files changed
- [ ] No backend business logic moved into frontend
- [ ] No new filler/marketing text introduced
- [ ] Primary page purpose became clearer
- [ ] Visual noise was reduced, not increased
- [ ] Verification was shown
- [ ] No oversized component became even larger
