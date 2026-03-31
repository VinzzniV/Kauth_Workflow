#!/bin/sh
set -eu

js_escape() {
  printf '%s' "${1:-}" | sed 's/\\/\\\\/g; s/"/\\"/g'
}

cat > /usr/share/nginx/html/app-config.js <<EOF
window.__APP_CONFIG__ = {
  apiBase: "$(js_escape "${API_BASE:-/api}")",
  authMode: "$(js_escape "${AUTH_MODE:-demo}")",
  entraClientId: "$(js_escape "${ENTRA_CLIENT_ID:-}")",
  entraTenantId: "$(js_escape "${ENTRA_TENANT_ID:-}")",
  entraRedirectUri: "$(js_escape "${ENTRA_REDIRECT_URI:-}")"
};
EOF
