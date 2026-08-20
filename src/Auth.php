<?php
declare(strict_types=1);

/**
 * Loads config/config.php once and caches it for the request.
 */
function load_config(): array
{
    static $config = null;

    if ($config === null) {
        $path = __DIR__ . '/../config/config.php';
        if (!is_file($path)) {
            http_response_code(500);
            header('Content-Type: application/json');
            echo json_encode(['error' => 'Server misconfigured: config/config.php is missing']);
            exit;
        }
        $config = require $path;
    }

    return $config;
}

/**
 * Resolves the caller's IP. Only trusts X-Forwarded-For when the direct
 * connection comes from a configured trusted proxy, to prevent header spoofing.
 */
function get_client_ip(array $config): string
{
    $remote = $_SERVER['REMOTE_ADDR'] ?? '0.0.0.0';

    $trustedProxies = $config['trusted_proxies'] ?? [];
    if ($trustedProxies && in_array($remote, $trustedProxies, true)) {
        $forwarded = $_SERVER['HTTP_X_FORWARDED_FOR'] ?? '';
        if ($forwarded !== '') {
            $parts = explode(',', $forwarded);
            $candidate = trim($parts[0]);
            if (filter_var($candidate, FILTER_VALIDATE_IP)) {
                return $candidate;
            }
        }
    }

    return $remote;
}

/**
 * Reads the API key from the X-API-Key header, or an Authorization: Bearer header.
 */
function extract_api_key(): ?string
{
    $headers = function_exists('getallheaders') ? getallheaders() : [];
    // getallheaders() can be inconsistent in casing depending on SAPI; normalize.
    $normalized = [];
    foreach ($headers as $name => $value) {
        $normalized[strtolower($name)] = $value;
    }

    if (isset($normalized['x-api-key']) && $normalized['x-api-key'] !== '') {
        return $normalized['x-api-key'];
    }

    $auth = $normalized['authorization'] ?? ($_SERVER['HTTP_AUTHORIZATION'] ?? '');
    if (stripos($auth, 'Bearer ') === 0) {
        return substr($auth, 7);
    }

    return null;
}

/**
 * Finds the client whose stored key hash matches the supplied API key.
 * Uses hash_equals for timing-safe comparison and checks every client
 * (rather than short-circuiting) so response time doesn't leak which
 * client names are valid.
 */
function authenticate_key(array $config, string $apiKey): ?array
{
    $suppliedHash = hash('sha256', $apiKey);
    $match = null;

    foreach ($config['clients'] ?? [] as $name => $client) {
        if (hash_equals($client['key_hash'], $suppliedHash)) {
            $match = $client + ['name' => $name];
        }
    }

    return $match;
}

/**
 * Checks whether $ip falls within one of the client's allowed IPs/CIDR ranges.
 */
function check_ip_allowed(array $client, string $ip): bool
{
    foreach ($client['allowed_ips'] ?? [] as $range) {
        if (ip_in_range($ip, $range)) {
            return true;
        }
    }

    return false;
}

/**
 * Supports a bare IP ("203.0.113.10") or CIDR notation ("203.0.113.0/24"),
 * for both IPv4 and IPv6.
 */
function ip_in_range(string $ip, string $range): bool
{
    if (strpos($range, '/') === false) {
        return hash_equals(inet_pton($range) ?: '', inet_pton($ip) ?: "\0");
    }

    [$subnet, $bits] = explode('/', $range, 2);
    $bits = (int) $bits;

    $ipBin = inet_pton($ip);
    $subnetBin = inet_pton($subnet);
    if ($ipBin === false || $subnetBin === false || strlen($ipBin) !== strlen($subnetBin)) {
        return false;
    }

    $bytes = intdiv($bits, 8);
    $remainderBits = $bits % 8;

    if ($bytes > 0 && substr($ipBin, 0, $bytes) !== substr($subnetBin, 0, $bytes)) {
        return false;
    }

    if ($remainderBits === 0) {
        return true;
    }

    $mask = ~(0xFF >> $remainderBits) & 0xFF;
    $ipByte = ord($ipBin[$bytes] ?? "\0");
    $subnetByte = ord($subnetBin[$bytes] ?? "\0");

    return ($ipByte & $mask) === ($subnetByte & $mask);
}

/**
 * Appends a single-line JSON log entry. Never throws — logging failures
 * must not block the caller's error response.
 */
function log_event(array $config, string $event, string $ip, ?string $clientName, array $extra = []): void
{
    $logFile = $config['log_file'] ?? null;
    if (!$logFile) {
        return;
    }

    $entry = [
        'time' => date('c'),
        'event' => $event,
        'ip' => $ip,
        'client' => $clientName,
    ] + $extra;

    @file_put_contents($logFile, json_encode($entry) . "\n", FILE_APPEND | LOCK_EX);
}

/**
 * Sends a JSON response and terminates the request.
 */
function respond(int $statusCode, array $body): never
{
    http_response_code($statusCode);
    header('Content-Type: application/json');
    echo json_encode($body);
    exit;
}
