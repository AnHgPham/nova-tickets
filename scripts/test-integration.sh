#!/usr/bin/env bash
set -euo pipefail

project_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
port=3055
base_url="http://127.0.0.1:$port"
db_file="/tmp/nova-integration-$$.db"
log_file="/tmp/nova-integration-$$.log"

cleanup() {
  if [[ -n "${server_pid:-}" ]]; then kill "$server_pid" 2>/dev/null || true; wait "$server_pid" 2>/dev/null || true; fi
  rm -f "$db_file" "$log_file"
}
trap cleanup EXIT

cd "$project_dir"
env ASPNETCORE_ENVIRONMENT=Testing UseSqlite=true ConnectionStrings__Sqlite="Data Source=$db_file" JWT_SECRET=integration-test-secret-0123456789 NOVA_SCHEDULER_KEY=integration-test-secret-0123456789 PORT="$port" \
  dotnet run --no-build --project src/NovaTickets.Api/NovaTickets.Api.csproj >"$log_file" 2>&1 &
server_pid=$!

for _ in $(seq 1 30); do
  if curl -fsS "$base_url/api/health" >/dev/null; then break; fi
  sleep 1
done
curl -fsS "$base_url/api/health" >/dev/null

bash scripts/test-auth-ownership.sh "$base_url"
bash scripts/test-seat-concurrency.sh "$base_url"
bash scripts/test-expired-hold-cleanup.sh "$base_url"
NOVA_VERIFY_ARTIFACTS=1 bash scripts/test-booking-flow.sh "$base_url"

printf 'cleanup_unauth='; curl -sS -o /dev/null -w '%{http_code}\n' -X POST "$base_url/api/scheduled/release-expired-holds"
printf 'cleanup_auth='; curl -fsS -o /dev/null -w '%{http_code}\n' -X POST -H 'X-Nova-Cleanup-Key: integration-test-secret-0123456789' "$base_url/api/scheduled/release-expired-holds"

curl -fsS -c /tmp/nova-integration-user.cookie -H 'Content-Type: application/json' -d '{"email":"demo@novatickets.vn","password":"Demo@12345"}' "$base_url/api/auth/login" >/dev/null
printf 'admin_as_customer='; curl -sS -o /dev/null -w '%{http_code}\n' -b /tmp/nova-integration-user.cookie "$base_url/api/admin/users"
printf 'invalid_hold='; curl -sS -o /dev/null -w '%{http_code}\n' -b /tmp/nova-integration-user.cookie -H 'Content-Type: application/json' -d '{"seatIds":[]}' "$base_url/api/seat-holds"
printf 'PASS: integration suite completed against isolated SQLite database.\n'
