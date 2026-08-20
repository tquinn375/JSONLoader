<?php
declare(strict_types=1);

/**
 * Copy this file to config.php and fill in real values.
 * config.php is gitignored — never commit real API key hashes or paths.
 *
 * Generate a key + hash pair with: php tools/generate_key.php
 */
return [
    'clients' => [
        // Key = an internal label for this source server, used in logs.
        'example-server' => [
            // SHA-256 hash of the API key (never store the raw key here).
            'key_hash' => 'PASTE_SHA256_HASH_HERE',
            // IPs or CIDR ranges this client is allowed to upload from.
            'allowed_ips' => [
                '203.0.113.10',
                '203.0.113.0/24',
            ],
        ],
    ],

    // Reject bodies larger than this (bytes). Also set upload_max_filesize /
    // post_max_size in php.ini to something sane.
    'max_upload_bytes' => 5 * 1024 * 1024,

    // Where accepted JSON files are written. Must be outside the web root,
    // or protected by .htaccess (see data/.htaccess).
    'upload_dir' => __DIR__ . '/../data/uploads',

    // Line-delimited JSON log of auth failures, IP denials, and uploads.
    'log_file' => __DIR__ . '/../logs/upload.log',

    // Only trust X-Forwarded-For when it comes directly from one of these
    // reverse-proxy/load-balancer IPs. Leave empty if Apache is public-facing.
    'trusted_proxies' => [],
];
