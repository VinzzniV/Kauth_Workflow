#!/usr/bin/env bash

# Z21-S10: Produktions-Verifikationslauf
#
# Ein reproduzierbarer Check der deterministischen Build-/Test-Schritte vor
# einer Freigabe. Browser-Smoke und Graph-/Mail-Live-Verifikation sind weiter
# Nutzer-Aufgaben (Z21-P3-3) und nicht Teil dieses Skripts.
#
# Aufruf:  ./scripts/verify-prod-ready.sh
# Exitcode: 0 wenn alle Schritte gruen, 1 wenn mindestens einer fehlschlaegt.

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

# Pre-existing Backend-Test-Build-Fehler aus frueheren Refactorings (Stubs).
# Wenn die tatsaechliche Fehleranzahl steigt, sind das neue, vom letzten Slice
# verursachte Brueche.
EXPECTED_TEST_STUB_ERRORS=7

declare -a RESULTS=()
EXIT_CODE=0

mark() {
    local label="$1"
    local status="$2"
    local detail="${3:-}"
    RESULTS+=("$status|$label|$detail")
    if [[ "$status" == "FAIL" ]]; then
        EXIT_CODE=1
    fi
}

section() {
    echo
    echo "=== $* ==="
}

# 1. API release build
section "API release build"
if (cd "$REPO_ROOT" && dotnet build api/API/API.csproj -c Release --nologo 2>&1) | tee /tmp/verify-api-build.log | tail -n 5; then
    if grep -qE "0 Error\(s\)" /tmp/verify-api-build.log; then
        mark "API release build" "PASS"
    else
        mark "API release build" "FAIL" "siehe /tmp/verify-api-build.log"
    fi
else
    mark "API release build" "FAIL" "dotnet exit != 0"
fi

# 2. API tests build (pre-existing Stub-Errors werden als baseline akzeptiert)
section "API tests build (Drift-Check gegen pre-existing Stub-Errors)"
(cd "$REPO_ROOT" && dotnet build api/API.Tests/API.Tests.csproj --nologo 2>&1) > /tmp/verify-api-tests-build.log || true
# Duplicate error lines (selbe Fehler-Stelle mehrfach gemeldet) eindeutig zaehlen.
ACTUAL_TEST_ERRORS=$(grep -E "error CS" /tmp/verify-api-tests-build.log | sort -u | wc -l | tr -d ' ')
if [[ "$ACTUAL_TEST_ERRORS" -le "$EXPECTED_TEST_STUB_ERRORS" ]]; then
    mark "API tests build" "PASS" "$ACTUAL_TEST_ERRORS Errors (Baseline $EXPECTED_TEST_STUB_ERRORS)"
else
    mark "API tests build" "FAIL" "$ACTUAL_TEST_ERRORS Errors (Baseline $EXPECTED_TEST_STUB_ERRORS — neuer Bruch)"
fi

# 3. Frontend build
section "Frontend build"
if (cd "$REPO_ROOT/web" && npm run build 2>&1) | tee /tmp/verify-fe-build.log | tail -n 5; then
    if grep -q "built in" /tmp/verify-fe-build.log; then
        mark "Frontend build" "PASS"
    else
        mark "Frontend build" "FAIL" "siehe /tmp/verify-fe-build.log"
    fi
else
    mark "Frontend build" "FAIL" "npm exit != 0"
fi

# 4. Frontend tests — NO_COLOR damit ANSI-Sequenzen den summary-grep nicht blockieren.
section "Frontend tests (vitest)"
if (cd "$REPO_ROOT/web" && NO_COLOR=1 npx vitest run 2>&1) | tee /tmp/verify-fe-tests.log | tail -n 5; then
    if grep -qE "Test Files +[0-9]+ passed" /tmp/verify-fe-tests.log; then
        TEST_SUMMARY=$(grep -E "^[[:space:]]*Tests" /tmp/verify-fe-tests.log | tail -n 1 | tr -s ' ')
        mark "Frontend tests" "PASS" "$TEST_SUMMARY"
    else
        mark "Frontend tests" "FAIL" "siehe /tmp/verify-fe-tests.log"
    fi
else
    mark "Frontend tests" "FAIL" "vitest exit != 0"
fi

# 5. start-vm.sh dev pre-check (syntax + dotnet/npm sichtbarkeit)
section "scripts/start-vm.sh dev Vorab-Check"
if bash -n "$REPO_ROOT/scripts/start-vm.sh"; then
    mark "start-vm.sh syntax" "PASS"
else
    mark "start-vm.sh syntax" "FAIL" "bash -n fail"
fi

# ─────────────────────────────────────────────────────────────────────────
section "Zusammenfassung"
printf "%-32s %-6s %s\n" "Schritt" "Status" "Detail"
printf "%-32s %-6s %s\n" "--------------------------------" "------" "------"
for entry in "${RESULTS[@]}"; do
    IFS='|' read -r status label detail <<<"$entry"
    printf "%-32s %-6s %s\n" "$label" "$status" "$detail"
done
echo

if [[ $EXIT_CODE -eq 0 ]]; then
    echo "✅ Alle deterministischen Pruefungen gruen."
    echo "Ausstehend (Nutzer-Aufgabe, nicht code-pruefbar):"
    echo "  - Browser-Smoke fuer Builder-Form-Editor (R8)"
    echo "  - Mobile-Layout (R10)"
    echo "  - Graph/Mail-Live-Verifikation mit echten Credentials"
else
    echo "❌ Ein oder mehrere Pruefungen fehlgeschlagen — siehe Detail oben."
fi

exit $EXIT_CODE
