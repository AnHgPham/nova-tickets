#!/usr/bin/env bash
set -euo pipefail

base_url="${1:-http://127.0.0.1:3000}"
cookie_file="/tmp/nova-concurrency.cookie"

for _ in $(seq 1 20); do
  if curl -fsS "$base_url/api/health" >/dev/null; then break; fi
  sleep 1
done

event_json="$(curl -fsS "$base_url/api/events")"
performance_id="$(printf '%s' "$event_json" | grep -o '"performances":\[{"id":"[^"]*' | head -1 | sed 's/.*"id":"//')"
seat_json="$(curl -fsS "$base_url/api/performances/$performance_id/seats")"
seat_id="$(printf '%s' "$seat_json" | grep -o '"seatId":"[^"]*"[^}]*"status":"Available"' | sed -n '1p' | grep -o '"seatId":"[^"]*' | sed 's/.*"seatId":"//')"
if [[ -z "$seat_id" ]]; then echo "No available seat found for concurrency test." >&2; exit 1; fi

curl -fsS -c "$cookie_file" -H 'Content-Type: application/json' \
  -d '{"email":"demo@novatickets.vn","password":"Demo@12345"}' \
  "$base_url/api/auth/login" >/dev/null

payload="$(printf '{"performanceId":"%s","seatIds":["%s"]}' "$performance_id" "$seat_id")"
curl -sS -o /tmp/nova-hold-a.json -w '%{http_code}' -b "$cookie_file" -H 'Content-Type: application/json' -d "$payload" "$base_url/api/seat-holds" >/tmp/nova-hold-status-a.txt &
pid_a=$!
curl -sS -o /tmp/nova-hold-b.json -w '%{http_code}' -b "$cookie_file" -H 'Content-Type: application/json' -d "$payload" "$base_url/api/seat-holds" >/tmp/nova-hold-status-b.txt &
pid_b=$!
wait "$pid_a" "$pid_b"

status_a="$(cat /tmp/nova-hold-status-a.txt)"
status_b="$(cat /tmp/nova-hold-status-b.txt)"
printf 'performance=%s seat=%s\nrequest_a=%s request_b=%s\n' "$performance_id" "$seat_id" "$status_a" "$status_b"

if [[ "$status_a" != "200" || "$status_b" != "409" ]] && [[ "$status_a" != "409" || "$status_b" != "200" ]]; then
  echo "Expected exactly one 200 and one 409." >&2
  cat /tmp/nova-hold-a.json /tmp/nova-hold-b.json >&2
  exit 1
fi

hold_token="$(grep -ho '"holdToken":"[^"]*' /tmp/nova-hold-a.json /tmp/nova-hold-b.json | head -1 | sed 's/.*"holdToken":"//')"
curl -fsS -X DELETE -b "$cookie_file" "$base_url/api/seat-holds/$hold_token" >/dev/null
seat_map_after_release="$(curl -fsS "$base_url/api/performances/$performance_id/seats")"
if ! printf '%s' "$seat_map_after_release" | grep -q "\"seatId\":\"$seat_id\"[^}]*\"status\":\"Available\""; then
  echo "Released seat did not return to Available." >&2
  exit 1
fi
echo "PASS: exactly one concurrent request held the seat; winning hold was released and became Available."
