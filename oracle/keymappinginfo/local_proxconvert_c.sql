-- update 10-14-2026 (b): both source types now store their calculated value in 061;
-- 065 is no longer used. Only one source type may exist per patron.
-- update 10-14-2026: only one pair may exist per patron.
-- update 10-12-2026: replaces LOCAL_PROXCONVERT_TRIGGER_R (before each row) and
-- LOCAL_PROXCONVERT_S (after statement) with a single compound trigger.
--
-- Source media types (both calculate into 061):
--     060 -> 061   keyvalue = A1000PROXCVT(060 keyvalue)
--     064 -> 061   keyvalue = LOCAL_A1000PROXCVT_16(064 keyvalue)
--
-- Behaviour:
--   * insert of a source row      -> delete the patron's other source type (and any
--                                    leftover 065), then create/update 061 from this row
--   * update of a source row      -> recalculate 061
--   * delete of a source row      -> delete 061 as well
--   * delete of 061 on its own    -> put it back while a 060 or 064 row still exists
--
-- The list of changes is declared in this trigger, not in a package, so each
-- statement gets a fresh empty list and nothing left over from an earlier
-- (failed or nested) statement can be replayed.
CREATE OR REPLACE TRIGGER "DIEBOLD"."LOCAL_PROXCONVERT_C"
FOR DELETE OR INSERT OR UPDATE
ON DIEBOLD.KEYMAPPINGINFO
COMPOUND TRIGGER

        TYPE t_change IS RECORD (
                action    CHAR(1),                              -- 'I' = added, 'U' = updated, 'D' = deleted
                patronid  keymappinginfo.patronid%TYPE,
                keyvalue  keymappinginfo.keyvalue%TYPE,
                mediatype keymappinginfo.mediatype%TYPE
        );
        TYPE t_changes IS TABLE OF t_change INDEX BY PLS_INTEGER;
        g_changes t_changes;

        FUNCTION convert_key(p_source_type IN VARCHAR2, p_keyvalue IN VARCHAR2) RETURN VARCHAR2 IS
        BEGIN
                IF p_source_type = '060' THEN
                        RETURN A1000PROXCVT(p_keyvalue);
                ELSE
                        RETURN LOCAL_A1000PROXCVT_16(p_keyvalue);
                END IF;
        END convert_key;

        PROCEDURE remember(p_action IN CHAR, p_patronid IN keymappinginfo.patronid%TYPE,
                           p_keyvalue IN keymappinginfo.keyvalue%TYPE, p_mediatype IN keymappinginfo.mediatype%TYPE) IS
                i PLS_INTEGER := g_changes.COUNT + 1;
        BEGIN
                g_changes(i).action    := p_action;
                g_changes(i).patronid  := p_patronid;
                g_changes(i).keyvalue  := p_keyvalue;
                g_changes(i).mediatype := p_mediatype;
        END remember;

        -- source row added: only one source type may exist per patron, so remove the
        -- other one. Deleting it fires this trigger again, which deletes the old 061;
        -- apply_upsert then creates a new 061 from this row. 065 is no longer used, so
        -- any leftover 065 row is removed as well.
        PROCEDURE remove_other_source(c IN t_change) IS
        BEGIN
                DELETE keymappinginfo
                 WHERE patronid = c.patronid
                   AND mediatype IN ('060', '064', '065')
                   AND mediatype <> c.mediatype;
        END remove_other_source;

        -- source row inserted/updated: create or update 061
        PROCEDURE apply_upsert(c IN t_change) IS
                v_newkey keymappinginfo.keyvalue%TYPE := convert_key(c.mediatype, c.keyvalue);
        BEGIN
                IF c.action = 'I' THEN
                        remove_other_source(c);
                END IF;
                UPDATE keymappinginfo SET keyvalue = v_newkey
                 WHERE patronid = c.patronid AND mediatype = '061';
                IF SQL%ROWCOUNT = 0 THEN
                        INSERT INTO keymappinginfo ( keyvalue, patronid, mediatype, inst )
                        VALUES ( v_newkey, c.patronid, '061', -1 );
                END IF;
        END apply_upsert;

        PROCEDURE apply_delete(c IN t_change) IS
                v_count NUMBER;
        BEGIN
                IF c.mediatype IN ('060', '064') THEN
                        -- source row deleted: delete 061 as well
                        LOCAL_PROXCONVERT_P.v_deletea1000prox := 1;
                        BEGIN
                                DELETE keymappinginfo WHERE patronid = c.patronid AND mediatype = '061';
                        EXCEPTION
                                WHEN OTHERS THEN
                                        LOCAL_PROXCONVERT_P.v_deletea1000prox := 0;
                                        RAISE;
                        END;
                        LOCAL_PROXCONVERT_P.v_deletea1000prox := 0;
                ELSE
                        -- 061 deleted directly: the only way to delete it is to delete its
                        -- source row, so put it back while a source row still exists
                        SELECT COUNT(*) INTO v_count FROM keymappinginfo
                         WHERE patronid = c.patronid AND mediatype IN ('060', '064');
                        IF v_count > 0 THEN
                                SELECT COUNT(*) INTO v_count FROM keymappinginfo
                                 WHERE patronid = c.patronid AND mediatype = '061';
                                IF v_count = 0 THEN
                                        INSERT INTO keymappinginfo ( keyvalue, patronid, mediatype, inst )
                                        VALUES ( c.keyvalue, c.patronid, '061', -1 );
                                END IF;
                        END IF;
                END IF;
        END apply_delete;

AFTER EACH ROW IS
BEGIN
        IF DELETING THEN
                -- skip 061 rows this trigger is deleting itself (cascade from a source delete)
                IF :old.mediatype IN ('060', '064')
                   OR (:old.mediatype = '061' AND LOCAL_PROXCONVERT_P.v_deletea1000prox = 0) THEN
                        remember('D', :old.patronid, :old.keyvalue, :old.mediatype);
                END IF;
        ELSIF :new.mediatype IN ('060', '064') THEN
                -- an update that changes a row into 060/064 counts as adding it
                IF INSERTING OR NVL(:old.mediatype, '-') <> :new.mediatype THEN
                        remember('I', :new.patronid, :new.keyvalue, :new.mediatype);
                ELSE
                        remember('U', :new.patronid, :new.keyvalue, :new.mediatype);
                END IF;
        END IF;
END AFTER EACH ROW;

AFTER STATEMENT IS
BEGIN
        FOR i IN REVERSE 1 .. g_changes.COUNT LOOP
                IF g_changes(i).action IN ('I', 'U') THEN
                        apply_upsert(g_changes(i));
                ELSE
                        apply_delete(g_changes(i));
                END IF;
        END LOOP;
        g_changes.DELETE;
END AFTER STATEMENT;

END LOCAL_PROXCONVERT_C;
/
