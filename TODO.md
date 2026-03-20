# TODO.md

## 🔴 MUST FIX BEFORE DEMO / HANDOFF

### [D1] Demo-Seed nutzt weiterhin reale Notification-Mailadresse
- Problem:
  In `db/02_seed.sql` verwenden alle Demo-Benutzer weiterhin dieselbe echte `notification_email`.

- Warum kritisch:
  Sobald Mailversand aktiviert oder getestet wird, können Demo-Aktionen reale E-Mails verschicken.

- Evidence:
  `db/02_seed.sql`
  - `notification_email = 'vinzent.niederwieser@kauth.de'`

- Fix direction:
  Alle Demo-Benutzer auf sichere Demo-Adressen umstellen, z. B. `user@demo.local`.

- Status:
  Okay for now, but if going Prod then change this

---

## 🧪 FUTURE / IDEAS

### [F1] Add minimal frontend smoke tests
Workflow list, supervisor step, my tasks visibility.

### [F2] Continue reducing presentation-side hardcoding
Remaining status/task display derivations in frontend utilities.

### [F3] Improve demo/dev/prod separation
Seeds, local env files, archive hygiene, config boundaries.

### [F4] Move toward a cleaner migration strategy
Langfristige DB-Evolution jenseits sequenzieller SQL-Resets/Backfills.
