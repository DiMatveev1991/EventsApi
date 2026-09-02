#!/usr/bin/env bash
set -euo pipefail

wait_for_api() {
  local url="$1"
  local name="$2"

  for _ in $(seq 1 120); do
    if curl --fail --silent --show-error "$url" >/dev/null; then
      return
    fi
    sleep 2
  done

  echo "$name did not become ready" >&2
  return 1
}

docker compose config --quiet
docker compose up --build --detach

wait_for_api "http://localhost:5001/health" "Users API"
wait_for_api "http://localhost:5002/health" "Events API"
wait_for_api "http://localhost:5003/health" "Bookings API"
wait_for_api "http://localhost:9090/-/ready" "Prometheus"
wait_for_api "http://localhost:16686/" "Jaeger"
wait_for_api "http://localhost:3000/api/health" "Grafana"

for port in 5001 5002 5003; do
  metrics=$(curl --fail --silent --show-error "http://localhost:$port/metrics")
  grep --quiet '^http_server_request_duration_seconds' <<<"$metrics"
done

users_logs=$(docker compose logs --no-color --no-log-prefix users-api)
structured_log=$(grep --max-count=1 '^{' <<<"$users_logs")
jq --exit-status . <<<"$structured_log" >/dev/null

admin_register_status=$(curl --silent --show-error --output /dev/null --write-out "%{http_code}" \
  --request POST "http://localhost:5001/auth/register" \
  --header "Content-Type: application/json" \
  --data '{"login":"ci-admin","password":"Password123!","role":"Admin"}')
test "$admin_register_status" = "204"

admin_token=$(curl --fail --silent --show-error \
  --request POST "http://localhost:5001/auth/login" \
  --header "Content-Type: application/json" \
  --data '{"login":"ci-admin","password":"Password123!"}' | jq --raw-output ".token")
test -n "$admin_token"

event_json=$(curl --fail --silent --show-error \
  --request POST "http://localhost:5002/events" \
  --header "Authorization: Bearer $admin_token" \
  --header "Content-Type: application/json" \
  --data '{"title":"Kafka smoke test","description":"CI","startAt":"2035-01-01T10:00:00Z","endAt":"2035-01-01T12:00:00Z","totalSeats":3}')
event_id=$(jq --raw-output ".id" <<<"$event_json")
test -n "$event_id"
test "$(jq --raw-output ".availableSeats" <<<"$event_json")" = "3"

user_register_status=$(curl --silent --show-error --output /dev/null --write-out "%{http_code}" \
  --request POST "http://localhost:5001/auth/register" \
  --header "Content-Type: application/json" \
  --data '{"login":"ci-user","password":"Password123!","role":"User"}')
test "$user_register_status" = "204"

user_token=$(curl --fail --silent --show-error \
  --request POST "http://localhost:5001/auth/login" \
  --header "Content-Type: application/json" \
  --data '{"login":"ci-user","password":"Password123!"}' | jq --raw-output ".token")
test -n "$user_token"

booking_json=$(curl --fail --silent --show-error \
  --request POST "http://localhost:5003/bookings" \
  --header "Authorization: Bearer $user_token" \
  --header "Content-Type: application/json" \
  --data "{\"eventId\":\"$event_id\",\"seats\":1}")
test "$(jq --raw-output ".status" <<<"$booking_json")" = "Pending"

available_seats=3
for _ in $(seq 1 30); do
  available_seats=$(curl --fail --silent --show-error \
    "http://localhost:5002/events/$event_id" | jq --raw-output ".availableSeats")
  if test "$available_seats" = "2"; then
    break
  fi
  sleep 1
done
test "$available_seats" = "2"

event_ttl=$(docker compose exec -T redis redis-cli TTL "event:$event_id" | tr -d '\r')
test "$event_ttl" -gt 0
test "$event_ttl" -le 300

top_json=$(curl --fail --silent --show-error "http://localhost:5002/events/top")
top_count=$(jq "length" <<<"$top_json")
test "$top_count" -ge 1
test "$top_count" -le 10
top_ttl=$(docker compose exec -T redis redis-cli TTL "events:top10" | tr -d '\r')
test "$top_ttl" -gt 0
test "$top_ttl" -le 60

# Redis is optional at runtime: Events must restart and serve database-backed
# requests while the cache is unavailable.
docker compose stop redis
docker compose restart events-api
wait_for_api "http://localhost:5002/health" "Events API without Redis"
available_without_redis=$(curl --max-time 10 --fail --silent --show-error \
  "http://localhost:5002/events/$event_id" | jq --raw-output ".availableSeats")
test "$available_without_redis" = "2"

unauthorized_booking_status=$(curl --silent --show-error --output /dev/null \
  --write-out "%{http_code}" --request POST "http://localhost:5003/bookings" \
  --header "Content-Type: application/json" \
  --data "{\"eventId\":\"$event_id\",\"seats\":1}")
test "$unauthorized_booking_status" = "401"

forbidden_event_status=$(curl --silent --show-error --output /dev/null \
  --write-out "%{http_code}" --request POST "http://localhost:5002/events" \
  --header "Authorization: Bearer $user_token" \
  --header "Content-Type: application/json" \
  --data '{"title":"Forbidden","startAt":"2035-01-01T10:00:00Z","endAt":"2035-01-01T12:00:00Z","totalSeats":1}')
test "$forbidden_event_status" = "403"

healthy_targets=0
for _ in $(seq 1 30); do
  healthy_targets=$(curl --fail --silent --show-error \
    "http://localhost:9090/api/v1/targets" \
    | jq '[.data.activeTargets[] | select(.health == "up")] | length')
  if test "$healthy_targets" = "3"; then
    break
  fi
  sleep 2
done
test "$healthy_targets" = "3"

traced_services=""
for _ in $(seq 1 30); do
  traced_services=$(curl --fail --silent --show-error \
    "http://localhost:16686/api/services" | jq --raw-output '.data[]?' | sort)
  if grep --quiet '^users-service$' <<<"$traced_services" \
    && grep --quiet '^events-service$' <<<"$traced_services" \
    && grep --quiet '^bookings-service$' <<<"$traced_services"; then
    break
  fi
  sleep 2
done
grep --quiet '^users-service$' <<<"$traced_services"
grep --quiet '^events-service$' <<<"$traced_services"
grep --quiet '^bookings-service$' <<<"$traced_services"

events_traces=$(curl --fail --silent --show-error \
  "http://localhost:16686/api/traces?service=events-service&limit=20&lookback=1h")
jq --exit-status \
  '[.data[].spans[].tags[]? | select(.key == "http.request.method" or .key == "http.method")] | length > 0' \
  <<<"$events_traces" >/dev/null
jq --exit-status \
  '[.data[].spans[].tags[]? | select(.key == "db.system.name" or .key == "db.system")] | length > 0' \
  <<<"$events_traces" >/dev/null

dashboard_count=0
for _ in $(seq 1 30); do
  dashboard_count=$(curl --fail --silent --show-error --user admin:admin \
    "http://localhost:3000/api/search?query=Events%20API%20observability" \
    | jq 'length')
  if test "$dashboard_count" -ge 1; then
    break
  fi
  sleep 2
done
test "$dashboard_count" -ge 1

echo "Docker smoke test passed"
