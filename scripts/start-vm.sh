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
  ./scripts/start-vm.sh prod logs
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

sha256_file() {
    local path="$1"

    if command -v sha256sum >/dev/null 2>&1; then
        sha256sum "$path" | awk '{print $1}'
        return 0
    fi

    if command -v shasum >/dev/null 2>&1; then
        shasum -a 256 "$path" | awk '{print $1}'
        return 0
    fi

    fail "Weder sha256sum noch shasum ist verfuegbar. package-lock-Pruefung ist nicht moeglich."
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

    if [[ "$major" == "18" ]]; then
        version_ge "$version" "18.19.0" || fail "Node $version ist zu alt. Fuer den Dev-Webserver wird mindestens Node 18.19.0 benoetigt."
        echo "WARNUNG: Node $version liegt unter der offiziell bevorzugten Vite-7-Version. Es wird ein lokaler Kompatibilitaetspfad genutzt."
        return 0
    fi

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

    fail "Node $version wird nicht unterstuetzt. Fuer den Dev-Webserver wird mindestens Node 18.19.0 benoetigt; bevorzugt 20.19.0+ oder 22.12.0+."
}

assert_file() {
    local path="$1"
    [[ -e "$path" ]] || fail "Pfad fehlt: $path"
}

http_ok() {
    local url="$1"

    if command -v curl >/dev/null 2>&1; then
        curl -fsS "$url" >/dev/null 2>&1
        return $?
    fi

    if command -v wget >/dev/null 2>&1; then
        wget -qO- "$url" >/dev/null 2>&1
        return $?
    fi

    fail "Weder curl noch wget ist verfuegbar. HTTP-Healthchecks koennen nicht ausgefuehrt werden."
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

list_child_pids() {
    local pid="$1"

    if command -v pgrep >/dev/null 2>&1; then
        pgrep -P "$pid" 2>/dev/null || true
        return 0
    fi

    if command -v ps >/dev/null 2>&1; then
        ps -o pid= --ppid "$pid" 2>/dev/null | awk '{print $1}' || true
        return 0
    fi

    return 0
}

kill_pid_tree() {
    local pid="$1"
    local signal="${2:-TERM}"
    local child_pid

    for child_pid in $(list_child_pids "$pid"); do
        [[ -n "$child_pid" ]] || continue
        kill_pid_tree "$child_pid" "$signal"
    done

    kill "-$signal" "$pid" >/dev/null 2>&1 || true
}

find_listening_pids_by_port() {
    local port="$1"

    if command -v lsof >/dev/null 2>&1; then
        lsof -tiTCP:"$port" -sTCP:LISTEN 2>/dev/null || true
        return 0
    fi

    if command -v fuser >/dev/null 2>&1; then
        fuser -n tcp "$port" 2>/dev/null | tr ' ' '\n' | awk 'NF' || true
        return 0
    fi

    return 0
}

describe_port_usage() {
    local port="$1"

    if command -v lsof >/dev/null 2>&1; then
        lsof -nP -iTCP:"$port" -sTCP:LISTEN 2>/dev/null || true
        return 0
    fi

    if command -v ss >/dev/null 2>&1; then
        ss -ltnp "( sport = :$port )" 2>/dev/null || true
        return 0
    fi

    return 0
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
        local usage
        usage="$(describe_port_usage "$port")"
        if [[ -n "$usage" ]]; then
            fail "$description-Port $port ist bereits belegt.\n$usage"
        fi

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
    local web_dir="$REPO_ROOT/web"
    local node_modules_dir="$web_dir/node_modules"
    local package_lock_file="$web_dir/package-lock.json"
    local lock_hash_file="$node_modules_dir/.package-lock.sha256"
    local expected_lock_hash
    expected_lock_hash="$(sha256_file "$package_lock_file")"

    if [[ -d "$node_modules_dir" && -f "$lock_hash_file" ]]; then
        local installed_lock_hash
        installed_lock_hash="$(tr -d '[:space:]' < "$lock_hash_file")"
        if [[ "$installed_lock_hash" == "$expected_lock_hash" ]]; then
            return 0
        fi
    fi

    if [[ -d "$node_modules_dir" ]]; then
        echo "web/package-lock.json hat sich geaendert oder node_modules ist nicht verifiziert. Fuehre npm ci aus ..."
    else
        echo "web/node_modules fehlt. Fuehre npm ci aus ..."
    fi

    (
        cd "$web_dir"
        npm ci
        printf '%s\n' "$expected_lock_hash" > "$lock_hash_file"
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

wait_for_dev_api_ready() {
    local timeout_seconds="${1:-90}"
    local deadline
    deadline=$((SECONDS + timeout_seconds))

    while (( SECONDS < deadline )); do
        local api_pid
        if ! api_pid="$(get_running_pid_from_file "$DEV_API_PID_FILE")"; then
            fail "API-Prozess ist vorzeitig beendet. Details: $DEV_API_LOG_FILE"
        fi

        if http_ok "http://127.0.0.1:5001/health/ready"; then
            return 0
        fi

        sleep 2
    done

    fail "API wurde innerhalb von $timeout_seconds Sekunden nicht auf /health/ready bereit. Details: $DEV_API_LOG_FILE"
}

wait_for_dev_web_ready() {
    local timeout_seconds="${1:-60}"
    local deadline
    deadline=$((SECONDS + timeout_seconds))

    while (( SECONDS < deadline )); do
        local web_pid
        if ! web_pid="$(get_running_pid_from_file "$DEV_WEB_PID_FILE")"; then
            fail "Web-Prozess ist vorzeitig beendet. Details: $DEV_WEB_LOG_FILE"
        fi

        if http_ok "http://127.0.0.1:5173"; then
            return 0
        fi

        sleep 2
    done

    fail "Web wurde innerhalb von $timeout_seconds Sekunden nicht erreichbar. Details: $DEV_WEB_LOG_FILE"
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
            HOST_RUNTIME_HEALTH_ENABLED=true \
            HOST_RUNTIME_PROCFS_PATH=/proc \
            HOST_RUNTIME_ROOT_PATH=/ \
            dotnet run --project api/API/API.csproj
    ) >>"$DEV_API_LOG_FILE" 2>&1 &
    echo "$!" > "$DEV_API_PID_FILE"

    sleep 2
    local started_pid
    if ! started_pid="$(get_running_pid_from_file "$DEV_API_PID_FILE")"; then
        fail "API konnte nicht gestartet werden. Details: $DEV_API_LOG_FILE"
    fi

    wait_for_dev_api_ready

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

    if [[ -d "$REPO_ROOT/web/node_modules/.vite" ]]; then
        rm -rf "$REPO_ROOT/web/node_modules/.vite"
    fi

    echo "Starte Vite-Webserver im Hintergrund ..."
    (
        cd "$REPO_ROOT/web"
        exec nohup env \
            VITE_API_PROXY_TARGET=http://127.0.0.1:5001 \
            VITE_AUTH_MODE=dev-sim \
            npm run dev -- --host 0.0.0.0 --force
    ) >>"$DEV_WEB_LOG_FILE" 2>&1 &
    echo "$!" > "$DEV_WEB_PID_FILE"

    sleep 2
    local started_pid
    if ! started_pid="$(get_running_pid_from_file "$DEV_WEB_PID_FILE")"; then
        fail "Web konnte nicht gestartet werden. Details: $DEV_WEB_LOG_FILE"
    fi

    wait_for_dev_web_ready

    echo "Web gestartet (PID $started_pid, Log: $DEV_WEB_LOG_FILE)."
}

stop_dev_process() {
    local pid_file="$1"
    local label="$2"
    local port="${3:-}"
    local pid

    if ! pid="$(get_running_pid_from_file "$pid_file")"; then
        echo "$label laeuft nicht."
    else
        echo "Stoppe $label (PID $pid) ..."
        kill_pid_tree "$pid" TERM

        local deadline
        deadline=$((SECONDS + 15))
        while is_pid_running "$pid" && (( SECONDS < deadline )); do
            sleep 1
        done

        if is_pid_running "$pid"; then
            kill_pid_tree "$pid" KILL
        fi

        rm -f "$pid_file"
    fi

    if [[ -n "$port" ]] && port_is_listening "$port"; then
        local stray_pid
        for stray_pid in $(find_listening_pids_by_port "$port"); do
            [[ -n "$stray_pid" ]] || continue
            echo "Bereinige uebrig gebliebenen $label-Portprozess (PID $stray_pid) ..."
            kill_pid_tree "$stray_pid" TERM
            sleep 1
            if is_pid_running "$stray_pid"; then
                kill_pid_tree "$stray_pid" KILL
            fi
        done
    fi
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
    stop_dev_process "$DEV_WEB_PID_FILE" "Web" "5173"
    stop_dev_process "$DEV_API_PID_FILE" "API" "5001"
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
