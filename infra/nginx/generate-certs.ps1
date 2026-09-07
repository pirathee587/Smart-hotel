$certsDir = Join-Path $PSScriptRoot "certs"
if (!(Test-Path $certsDir)) {
    New-Item -ItemType Directory -Path $certsDir -Force | Out-Null
}

$keyPath = Join-Path $certsDir "nginx-selfsigned.key"
$crtPath = Join-Path $certsDir "nginx-selfsigned.crt"

$opensslPath = "C:\Program Files\Git\usr\bin\openssl.exe"
if (!(Test-Path $opensslPath)) {
    $opensslCmd = Get-Command openssl -ErrorAction SilentlyContinue
    if ($opensslCmd) {
        $opensslPath = $opensslCmd.Source
    } else {
        Write-Error "OpenSSL not found. Please install Git for Windows or OpenSSL."
        exit 1
    }
}

Write-Host "Generating self-signed certificate using: $opensslPath"
& $opensslPath req -x509 -nodes -days 365 -newkey rsa:2048 `
    -keyout $keyPath `
    -out $crtPath `
    -subj "/CN=localhost" `
    -addext "subjectAltName=DNS:localhost,IP:127.0.0.1"

Write-Host "Certificates generated at:"
Write-Host "Key: $keyPath"
Write-Host "Cert: $crtPath"
