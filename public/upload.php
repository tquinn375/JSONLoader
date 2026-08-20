<?php
declare(strict_types=1);

require __DIR__ . '/../src/Auth.php';

header('Content-Type: application/json');

$config = load_config();
$ip = get_client_ip($config);

if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
    respond(405, ['error' => 'Method not allowed. Use POST.']);
}

$apiKey = extract_api_key();
if ($apiKey === null || $apiKey === '') {
    log_event($config, 'AUTH_MISSING', $ip, null);
    respond(401, ['error' => 'Missing API key. Send it in the X-API-Key header.']);
}

$client = authenticate_key($config, $apiKey);
if ($client === null) {
    log_event($config, 'AUTH_FAILED', $ip, null);
    respond(401, ['error' => 'Invalid API key.']);
}

if (!check_ip_allowed($client, $ip)) {
    log_event($config, 'IP_DENIED', $ip, $client['name']);
    respond(403, ['error' => 'Source IP not permitted for this API key.']);
}

$maxBytes = $config['max_upload_bytes'] ?? 5 * 1024 * 1024;

// Accept either a raw JSON request body, or a multipart file upload field named "file".
if (!empty($_FILES['file']['tmp_name']) && is_uploaded_file($_FILES['file']['tmp_name'])) {
    if ($_FILES['file']['error'] !== UPLOAD_ERR_OK) {
        log_event($config, 'UPLOAD_ERROR', $ip, $client['name'], ['php_error' => $_FILES['file']['error']]);
        respond(400, ['error' => 'File upload failed.']);
    }
    if ($_FILES['file']['size'] > $maxBytes) {
        log_event($config, 'TOO_LARGE', $ip, $client['name'], ['size' => $_FILES['file']['size']]);
        respond(413, ['error' => 'File exceeds maximum allowed size of ' . $maxBytes . ' bytes.']);
    }
    $raw = file_get_contents($_FILES['file']['tmp_name']);
} else {
    $contentLength = (int) ($_SERVER['CONTENT_LENGTH'] ?? 0);
    if ($contentLength > $maxBytes) {
        log_event($config, 'TOO_LARGE', $ip, $client['name'], ['size' => $contentLength]);
        respond(413, ['error' => 'Body exceeds maximum allowed size of ' . $maxBytes . ' bytes.']);
    }
    $raw = file_get_contents('php://input');
}

if ($raw === false || $raw === '') {
    respond(400, ['error' => 'Empty request body.']);
}

if (strlen($raw) > $maxBytes) {
    log_event($config, 'TOO_LARGE', $ip, $client['name'], ['size' => strlen($raw)]);
    respond(413, ['error' => 'Body exceeds maximum allowed size of ' . $maxBytes . ' bytes.']);
}

$decoded = json_decode($raw, true, 512, JSON_BIGINT_AS_STRING);
if ($decoded === null && json_last_error() !== JSON_ERROR_NONE) {
    // Capture the message now — log_event()/respond() call json_encode()
    // internally, which would otherwise reset this global error state.
    $jsonError = json_last_error_msg();
    log_event($config, 'INVALID_JSON', $ip, $client['name'], ['json_error' => $jsonError]);
    respond(400, ['error' => 'Malformed JSON: ' . $jsonError]);
}

$uploadDir = $config['upload_dir'] ?? (__DIR__ . '/../data/uploads');
if (!is_dir($uploadDir) || !is_writable($uploadDir)) {
    log_event($config, 'SERVER_ERROR', $ip, $client['name'], ['reason' => 'upload_dir not writable']);
    respond(500, ['error' => 'Server storage is not writable.']);
}

$safeClientName = preg_replace('/[^a-zA-Z0-9_-]/', '_', $client['name']);
$filename = sprintf('%s_%s_%s.json', date('Ymd_His'), bin2hex(random_bytes(4)), $safeClientName);
$destination = rtrim($uploadDir, '/') . '/' . $filename;

$tmpPath = $destination . '.tmp';
if (file_put_contents($tmpPath, $raw, LOCK_EX) === false || !rename($tmpPath, $destination)) {
    @unlink($tmpPath);
    log_event($config, 'WRITE_FAILED', $ip, $client['name']);
    respond(500, ['error' => 'Failed to save uploaded file.']);
}

log_event($config, 'UPLOAD_OK', $ip, $client['name'], ['file' => $filename, 'bytes' => strlen($raw)]);

respond(201, [
    'status' => 'ok',
    'file' => $filename,
    'bytes' => strlen($raw),
    'received_at' => date('c'),
]);
