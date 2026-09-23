#!/usr/bin/env bash
set -euo pipefail

base_url="${1:-http://127.0.0.1:3000}"
run_id="$(date +%s%N)"
email_a="auth-a-$run_id@novatickets.test"
email_b="auth-b-$run_id@novatickets.test"
email_c="auth-lockout-$run_id@novatickets.test"
password='Strong@12345'
cookie_a="/tmp/nova-auth-a-$run_id.cookie"
cookie_b="/tmp/nova-auth-b-$run_id.cookie"
cookie_c="/tmp/nova-auth-c-$run_id.cookie"
admin_cookie="/tmp/nova-auth-admin-$run_id.cookie"
trap 'rm -f "$cookie_a" "$cookie_b" "$cookie_c" "$admin_cookie"' EXIT

status="$(curl -sS -o /tmp/nova-weak-password.json -w '%{http_code}' \
  -H 'Content-Type: application/json' \
  -d "{\"email\":\"weak-$run_id@novatickets.test\",\"password\":\"weakpass\",\"fullName\":\"Weak Password\",\"phone\":\"0900000001\"}" \
  "$base_url/api/auth/register")"
[[ "$status" == "400" ]] || { echo "Expected weak password 400, got $status" >&2; exit 1; }

register_a="$(curl -fsS -c "$cookie_a" -H 'Content-Type: application/json' \
  -d "{\"email\":\"$email_a\",\"password\":\"$password\",\"fullName\":\"Auth Tester A\",\"phone\":\"0900000002\"}" \
  "$base_url/api/auth/register")"
printf '%s' "$register_a" | grep -q "\"email\":\"$email_a\"" || { echo "Registration A did not return persisted user." >&2; exit 1; }
curl -fsS -b "$cookie_a" "$base_url/api/auth/me" | grep -q "\"email\":\"$email_a\""

duplicate_status="$(curl -sS -o /tmp/nova-duplicate-email.json -w '%{http_code}' \
  -H 'Content-Type: application/json' \
  -d "{\"email\":\"$email_a\",\"password\":\"$password\",\"fullName\":\"Duplicate\",\"phone\":\"0900000003\"}" \
  "$base_url/api/auth/register")"
[[ "$duplicate_status" == "409" ]] || { echo "Expected duplicate email 409, got $duplicate_status" >&2; exit 1; }

wrong_login_status="$(curl -sS -o /tmp/nova-wrong-login.json -w '%{http_code}' \
  -H 'Content-Type: application/json' -d "{\"email\":\"$email_a\",\"password\":\"Wrong@12345\"}" \
  "$base_url/api/auth/login")"
[[ "$wrong_login_status" == "401" ]] || { echo "Expected wrong login 401, got $wrong_login_status" >&2; exit 1; }

curl -fsS -b "$cookie_a" -c "$cookie_a" -X POST "$base_url/api/auth/logout" >/dev/null
after_logout_status="$(curl -sS -o /tmp/nova-after-logout.json -w '%{http_code}' -b "$cookie_a" "$base_url/api/auth/me")"
[[ "$after_logout_status" == "401" ]] || { echo "Expected /me 401 after logout, got $after_logout_status" >&2; exit 1; }
curl -fsS -c "$cookie_a" -H 'Content-Type: application/json' \
  -d "{\"email\":\"$email_a\",\"password\":\"$password\"}" "$base_url/api/auth/login" >/dev/null

register_b="$(curl -fsS -c "$cookie_b" -H 'Content-Type: application/json' \
  -d "{\"email\":\"$email_b\",\"password\":\"$password\",\"fullName\":\"Auth Tester B\",\"phone\":\"0900000004\"}" \
  "$base_url/api/auth/register")"
user_b_id="$(printf '%s' "$register_b" | grep -o '"id":"[^"]*' | sed -n '1p' | sed 's/.*"id":"//')"

curl -fsS -c "$cookie_c" -H 'Content-Type: application/json' \
  -d "{\"email\":\"$email_c\",\"password\":\"$password\",\"fullName\":\"Lockout Tester\",\"phone\":\"0900000005\"}" \
  "$base_url/api/auth/register" >/dev/null
for attempt in 1 2 3 4; do
  lock_status="$(curl -sS -o /tmp/nova-lockout-$attempt.json -w '%{http_code}' -H 'Content-Type: application/json' \
    -d "{\"email\":\"$email_c\",\"password\":\"Wrong@12345\"}" "$base_url/api/auth/login")"
  [[ "$lock_status" == "401" ]] || { echo "Expected lockout attempt $attempt to return 401, got $lock_status" >&2; exit 1; }
done
lock_status="$(curl -sS -o /tmp/nova-lockout-5.json -w '%{http_code}' -H 'Content-Type: application/json' \
  -d "{\"email\":\"$email_c\",\"password\":\"Wrong@12345\"}" "$base_url/api/auth/login")"
[[ "$lock_status" == "423" ]] || { echo "Expected fifth failed login to return 423, got $lock_status" >&2; exit 1; }
locked_correct_status="$(curl -sS -o /tmp/nova-lockout-correct.json -w '%{http_code}' -H 'Content-Type: application/json' \
  -d "{\"email\":\"$email_c\",\"password\":\"$password\"}" "$base_url/api/auth/login")"
[[ "$locked_correct_status" == "423" ]] || { echo "Expected locked account to reject correct password with 423, got $locked_correct_status" >&2; exit 1; }

performance_id="$(curl -fsS "$base_url/api/events" | grep -o '"performances":\[{"id":"[^"]*' | sed -n '1p' | sed 's/.*"id":"//')"
seat_id="$(curl -fsS "$base_url/api/performances/$performance_id/seats" | grep -o '"seatId":"[^"]*"[^}]*"status":"Available"' | sed -n '1p' | grep -o '"seatId":"[^"]*' | sed 's/.*"seatId":"//')"
[[ -n "$seat_id" ]] || { echo "No available seat for ownership test." >&2; exit 1; }

hold="$(curl -fsS -b "$cookie_a" -H 'Content-Type: application/json' \
  -d "{\"performanceId\":\"$performance_id\",\"seatIds\":[\"$seat_id\"]}" "$base_url/api/seat-holds")"
hold_token="$(printf '%s' "$hold" | grep -o '"holdToken":"[^"]*' | sed 's/.*"holdToken":"//')"
booking="$(curl -fsS -b "$cookie_a" -H 'Content-Type: application/json' \
  -d "{\"holdToken\":\"$hold_token\",\"idempotencyKey\":\"ownership-$run_id\",\"customerName\":\"Auth Tester A\",\"customerEmail\":\"$email_a\",\"customerPhone\":\"0900000002\"}" \
  "$base_url/api/bookings")"
booking_id="$(printf '%s' "$booking" | grep -o '"id":"[^"]*' | sed -n '1p' | sed 's/.*"id":"//')"
[[ -n "$booking_id" ]] || { echo "Booking ownership test did not create a booking." >&2; exit 1; }
curl -fsS -b "$cookie_a" "$base_url/api/bookings/$booking_id" | grep -q "\"id\":\"$booking_id\""
other_user_status="$(curl -sS -o /tmp/nova-other-user-booking.json -w '%{http_code}' -b "$cookie_b" "$base_url/api/bookings/$booking_id")"
[[ "$other_user_status" == "404" ]] || { echo "Expected other user booking access 404, got $other_user_status" >&2; exit 1; }
curl -fsS -b "$cookie_a" "$base_url/api/bookings/mine" | grep -q "\"id\":\"$booking_id\""

curl -fsS -c "$admin_cookie" -H 'Content-Type: application/json' \
  -d '{"email":"admin@novatickets.vn","password":"Admin@12345"}' "$base_url/api/auth/login" >/dev/null
curl -fsS -b "$admin_cookie" -X DELETE "$base_url/api/admin/users/$user_b_id" >/dev/null
inactive_session_status="$(curl -sS -o /tmp/nova-inactive-session.json -w '%{http_code}' -b "$cookie_b" "$base_url/api/auth/me")"
[[ "$inactive_session_status" == "401" ]] || { echo "Expected deactivated user session to return 401, got $inactive_session_status" >&2; exit 1; }

rm -f /tmp/nova-weak-password.json /tmp/nova-duplicate-email.json /tmp/nova-wrong-login.json /tmp/nova-after-logout.json /tmp/nova-other-user-booking.json /tmp/nova-lockout-*.json /tmp/nova-inactive-session.json
printf 'PASS: registration, password policy, login/logout, lockout, session revocation and booking ownership are enforced.\n'
