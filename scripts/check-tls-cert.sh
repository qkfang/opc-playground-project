#!/usr/bin/env bash
set -euo pipefail

HOST="${1:-${SITE_HOST:-}}"

if [[ -z "${HOST}" ]]; then
  echo "Usage: SITE_HOST=example.com bash scripts/check-tls-cert.sh"
  echo "   or: bash scripts/check-tls-cert.sh example.com"
  exit 1
fi

if ! command -v openssl >/dev/null 2>&1; then
  echo "openssl is required but not installed."
  exit 1
fi

TLS_OUTPUT="$(openssl s_client -connect "${HOST}:443" -servername "${HOST}" -verify_return_error -showcerts </dev/null 2>/dev/null || true)"

if [[ -z "${TLS_OUTPUT}" ]]; then
  echo "Could not retrieve TLS certificate from ${HOST}:443"
  exit 1
fi

VERIFY_LINE="$(printf '%s\n' "${TLS_OUTPUT}" | sed -n 's/^ *Verify return code: /Verify return code: /p' | tail -n1)"

if [[ "${VERIFY_LINE}" != "Verify return code: 0 (ok)" ]]; then
  echo "Certificate verification failed for ${HOST}"
  echo "${VERIFY_LINE:-Verify return code not found}"
  exit 1
fi

LEAF_CERT="$(printf '%s\n' "${TLS_OUTPUT}" | awk '/-----BEGIN CERTIFICATE-----/{capture=1} capture{print} /-----END CERTIFICATE-----/{exit}')"

if [[ -z "${LEAF_CERT}" ]]; then
  echo "Failed to extract leaf certificate for ${HOST}"
  exit 1
fi

START_DATE="$(printf '%s\n' "${LEAF_CERT}" | openssl x509 -noout -startdate | cut -d= -f2-)"
END_DATE="$(printf '%s\n' "${LEAF_CERT}" | openssl x509 -noout -enddate | cut -d= -f2-)"

if [[ -z "${START_DATE}" || -z "${END_DATE}" ]]; then
  echo "Could not read certificate validity dates for ${HOST}"
  exit 1
fi

NOW_EPOCH="$(date -u +%s)"
EXPIRY_EPOCH="$(date -u -d "${END_DATE}" +%s)"
DAYS_LEFT="$(( (EXPIRY_EPOCH - NOW_EPOCH) / 86400 ))"

if (( DAYS_LEFT < 0 )); then
  echo "Certificate for ${HOST} is expired (ended ${END_DATE})."
  exit 1
fi

echo "TLS certificate check passed for ${HOST}"
echo "  Valid from : ${START_DATE}"
echo "  Valid until: ${END_DATE} (${DAYS_LEFT} days remaining)"
echo "  Chain check: ${VERIFY_LINE}"
