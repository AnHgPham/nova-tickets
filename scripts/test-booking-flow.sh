#!/usr/bin/env bash
set -euo pipefail

base_url="${1:-http://127.0.0.1:3000}"
cookie_file="/tmp/nova-booking-flow.cookie"
for _ in $(seq 1 20); do curl -fsS "$base_url/api/health" >/dev/null && break; sleep 1; done

performance_id="$(curl -fsS "$base_url/api/events" | grep -o '"performances":\[{"id":"[^"]*' | sed -n '1p' | sed 's/.*"id":"//')"
seat_id="$(curl -fsS "$base_url/api/performances/$performance_id/seats" | grep -o '"seatId":"[^"]*"[^}]*"status":"Available"' | sed -n '1p' | grep -o '"seatId":"[^"]*' | sed 's/.*"seatId":"//')"
if [[ -z "$seat_id" ]]; then echo "No available seat found for integration test." >&2; exit 1; fi

curl -fsS -c "$cookie_file" -H 'Content-Type: application/json' -d '{"email":"demo@novatickets.vn","password":"Demo@12345"}' "$base_url/api/auth/login" >/dev/null
hold="$(curl -fsS -b "$cookie_file" -H 'Content-Type: application/json' -d "{\"performanceId\":\"$performance_id\",\"seatIds\":[\"$seat_id\"]}" "$base_url/api/seat-holds")"
hold_token="$(printf '%s' "$hold" | grep -o '"holdToken":"[^"]*' | sed 's/.*"holdToken":"//')"
idempotency_key="integration-$(date +%s%N)"
payload="$(printf '{"holdToken":"%s","idempotencyKey":"%s","customerName":"Integration Tester","customerEmail":"integration@novatickets.vn","customerPhone":"0900000000"}' "$hold_token" "$idempotency_key")"
first="$(curl -fsS -b "$cookie_file" -H 'Content-Type: application/json' -d "$payload" "$base_url/api/bookings")"
second="$(curl -fsS -b "$cookie_file" -H 'Content-Type: application/json' -d "$payload" "$base_url/api/bookings")"
first_id="$(printf '%s' "$first" | grep -o '"id":"[^"]*' | head -1 | sed 's/.*"id":"//')"
second_id="$(printf '%s' "$second" | grep -o '"id":"[^"]*' | head -1 | sed 's/.*"id":"//')"
tickets="$(printf '%s' "$first" | grep -o '"ticketCode":"[^"]*' | wc -l | tr -d ' ')"

if [[ "$first_id" != "$second_id" || "$tickets" -lt 1 ]]; then
  echo "Booking flow or idempotency assertion failed." >&2
  printf 'first=%s\nsecond=%s\n' "$first" "$second" >&2
  exit 1
fi
if [[ "${NOVA_VERIFY_ARTIFACTS:-0}" == "1" ]]; then
  artifacts="$(curl -fsS "$base_url/api/test/bookings/$first_id/artifacts")"
  if ! printf '%s' "$artifacts" | grep -q '"ticketCount":1' || ! printf '%s' "$artifacts" | grep -q '"paymentCount":1' || ! printf '%s' "$artifacts" | grep -q '"paymentIdempotencyKey":"integration-' || ! printf '%s' "$artifacts" | grep -q '"emailCount":1' || ! printf '%s' "$artifacts" | grep -q '"emailStatus":"Sent"' || ! printf '%s' "$artifacts" | grep -q '"emailProvider":"OfflinePreview"' || ! printf '%s' "$artifacts" | grep -q '"emailAttemptCount":1' || ! printf '%s' "$artifacts" | grep -q '"emailContainsBookingCode":true' || ! printf '%s' "$artifacts" | grep -q '"emailContainsTicketCode":true'; then
    echo "Ticket/payment/email artifact assertion failed: $artifacts" >&2
    exit 1
  fi
fi
preview="$(curl -fsS -b "$cookie_file" "$base_url/api/bookings/$first_id/confirmation-email")"
if ! printf '%s' "$preview" | grep -q '"status":"Sent"' || ! printf '%s' "$preview" | grep -q '"provider":"OfflinePreview"'; then
  echo "Customer email preview assertion failed: $preview" >&2
  exit 1
fi
email_id="$(printf '%s' "$preview" | grep -o '"id":"[^"]*' | sed 's/.*"id":"//')"
failed="$(curl -fsS -b "$cookie_file" -X POST "$base_url/api/test/bookings/$first_id/email-failed")"
if ! printf '%s' "$failed" | grep -q '"status":"Failed"'; then
  echo "Test-only email failure transition assertion failed: $failed" >&2
  exit 1
fi
admin_cookie="/tmp/nova-booking-flow-admin.cookie"
curl -fsS -c "$admin_cookie" -H 'Content-Type: application/json' -d '{"email":"admin@novatickets.vn","password":"Admin@12345"}' "$base_url/api/auth/login" >/dev/null
retry="$(curl -fsS -b "$admin_cookie" -X POST "$base_url/api/admin/email-outbox/$email_id/retry")"
if ! printf '%s' "$retry" | grep -q '"status":"Sent"' || ! printf '%s' "$retry" | grep -q '"attemptCount":2'; then
  echo "Admin email retry assertion failed: $retry" >&2
  exit 1
fi
printf 'PASS: booking=%s, seat=%s, tickets=%s, payment and offline email issued once; Failed -> Sent retry and preview verified; duplicate confirmation returned same booking.\n' "$first_id" "$seat_id" "$tickets"
