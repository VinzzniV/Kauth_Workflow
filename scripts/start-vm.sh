#!/usr/bin/env bash

set -euo pipefail

MODE="${1:-}"
ACTION="${2:-start}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
DEV_STATE_DIR="$REPO_ROOT/.tmp-vm-dev"
DEV_API_PID_FILE="$DEV_STATE_DIR/api.pid"
DEV_WEB_PID_FILE="$DEV_STATE_DIR/web.pid"
DEV_API_LOG_FILE="$DEV_STATE_DIR/api.log"
DEV_WEB_LOG_FILE="$DEV_STATE_DIR/web.log"

usage() {
    cat <<'EOF'
Usage:
  ./scripts/start-vm.sh <dev|prod> [start|stop|status|logs|restart]

Examples:
  ./scripts/start-vm.sh dev
  ./scripts/start-vm.sh dev logs
  ./scripts/start-vm.sh prod
  ./scripts/start-vm.sh prod status
EOF
}

fail() {
    echo "ERROR: $*" >&2
    exit 1
}

require_command() {
    local name="$1"
    command -v "$name" >/dev/null 2>&1 || fail "'$name' wurde nicht gefunden."
}

version_ge() {
    local left="$1"
    local right="$2"
    [[ "$(printf '%s\n%s\n' "$right" "$left" | sort -V | tail -n1)" == "$left" ]]
}

ensure_supported_node_version() {
    require_command node

    local raw_version
    raw_version="$(node -v 2>/dev/null || true)"
    [[ -n "$raw_version" ]] || fail "Node-Version konnte nicht gelesen werden."

    local version="${raw_version#v}"
    local major="${version%%.*}"

    if [[ "$major" == "20" ]]; then
        version_ge "$version" "20.19.0" || fail "Node $version ist zu alt. Fuer Vite 7 wird mindestens Node 20.19.0 oder 22.12.0 benoetigt."
        return 0
    fi

    if [[ "$major" == "22" ]]; then
        version_ge "$version" "22.12.0" || fail "Node $version ist zu alt. Fuer Vite 7 wird mindestens Node 20.19.0 oder 22.12.0 benoetigt."
        return 0
    fi

    if [[ "$major" =~ ^[2-9][3-9]$|^[3-9][0-9]+$ ]]; then
        return 0
    fi

    fail "Node $version wird nicht unterstuetzt. Fuer den Dev-Webserver wird Node 20.19.0+ oder 22.12.0+ benoetigt."
}

assert_file() {
    local path="$1"
    [[ -e "$path" ]] || fail "Pfad fehlt: $path"
}

run_in_repo() {
    (
        cd "$REPO_ROOT"
        "$@"
    )
}

ensure_dev_state_dir() {
    mkdir -p "$DEV_STATE_DIR"
}

read_pid_file() {
    local pid_file="$1"
    [[ -f "$pid_file" ]] || return 1

    local pid
    pid="$(tr -d '[:space:]' < "$pid_file")"
    [[ -n "$pid" ]] || return 1

    printf '%s\n' "$pid"
}

is_pid_running() {
    local pid="$1"
    kill -0 "$pid" >/dev/null 2>&1
}

get_running_pid_from_file() {
    local pid_file="$1"
    local pid

    if ! pid="$(read_pid_file "$pid_file")"; then
        return 1
    fi

    if ! is_pid_running "$pid"; then
        rm -f "$pid_file"
        return 1
    fi

    printf '%s\n' "$pid"
}

port_is_listening() {
    local port="$1"

    if command -v ss >/dev/null 2>&1; then
        ss -ltnH | awk '{print $4}' | grep -Eq "(^|:)$port$"
        return $?
    fi

    if command -v netstat >/dev/null 2>&1; then
        netstat -ltn 2>/dev/null | awk 'NR > 2 { print $4 }' | grep -Eq "(^|:)$port$"
        return $?
    fi

    return 1
}

assert_port_free() {
    local port="$1"
    local description="$2"

    if port_is_listening "$port"; then
        fail "$description-Port $port ist bereits belegt."
    fi
}

dev_compose() {
    run_in_repo docker compose -f compose.yml -f compose.dev-db.yml "$@"
}

prod_compose() {
    run_in_repo docker compose --env-file .env.prod -f compose.yml -f compose.prod.yml "$@"
}

get_compose_db_container_id() {
    local container_id
    container_id="$(dev_compose ps -q db | tr -d '[:space:]')"
    printf '%s\n' "$container_id"
}

get_docker_container_state() {
    local container_id="$1"
    docker inspect --format '{{.State.Status}}|{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}' "$container_id"
}

wait_for_dev_database() {
    local timeout_seconds="${1:-90}"
    local deadline
    deadline=$((SECONDS + timeout_seconds))

    while (( SECONDS < deadline )); do
        local container_id
        container_id="$(get_compose_db_container_id)"

        if [[ -n "$container_id" ]]; then
            local state
            state="$(get_docker_container_state "$container_id")"
            local status="${state%%|*}"
            local health="${state##*|}"
            echo "DB-Status: $status, Health: $health"

            if [[ "$status" == "running" && "$health" == "healthy" ]]; then
                return 0
            fi

            if [[ "$status" == "exited" || "$status" == "dead" ]]; then
                fail "DB-Container ist nicht lauffaehig (Status: $status)."
            fi
        fi

        sleep 2
    done

    fail "DB wurde innerhalb von $timeout_seconds Sekunden nicht healthy."
}

ensure_dev_database_ready() {
    require_command docker
    assert_file "$REPO_ROOT/compose.yml"
    assert_file "$REPO_ROOT/compose.dev-db.yml"

    local container_id
    container_id="$(get_compose_db_container_id)"

    if [[ -n "$container_id" ]]; then
        local state
        state="$(get_docker_container_state "$container_id")"
        local status="${state%%|*}"
        local health="${state##*|}"

        if [[ "$status" == "running" && "$health" == "healthy" ]]; then
            echo "Dev-Datenbank laeuft bereits und ist healthy."
            return 0
        fi

        if [[ "$status" == "running" ]]; then
            echo "Dev-Datenbank laeuft bereits, ist aber noch nicht healthy. Warte auf Bereitschaft ..."
            wait_for_dev_database
            return 0
        fi
    fi

    assert_port_free 26432 "Dev-DB"

    echo "Starte Dev-Datenbank ueber Docker Compose ..."
    dev_compose up -d db

    echo "Warte auf DB-Bereitschaft ..."
    wait_for_dev_database
}

ensure_web_dependencies() {
    ensure_supported_node_version

    if [[ -d "$REPO_ROOT/web/node_modules" ]]; then
        return 0
    fi

    echo "web/node_modules fehlt. Fuehre npm ci aus ..."
    (
        cd "$REPO_ROOT/web"
        npm ci
    )
}

resolve_dev_public_base_url() {
    if [[ -n "${DEV_PUBLIC_BASE_URL:-}" ]]; then
        printf '%s\n' "$DEV_PUBLIC_BASE_URL"
        return 0
    fi

    local detected_host=""

    if command -v hostname >/dev/null 2>&1; then
        detected_host="$(hostname -I 2>/dev/null | awk '{print $1}')"
    fi

    if [[ -z "$detected_host" ]]; then
        detected_host="localhost"
    fi

    printf 'http://%s:5173\n' "$detected_host"
}

start_dev_api() {
    local existing_pid
    if existing_pid="$(get_running_pid_from_file "$DEV_API_PID_FILE")"; then
        echo "API laeuft bereits mit PID $existing_pid."
        return 0
    fi

    assert_port_free 5001 "API"

    local dev_public_base_url
    dev_public_base_url="$(resolve_dev_public_base_url)"

    echo "Starte API im Hintergrund ..."
    (
        cd "$REPO_ROOT"
        exec nohup env \
            ASPNETCORE_ENVIRONMENT=Development \
            ASPNETCORE_URLS=http://0.0.0.0:5001 \
            AUTH_MODE=dev-sim \
            ConnectionStrings__Default="Host=localhost;Port=26432;Username=${DEV_POSTGRES_USER:-app};Password=${DEV_POSTGRES_PASSWORD:-app_pw};Database=${DEV_POSTGRES_DB:-appdb};GSS Encryption Mode=Disable;SSL Mode=Disable" \
            PUBLIC_BASE_URL="$dev_public_base_url" \
            Cors__AllowedOrigins__0="$dev_public_base_url" \
            NotificationEmail__FrontendBaseUrl="$dev_public_base_url" \
            DIRECTORY_GROUP_PREFIX="${DIRECTORY_GROUP_PREFIX:-Onboarding-App-}" \
            DIRECTORY_SYNC_SCHEDULED="${DIRECTORY_SYNC_SCHEDULED:-true}" \
            SWAGGER_ENABLED="${SWAGGER_ENABLED:-true}" \
            dotnet run --project api/API/API.csproj
    ) >>"$DEV_API_LOG_FILE" 2>&1 &
    echo "$!" > "$DEV_API_PID_FILE"

    sleep 2
    local started_pid
    if ! started_pid="$(get_running_pid_from_file "$DEV_API_PID_FILE")"; then
        fail "API konnte nicht gestartet werden. Details: $DEV_API_LOG_FILE"
    fi

    echo "API gestartet (PID $started_pid, Log: $DEV_API_LOG_FILE)."
}

start_dev_web() {
    local existing_pid
    if existing_pid="$(get_running_pid_from_file "$DEV_WEB_PID_FILE")"; then
        echo "Web laeuft bereits mit PID $existing_pid."
        return 0
    fi

    assert_port_free 5173 "Web"
    ensure_web_dependencies

    echo "Starte Vite-Webserver im Hintergrund ..."
    (
        cd "$REPO_ROOT/web"
        exec nohup env \
            VITE_API_PROXY_TARGET=http://127.0.0.1:5001 \
            VITE_AUTH_MODE=dev-sim \
            npm run dev -- --host 0.0.0.0
    ) >>"$DEV_WEB_LOG_FILE" 2>&1 &
    echo "$!" > "$DEV_WEB_PID_FILE"

    sleep 2
    local started_pid
    if ! started_pid="$(get_running_pid_from_file "$DEV_WEB_PID_FILE")"; then
        fail "Web konnte nicht gestartet werden. Details: $DEV_WEB_LOG_FILE"
    fi

    echo "Web gestartet (PID $started_pid, Log: $DEV_WEB_LOG_FILE)."
}

stop_dev_process() {
    local pid_file="$1"
    local label="$2"
    local pid

    if ! pid="$(get_running_pid_from_file "$pid_file")"; then
        echo "$label laeuft nicht."
        return 0
    fi

    echo "Stoppe $label (PID $pid) ..."
    kill "$pid" >/dev/null 2>&1 || true

    local deadline
    deadline=$((SECONDS + 15))
    while is_pid_running "$pid" && (( SECONDS < deadline )); do
        sleep 1
    done

    if is_pid_running "$pid"; then
        kill -9 "$pid" >/dev/null 2>&1 || true
    fi

    rm -f "$pid_file"
}

dev_status() {
    echo "Dev-DB:"
    dev_compose ps db
    echo

    if get_running_pid_from_file "$DEV_API_PID_FILE" >/dev/null; then
        echo "API: running (PID $(read_pid_file "$DEV_API_PID_FILE"))"
    else
        echo "API: stopped"
    fi

    if get_running_pid_from_file "$DEV_WEB_PID_FILE" >/dev/null; then
        echo "Web: running (PID $(read_pid_file "$DEV_WEB_PID_FILE"))"
    else
        echo "Web: stopped"
    fi

    echo
    echo "API URL: http://<vm-host>:5001"
    echo "Web URL: $(resolve_dev_public_base_url)"
    echo "Logs: $DEV_API_LOG_FILE | $DEV_WEB_LOG_FILE"
}

dev_logs() {
    ensure_dev_state_dir
    touch "$DEV_API_LOG_FILE" "$DEV_WEB_LOG_FILE"
    tail -n 100 -f "$DEV_API_LOG_FILE" "$DEV_WEB_LOG_FILE"
}

start_dev_environment() {
    require_command docker
    require_command dotnet
    require_command npm
    assert_file "$REPO_ROOT/api/API/API.csproj"
    assert_file "$REPO_ROOT/web/package.json"
    ensure_dev_state_dir
    ensure_dev_database_ready
    start_dev_api
    start_dev_web

    echo
    echo "Dev-Start abgeschlossen."
    echo "API:  http://<vm-host>:5001"
    echo "Web:  $(resolve_dev_public_base_url)"
    echo "DB:   localhost:26432"
    echo "Logs: $DEV_API_LOG_FILE | $DEV_WEB_LOG_FILE"
}

stop_dev_environment() {
    ensure_dev_state_dir
    stop_dev_process "$DEV_WEB_PID_FILE" "Web"
    stop_dev_process "$DEV_API_PID_FILE" "API"
    echo "Stoppe Dev-Datenbank ..."
    dev_compose down
}

start_prod_environment() {
    ensure_prod_prerequisites

    echo "Starte produktionsnahen Compose-Stack ..."
    prod_compose up -d --build

    echo
    echo "Prod-Start abgeschlossen."
    echo "Status: ./scripts/start-vm.sh prod status"
    echo "Logs:   ./scripts/start-vm.sh prod logs"
}

ensure_prod_prerequisites() {
    require_command docker
    assert_file "$REPO_ROOT/compose.yml"
    assert_file "$REPO_ROOT/compose.prod.yml"
    assert_file "$REPO_ROOT/.env.prod"
}

prod_status() {
    ensure_prod_prerequisites
    prod_compose ps
}

prod_logs() {
    ensure_prod_prerequisites
    prod_compose logs -f
}

stop_prod_environment() {
    ensure_prod_prerequisites
    prod_compose down
}

restart_mode() {
    local mode="$1"
    case "$mode" in
        dev)
            stop_dev_environment
            start_dev_environment
            ;;
        prod)
            stop_prod_environment
            start_prod_environment
            ;;
        *)
            fail "Unbekannter Modus fuer restart: $mode"
            ;;
    esac
}

[[ -n "$MODE" ]] || {
    usage
    exit 1
}

case "$MODE" in
    dev|prod) ;;
    help|-h|--help)
        usage
        exit 0
        ;;
    *)
        fail "Ungueltiger Modus '$MODE'. Erlaubt sind: dev, prod."
        ;;
esac

case "$ACTION" in
    start|stop|status|logs|restart) ;;
    *)
        fail "Ungueltige Aktion '$ACTION'. Erlaubt sind: start, stop, status, logs, restart."
        ;;
esac

case "$MODE:$ACTION" in
    dev:start) start_dev_environment ;;
    dev:stop) stop_dev_environment ;;
    dev:status) dev_status ;;
    dev:logs) dev_logs ;;
    dev:restart) restart_mode dev ;;
    prod:start) start_prod_environment ;;
    prod:stop) stop_prod_environment ;;
    prod:status) prod_status ;;
    prod:logs) prod_logs ;;
    prod:restart) restart_mode prod ;;
esac
