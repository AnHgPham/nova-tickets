#!/usr/bin/env bash
set -euo pipefail

base_url="${1:-http://127.0.0.1:3000}"
cookie_file="/tmp/nova-expired-hold.cookie"
performance_id="$(curl -fsS "$base_url/api/events" | grep -o '"performances":\[{"id":"[^"]*' | sed -n '1p' | sed 's/.*"id":"//')"
seat_id="$(curl -fsS "$base_url/api/performances/$performance_id/seats" | grep -o '"seatId":"[^"]*"[^}]*"status":"Available"' | sed -n '1p' | grep -o '"seatId":"[^"]*' | sed 's/.*"seatId":"//')"
curl -fsS -c "$cookie_file" -H 'Content-Type: application/json' -d '{"email":"demo@novatickets.vn","password":"Demo@12345"}' "$base_url/api/auth/login" >/dev/null
hold="$(curl -fsS -b "$cookie_file" -H 'Content-Type: application/json' -d "{\"performanceId\":\"$performance_id\",\"seatIds\":[\"$seat_id\"]}" "$base_url/api/seat-holds")"
hold_token="$(printf '%s' "$hold" | grep -o '"holdToken":"[^"]*' | sed 's/.*"holdToken":"//')"
result="$(curl -fsS -X POST "$base_url/api/test/expire-and-cleanup/$hold_token")"
released="$(printf '%s' "$result" | grep -o '"released":[0-9]*' | sed 's/.*://')"
if [[ "$released" != "1" ]]; then echo "Expected one released hold, received: $result" >&2; exit 1; fi
echo "PASS: expired hold $hold_token was proactively released."
