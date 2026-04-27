"""
Generiert ein strukturiertes ERD-Dokument (Mermaid) aus 01_schema.sql.

Ausgabe: docs/ERD.md
  - Hochebenen-Uebersicht: alle Tabellen nach Domain gruppiert, FK-Kanten
  - Pro Domain: Detail-ERD mit Spalten und Typen
"""

import re
import sys
from pathlib import Path
from collections import defaultdict

SCHEMA_PATH = Path(__file__).parent.parent / "db" / "01_schema.sql"
OUT_PATH = Path(__file__).parent.parent / "docs" / "ERD.md"
OUT_HTML = Path(__file__).parent.parent / "docs" / "ERD.html"

# ── Domain-Gruppen ─────────────────────────────────────────────────────────────
DOMAINS = {
    "Identity": [
        "people", "app_users", "directory_identities", "directory_groups",
        "directory_group_members", "directory_group_role_mappings",
        "directory_mapping_audit_log", "directory_sync_log",
    ],
    "Auth & Rollen": [
        "app_roles", "app_permissions", "app_responsibilities", "app_groups",
        "app_user_roles", "app_role_permissions", "app_user_groups",
        "app_group_roles", "app_group_responsibilities",
        "app_user_responsibilities", "app_user_permission_overrides",
        "auth_permission_audit_log", "system_responsibilities",
    ],
    "Prozess-Konfiguration": [
        "process_types", "task_templates", "task_template_conditions",
        "task_template_dependencies", "workflow_answer_definitions",
        "workflow_answer_options", "workflow_answer_validation_rules",
        "workflow_answer_visibility_rules", "workflow_answer_reset_rules",
        "workflow_answer_derivation_rules",
        "workflow_answer_single_select_keep_values",
        "app_role_answer_defaults", "app_role_answer_default_options",
    ],
    "Workflow Runtime": [
        "workflows", "workflow_tasks", "workflow_task_comments",
        "workflow_links", "workflow_audit_log", "workflow_answers",
        "workflow_answer_selected_options", "workflow_notifications",
        "workflow_task_dependencies", "workflow_runtime_events",
        "task_assignments",
    ],
    "Workflow Definition": [
        "workflow_definitions", "workflow_definition_versions",
        "workflow_nodes", "workflow_edges", "workflow_node_configs",
        "workflow_node_instances", "workflow_node_actions",
    ],
    "Automation": [
        "action_definitions", "automation_jobs", "automation_job_attempts",
        "automation_job_logs", "department_action_templates",
    ],
    "Rotation": [
        "rotation_plans", "rotation_stations", "rotation_generated_tasks",
        "rotation_task_assignments", "rotation_task_comments",
        "rotation_notifications", "rotation_audit_log",
    ],
    "Admin & Konfiguration": [
        "departments", "department_settings",
        "notification_email_settings", "notification_templates",
    ],
    "System": [
        "system_event_log",
    ],
}

# Kurzbezeichnung fuer den Mermaid-Bezeichner (keine Leerzeichen)
DOMAIN_ID = {k: k.replace(" ", "_").replace("&", "und") for k in DOMAINS}

# ── SQL-Parser ────────────────────────────────────────────────────────────────

def parse_schema(sql: str):
    tables = {}   # table_name -> list of (col_name, col_type, nullable, pk)
    fks = []      # (from_table, from_col, to_table, to_col)

    # Alle CREATE TABLE Bloecke extrahieren
    table_pattern = re.compile(
        r"CREATE TABLE public\.(\w+)\s*\((.*?)\);",
        re.DOTALL,
    )
    for m in table_pattern.finditer(sql):
        tname = m.group(1)
        body = m.group(2)
        cols = []
        for line in body.splitlines():
            line = line.strip().rstrip(",")
            if not line or line.startswith("--"):
                continue
            # Ueberspringe Constraints innerhalb des CREATE TABLE
            if re.match(r"(CONSTRAINT|PRIMARY KEY|UNIQUE|CHECK|FOREIGN KEY)", line, re.I):
                continue
            # Spaltenname + Typ parsen
            col_m = re.match(r"(\w+)\s+(.+)", line)
            if not col_m:
                continue
            col_name = col_m.group(1)
            rest = col_m.group(2)
            nullable = "NOT NULL" not in rest
            is_pk = col_name == "id"
            # Typ bereinigen (entferne DEFAULT, NOT NULL ...)
            # Mehrteilige Typen (character varying, timestamp with time zone) zuerst pruefen
            TYPE_MAP = [
                (r"character varying(?:\(\d+\))?", "varchar"),
                (r"timestamp with time zone", "timestamptz"),
                (r"timestamp without time zone", "timestamp"),
                (r"double precision", "float8"),
                (r"bigint", "bigint"),
                (r"integer", "int"),
                (r"boolean", "bool"),
                (r"text", "text"),
                (r"uuid", "uuid"),
                (r"jsonb?", "jsonb"),
                (r"date", "date"),
                (r"numeric(?:\([^)]*\))?", "numeric"),
                (r"character(?:\(\d+\))?", "char"),
            ]
            col_type = rest.split()[0]  # Fallback
            for pattern, replacement in TYPE_MAP:
                if re.match(pattern, rest, re.I):
                    col_type = replacement
                    break
            cols.append((col_name, col_type, nullable, is_pk))
        tables[tname] = cols

    # ALTER TABLE ... FOREIGN KEY parsen
    fk_block_re = re.compile(
        r"ALTER TABLE ONLY public\.(\w+)\s+ADD CONSTRAINT \S+ FOREIGN KEY \(([^)]+)\)\s+REFERENCES public\.(\w+)\(([^)]+)\)",
        re.DOTALL,
    )
    for m in fk_block_re.finditer(sql):
        from_table = m.group(1)
        from_cols = [c.strip() for c in m.group(2).split(",")]
        to_table = m.group(3)
        to_cols = [c.strip() for c in m.group(4).split(",")]
        # Nur die erste Spalte fuer einfache FKs
        fks.append((from_table, from_cols[0], to_table, to_cols[0]))

    return tables, fks


# ── Mermaid-Helfer ────────────────────────────────────────────────────────────

def table_domain(name: str) -> str:
    for d, tables in DOMAINS.items():
        if name in tables:
            return d
    return "Sonstige"


def overview_diagram(tables: dict, fks: list) -> str:
    lines = ["flowchart LR"]

    # Subgraphen pro Domain
    for domain, members in DOMAINS.items():
        did = DOMAIN_ID[domain]
        present = [t for t in members if t in tables]
        if not present:
            continue
        lines.append(f"  subgraph {did}[\"{domain}\"]")
        for t in present:
            lines.append(f"    {t}")
        lines.append("  end")

    lines.append("")

    # Kanten – nur wenn beide Seiten in bekannten Domains
    all_known = {t for ts in DOMAINS.values() for t in ts}
    added = set()
    for from_t, from_c, to_t, to_c in fks:
        key = (from_t, to_t)
        if key in added:
            continue
        if from_t in all_known and to_t in all_known:
            lines.append(f"  {from_t} --> {to_t}")
            added.add(key)

    return "\n".join(lines)


def detail_diagram(domain: str, members: list, tables: dict, fks: list) -> str:
    present = [t for t in members if t in tables]
    if not present:
        return ""

    lines = ["erDiagram"]

    for tname in present:
        cols = tables[tname]
        lines.append(f"  {tname} {{")
        for col_name, col_type, nullable, is_pk in cols:
            pk_tag = " PK" if is_pk else ""
            null_tag = "" if not nullable else ""
            lines.append(f"    {col_type} {col_name}{pk_tag}")
        lines.append("  }")

    lines.append("")

    # FK-Kanten: nur innerhalb der Domain ODER nach aussen (einmalig)
    present_set = set(present)
    added = set()
    cross_domain_declared = set()
    for from_t, from_c, to_t, to_c in fks:
        if from_t not in present_set:
            continue
        key = (from_t, to_t, from_c)
        if key in added:
            continue
        added.add(key)
        if to_t in present_set:
            lines.append(f"  {from_t} }}o--|| {to_t} : \"{from_c}\"")
        else:
            # Cross-Domain-Referenz: Stub-Entity einmalig deklarieren
            if to_t not in cross_domain_declared:
                lines.append(f"  {to_t} {{")
                lines.append(f"  }}")
                cross_domain_declared.add(to_t)
            lines.append(f"  {from_t} }}o..|| {to_t} : \"{from_c} →\"")

    return "\n".join(lines)


# ── Markdown-Ausgabe ─────────────────────────────────────────────────────────

def build_markdown(tables: dict, fks: list) -> str:
    all_known = {t for ts in DOMAINS.values() for t in ts}
    unknown = set(tables.keys()) - all_known
    if unknown:
        print(f"⚠  Nicht zugeordnete Tabellen: {sorted(unknown)}", file=sys.stderr)

    lines = [
        "# ERD – Kauth Workflow",
        "",
        "> Automatisch generiert aus `db/01_schema.sql`. Nicht manuell bearbeiten.",
        f"> {len(tables)} Tabellen, {len(fks)} Foreign Keys.",
        "",
        "---",
        "",
        "## Übersicht (alle Domains)",
        "",
        "```mermaid",
        overview_diagram(tables, fks),
        "```",
        "",
        "---",
        "",
        "## Detail-ERDs nach Domain",
        "",
    ]

    for domain, members in DOMAINS.items():
        present = [t for t in members if t in tables]
        if not present:
            continue
        lines += [
            f"### {domain}",
            "",
            f"Tabellen: {', '.join(f'`{t}`' for t in present)}",
            "",
            "```mermaid",
            detail_diagram(domain, members, tables, fks),
            "```",
            "",
        ]

    return "\n".join(lines)


# ── Main ──────────────────────────────────────────────────────────────────────

def build_html(tables: dict, fks: list) -> str:
    sections = []

    # Uebersicht
    sections.append(("Übersicht (alle Domains)", overview_diagram(tables, fks)))

    # Detail-ERDs
    for domain, members in DOMAINS.items():
        if not any(t in tables for t in members):
            continue
        diagram = detail_diagram(domain, members, tables, fks)
        sections.append((domain, diagram))

    nav_links = "\n".join(
        f'<a href="#section-{i}">{title}</a>'
        for i, (title, _) in enumerate(sections)
    )

    section_html = ""
    for i, (title, diagram) in enumerate(sections):
        esc = diagram.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")
        section_html += f"""
        <section id="section-{i}">
            <h2>{title}</h2>
            <div class="mermaid">{diagram}</div>
        </section>
        """

    return f"""<!DOCTYPE html>
<html lang="de">
<head>
  <meta charset="UTF-8">
  <title>ERD – Kauth Workflow</title>
  <style>
    body {{ font-family: system-ui, sans-serif; margin: 0; background: #f8f9fa; color: #1a1a2e; }}
    header {{ background: #16213e; color: #e2e8f0; padding: 1.2rem 2rem; position: sticky; top: 0; z-index: 100; display: flex; align-items: center; gap: 2rem; }}
    header h1 {{ margin: 0; font-size: 1.2rem; white-space: nowrap; }}
    nav {{ display: flex; flex-wrap: wrap; gap: 0.5rem; }}
    nav a {{ color: #90cdf4; text-decoration: none; font-size: 0.8rem; padding: 0.2rem 0.5rem; border-radius: 4px; border: 1px solid #2d3748; white-space: nowrap; }}
    nav a:hover {{ background: #2d3748; }}
    main {{ max-width: 100%; padding: 1rem 2rem 4rem; }}
    section {{ margin: 2rem 0; background: white; border-radius: 8px; padding: 1.5rem; box-shadow: 0 1px 4px rgba(0,0,0,.08); overflow-x: auto; }}
    h2 {{ margin-top: 0; font-size: 1.1rem; color: #2d3748; border-bottom: 2px solid #e2e8f0; padding-bottom: 0.5rem; }}
    .mermaid {{ min-height: 60px; }}
    p.meta {{ color: #718096; font-size: 0.85rem; }}
  </style>
</head>
<body>
  <header>
    <h1>ERD – Kauth Workflow</h1>
    <nav>{nav_links}</nav>
  </header>
  <main>
    <p class="meta">Automatisch generiert aus <code>db/01_schema.sql</code> &mdash; {len(tables)} Tabellen, {len(fks)} Foreign Keys.</p>
    {section_html}
  </main>
  <script type="module">
    import mermaid from 'https://cdn.jsdelivr.net/npm/mermaid@11/dist/mermaid.esm.min.mjs';
    mermaid.initialize({{ startOnLoad: true, theme: 'default', er: {{ diagramPadding: 30 }}, flowchart: {{ padding: 20 }} }});
  </script>
</body>
</html>
"""


def main():
    sql = SCHEMA_PATH.read_text(encoding="utf-8")
    tables, fks = parse_schema(sql)
    print(f"Geparst: {len(tables)} Tabellen, {len(fks)} FKs", file=sys.stderr)

    OUT_PATH.parent.mkdir(parents=True, exist_ok=True)

    md = build_markdown(tables, fks)
    OUT_PATH.write_text(md, encoding="utf-8")
    print(f"Markdown: {OUT_PATH}", file=sys.stderr)

    html = build_html(tables, fks)
    OUT_HTML.write_text(html, encoding="utf-8")
    print(f"HTML:     {OUT_HTML}", file=sys.stderr)


if __name__ == "__main__":
    main()
