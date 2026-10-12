# KEYMAPPINGINFO prox-conversion trigger

Maintains calculated media types on `DIEBOLD.KEYMAPPINGINFO`:

| Source | Calculated | Conversion              |
|--------|------------|-------------------------|
| 060    | 061        | `A1000PROXCVT`          |
| 064    | 065        | `LOCAL_A1000PROXCVT_16` |

Each pair is handled on its own. Inserting or updating a source row creates or updates its
calculated row. Deleting a source row also deletes its calculated row. A calculated row
deleted on its own is put back while its source row still exists.

## Why the change

The old design kept a list of pending changes in package variables
(`LOCAL_PROXCONVERT_P.v_keymappinginfo` / `v_NumEntries`). Those variables last for the
whole database session. If a statement failed after the row trigger had recorded an
entry, or if the statement trigger's own DML fired the triggers again, entries were left
behind or overwritten. The next statement in the same session then replayed the stale
060/061 entries. That is how 061 came back when only 064 was inserted.

`LOCAL_PROXCONVERT_C` is a compound trigger. Its change list lives inside the trigger, so
every statement starts with an empty list, and Oracle discards the list when the
statement ends or fails. `LOCAL_PROXCONVERT_P` now holds only the cascade-delete guards,
one per pair.

## Files

- `local_proxconvert_p.sql`: package (guards; legacy members kept so other code still compiles)
- `local_proxconvert_c.sql`: compound trigger, replaces `LOCAL_PROXCONVERT_TRIGGER_R` and `LOCAL_PROXCONVERT_S`
- `deploy.sql`: creates the package, drops the two old triggers, creates the new trigger
- `rollback.sql`: drops the new trigger and restores the 10-7-2026 versions from `original/`
- `test_local_proxconvert.sql`: manual check against one patron, rolls itself back

Requires Oracle 11g or later (compound triggers).
