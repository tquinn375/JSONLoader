# JSONLoader

A small PHP endpoint that accepts JSON file uploads from a fixed set of
authorized servers, over Apache. Authentication is a per-client API key
(sent as a header, checked as a SHA-256 hash server-side); each key is also
locked to an IP/CIDR allowlist for defense in depth.

## Layout

```
public/           Web root — point Apache's DocumentRoot here
  upload.php        The upload endpoint
  .htaccess          Disables directory listing
src/
  Auth.php           Auth, IP-allowlist, and logging helpers
config/
  config.example.php Template — copy to config.php and edit
  config.php         Real config (gitignored, holds key hashes)
data/uploads/       Where accepted JSON files are written (gitignored)
logs/               Line-delimited JSON audit log (gitignored)
tools/
  generate_key.php   CLI: generates an API key + its SHA-256 hash
```

Every directory other than `public/` also ships a `.htaccess` denying all
web access, in case Apache's `DocumentRoot` ends up pointing above `public/`
(e.g. on shared hosting). Still prefer setting `DocumentRoot` to `public/` —
that's the real boundary; the `.htaccess` files are a backstop.

## Setup

1. **Point Apache at `public/`.** Example vhost:

   ```apache
   <VirtualHost *:443>
       ServerName uploads.example.com
       DocumentRoot /var/www/JSONLoader/public

       <Directory /var/www/JSONLoader/public>
           AllowOverride All
           Require all granted
       </Directory>

       # TLS is required — API keys must never travel over plain HTTP.
       SSLEngine on
       SSLCertificateFile      /etc/letsencrypt/live/uploads.example.com/fullchain.pem
       SSLCertificateKeyFile   /etc/letsencrypt/live/uploads.example.com/privkey.pem
   </VirtualHost>
   ```

   Requires `mod_rewrite`/`mod_authz_core` enabled and `AllowOverride All` (or
   at least `AllowOverride Limit AuthConfig Indexes`) so the `.htaccess`
   files take effect.

2. **Create the config file:**

   ```bash
   cp config/config.example.php config/config.php
   ```

3. **Generate an API key per client server:**

   ```bash
   php tools/generate_key.php
   ```

   This prints the raw key (give this to the uploading server — it's shown
   only once) and its SHA-256 hash (paste that into `config/config.php`).

4. **Edit `config/config.php`:** add one entry per client under `clients`,
   with its `key_hash` and its `allowed_ips` (bare IPs or CIDR ranges).

5. **Set permissions** so Apache's user can write uploads and logs, e.g.:

   ```bash
   sudo chown -R www-data:www-data data/uploads logs
   ```

6. Set `upload_max_filesize` / `post_max_size` in `php.ini` to match (or
   exceed) `max_upload_bytes` in `config.php`.

## Uploading

Send the JSON as the raw POST body with the API key in `X-API-Key`:

```bash
curl -X POST https://uploads.example.com/upload.php \
  -H "X-API-Key: <the raw key from generate_key.php>" \
  -H "Content-Type: application/json" \
  --data-binary @payload.json
```

A multipart file field named `file` also works:

```bash
curl -X POST https://uploads.example.com/upload.php \
  -H "X-API-Key: <the raw key>" \
  -F "file=@payload.json"
```

Responses:

| Status | Meaning |
|--------|---------|
| 201 | Saved. Body includes the stored filename. |
| 400 | Missing body or malformed JSON. |
| 401 | Missing or invalid API key. |
| 403 | Valid key, but source IP isn't in that key's `allowed_ips`. |
| 405 | Non-POST request. |
| 413 | Body exceeds `max_upload_bytes`. |
| 500 | Server misconfiguration (missing config, unwritable storage). |

## Notes

- Validation is currently syntax-only (`json_decode` must succeed). If you
  need structural validation (required fields, types), add a JSON Schema
  check in `public/upload.php` before the file is written.
- `logs/upload.log` records every auth failure, IP denial, and successful
  upload as one JSON object per line — useful for fail2ban or alerting on
  repeated `AUTH_FAILED`/`IP_DENIED` events from the same IP.
- Keys are stored and compared as SHA-256 hashes (`hash_equals`, timing-safe)
  so a leaked `config.php` doesn't hand over live credentials outright —
  but treat it as a secret file regardless.
- Rotate a client's key by generating a new one and swapping the hash;
  nothing else needs to change on your end.
