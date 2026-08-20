<?php
declare(strict_types=1);

/**
 * CLI helper: generates a new API key and its SHA-256 hash.
 * Usage: php tools/generate_key.php
 */
if (PHP_SAPI !== 'cli') {
    http_response_code(403);
    exit('This script may only be run from the command line.');
}

$key = bin2hex(random_bytes(32));
$hash = hash('sha256', $key);

echo "API key (give this to the client server, shown only once):\n{$key}\n\n";
echo "SHA-256 hash (paste this into config/config.php as 'key_hash'):\n{$hash}\n";
