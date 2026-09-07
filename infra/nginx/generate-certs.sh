#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CERTS_DIR="${SCRIPT_DIR}/certs"

mkdir -p "${CERTS_DIR}"

KEY_PATH="${CERTS_DIR}/nginx-selfsigned.key"
CRT_PATH="${CERTS_DIR}/nginx-selfsigned.crt"

echo "Generating self-signed certificate for local dev..."
openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
    -keyout "${KEY_PATH}" \
    -out "${CRT_PATH}" \
    -subj "/CN=localhost" \
    -addext "subjectAltName=DNS:localhost,IP:127.0.0.1"

echo "Certificates generated successfully:"
echo "Key: ${KEY_PATH}"
echo "Cert: ${CRT_PATH}"
