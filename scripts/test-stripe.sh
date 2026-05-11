#!/usr/bin/env bash
# Minimal end-to-end test script: register/login -> create client -> create invoice -> create payment link
# Usage: ./scripts/test-stripe.sh

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
API_URL="http://localhost:5216"

if ! command -v jq >/dev/null 2>&1; then
  echo "jq is required. Install with: brew install jq"
  exit 1
fi

echo "Using API at $API_URL"

# Register (ignore errors if user exists)
echo "Registering demo user (may fail if already exists)..."
curl -s -X POST "$API_URL/api/v1/auth/register" \
  -H "Content-Type: application/json" \
  -d '{"email":"demo+1@example.com","password":"Password123!","firstName":"Demo","lastName":"User"}' || true

echo "Logging in..."
LOGIN_JSON=$(curl -s -X POST "$API_URL/api/v1/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email":"demo+1@example.com","password":"Password123!"}')

echo "Login response:"
echo "$LOGIN_JSON" | jq .

ACCESS_TOKEN=$(echo "$LOGIN_JSON" | jq -r '(.accessToken // .AccessToken // .token // .access_token)')
if [ -z "$ACCESS_TOKEN" ] || [ "$ACCESS_TOKEN" = "null" ]; then
  echo "Failed to obtain access token. Inspect login response above." >&2
  exit 2
fi

echo "Access token obtained"

echo "Creating client..."
CREATE_CLIENT_RESP=$(curl -s -X POST "$API_URL/api/v1/clients" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"clientType":"company","companyName":"ACME Ltd","email":"billing@acme.test","addressLine1":"High St 1","countryCode":"LT"}')

echo "$CREATE_CLIENT_RESP" | jq .
CLIENT_ID=$(echo "$CREATE_CLIENT_RESP" | jq -r '.id // .Id // empty')
if [ -z "$CLIENT_ID" ]; then
  echo "Failed to create client. Inspect response above." >&2
  exit 3
fi

echo "CLIENT_ID=$CLIENT_ID"

echo "Creating invoice..."
CREATE_INVOICE_RESP=$(curl -s -X POST "$API_URL/api/v1/invoices" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d @- <<JSON
{
  "clientId": "$CLIENT_ID",
  "projectId": null,
  "languageCode": "en",
  "issueDate": "2026-05-11",
  "dueDate": "2026-06-11",
  "currency": "EUR",
  "notes": null,
  "lineItems": [
    {"sortOrder":1,"description":"Test work","quantity":1,"unit":"pc","unitPrice":10.00,"vatRate":0.0}
  ]
}
JSON
)

echo "$CREATE_INVOICE_RESP" | jq .
INVOICE_ID=$(echo "$CREATE_INVOICE_RESP" | jq -r '.id // .Id // empty')
if [ -z "$INVOICE_ID" ]; then
  echo "Failed to create invoice. Inspect response above." >&2
  exit 4
fi

echo "INVOICE_ID=$INVOICE_ID"

echo "Requesting payment link..."
CREATE_LINK_RESP=$(curl -s -X POST "$API_URL/api/v1/invoices/$INVOICE_ID/create-payment-link" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json")

echo "$CREATE_LINK_RESP" | jq .
CHECKOUT_URL=$(echo "$CREATE_LINK_RESP" | jq -r '.url // .Url // .checkoutUrl // .checkout_url // empty')
if [ -z "$CHECKOUT_URL" ]; then
  echo "Failed to get checkout URL. Inspect response above." >&2
  exit 5
fi

echo "Checkout URL: $CHECKOUT_URL"
echo
echo "Opening checkout URL in default browser..."
open "$CHECKOUT_URL"

echo "Done. Complete payment in browser using Stripe test card 4242 4242 4242 4242"

exit 0

