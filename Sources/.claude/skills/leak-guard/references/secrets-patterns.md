# Secret patterns

The `secret-in-source` rule fires on:

```
(api[_-]?key|password|secret|token|bearer)\s*=\s*"[A-Za-z0-9+/=]{16,}"
```

Variations covered:

- `apiKey="..."`, `api_key="..."`, `api-key="..."`
- `password="..."`
- `secret="..."`, `secretKey="..."`
- `token="..."`, `accessToken="..."`, `bearer="..."`

The 16-character minimum is a heuristic to skip placeholder / dev sentinels
(`password=""`, `password="x"`).

## What it does NOT catch (limitations)

- AWS / GCP / Azure key prefixes (`AKIA*`, `AIza*`, `AC*`) — would need
  per-vendor regex.
- Connection strings with embedded password (`Password=...;Server=...`).
- Secrets in non-`.cs` files (`.json`, `.yaml`, `.config`).
- Multi-line PEM blocks.

If a vendor-specific scanner becomes necessary, add patterns here AND extend
the regex in `leak-scan.ps1`.

## Whitelist

- Any line with comment `// fixture` is skipped.
- Files under `Tests/Fixtures/` are skipped.
- Add a project-specific whitelist via `// leak-ok: <reason>` on the line.

## What to do when a secret is flagged

1. **Stop the edit batch immediately.** Do not commit.
2. Move the value to:
   - User secrets (`dotnet user-secrets set <key> <value>`).
   - Environment variable (`Environment.GetEnvironmentVariable(...)`).
   - A config file outside source control (`appsettings.Local.json` in
     `.gitignore`).
3. If the secret is **already pushed**, treat it as compromised: rotate it
   immediately, then remove from history (`git filter-repo` or
   BFG Repo-Cleaner) and force-push (with user authorization).

## Out-of-scope

- This skill does not scan git history. Use `git-secrets` /
  `trufflehog` for retroactive audits.
- This skill does not call any external service. Detection is local-only.
