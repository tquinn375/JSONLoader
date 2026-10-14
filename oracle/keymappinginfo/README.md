# KEYMAPPINGINFO prox-conversion trigger

Maintains the calculated media type 061 on `DIEBOLD.KEYMAPPINGINFO`:

| Source | Calculated | Conversion              |
|--------|------------|-------------------------|
| 060    | 061        | `A1000PROXCVT`          |
| 064    | 061        | `LOCAL_A1000PROXCVT_16` |

065 is no longer used. Only one source type may exist per patron: adding 060 removes
that patron's 064 row, adding 064 removes their 060 row, and either removes a leftover
065. Inserting or updating a source row creates or updates 061. Deleting a source row
also deletes 061. A 061 row deleted on its own is put back while a 060 or 064 row still
exists.

## Why the change

The old design kept a list of pending changes in package variables
(`LOCAL_PROXCONVERT_P.v_keymappinginfo` / `v_NumEntries`). Those variables last for the
whole database session. If a statement failed after the row trigger had recorded an
entry, or if the statement trigger's own DML fired the triggers again, entries could be
left behind or overwritten, and a later statement in the same session could replay them.

`LOCAL_PROXCONVERT_C` is a compound trigger. Its change list lives inside the trigger, so
every statement starts with an empty list, and Oracle discards the list when the
statement ends or fails. `LOCAL_PROXCONVERT_P` now holds only the cascade-delete guard
for 061.

Note: the 060/061 rows that reappeared after deleting all four media types and adding
064 were not caused by the triggers. An external feed was restoring the values it
considered correct. Check that feed before changing these media types by hand.

## Files

- `local_proxconvert_p.sql`: package (guards; legacy members kept so other code still compiles)
- `local_proxconvert_c.sql`: compound trigger, replaces `LOCAL_PROXCONVERT_TRIGGER_R` and `LOCAL_PROXCONVERT_S`
- `migrate_065_to_061.sql`: one-time move of existing 064 patrons from 065 to 061
- `deploy.sql`: creates the package, drops the two old triggers, creates the new trigger
- `rollback.sql`: drops the new trigger and restores the 10-7-2026 versions from `original/`
- `test_local_proxconvert.sql`: manual check against one patron, rolls itself back

Requires Oracle 11g or later (compound triggers).
