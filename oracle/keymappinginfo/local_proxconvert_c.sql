-- update 10-12-2026: replaces LOCAL_PROXCONVERT_TRIGGER_R (before each row) and
-- LOCAL_PROXCONVERT_S (after statement) with a single compound trigger.
--
-- Media type pairs (source -> calculated):
--     060 -> 061   keyvalue = A1000PROXCVT(060 keyvalue)
--     064 -> 065   keyvalue = LOCAL_A1000PROXCVT_16(064 keyvalue)
--
-- Behaviour per pair (the two pairs never affect each other):
--   * insert/update of a source row  -> create or update its calculated row
--   * delete of a source row         -> delete its calculated row as well
--   * delete of a calculated row     -> put it back if its source row still exists
--
-- The list of changes is declared in this trigger, not in a package, so each
-- statement gets a fresh empty list and nothing left over from an earlier
-- (failed or nested) statement can be replayed.
CREATE OR REPLACE TRIGGER "DIEBOLD"."LOCAL_PROXCONVERT_C"
FOR DELETE OR INSERT OR UPDATE
ON DIEBOLD.KEYMAPPINGINFO
COMPOUND TRIGGER

        TYPE t_change IS RECORD (
                action    CHAR(1),                              -- 'U' = insert/update, 'D' = delete
                patronid  keymappinginfo.patronid%TYPE,
                keyvalue  keymappinginfo.keyvalue%TYPE,
                mediatype keymappinginfo.mediatype%TYPE
        );
        TYPE t_changes IS TABLE OF t_change INDEX BY PLS_INTEGER;
        g_changes t_changes;

        -- source media type of the pair p_mediatype belongs to, or null if it is not a managed type
        FUNCTION source_type(p_mediatype IN VARCHAR2) RETURN VARCHAR2 IS
        BEGIN
                RETURN CASE WHEN p_mediatype IN ('060', '061') THEN '060'
                            WHEN p_mediatype IN ('064', '065') THEN '064' END;
        END source_type;

        -- calculated media type of the pair p_mediatype belongs to, or null if it is not a managed type
        FUNCTION calc_type(p_mediatype IN VARCHAR2) RETURN VARCHAR2 IS
        BEGIN
                RETURN CASE WHEN p_mediatype IN ('060', '061') THEN '061'
                            WHEN p_mediatype IN ('064', '065') THEN '065' END;
        END calc_type;

        FUNCTION convert_key(p_calc_type IN VARCHAR2, p_keyvalue IN VARCHAR2) RETURN VARCHAR2 IS
        BEGIN
                IF p_calc_type = '061' THEN
                        RETURN A1000PROXCVT(p_keyvalue);
                ELSE
                        RETURN LOCAL_A1000PROXCVT_16(p_keyvalue);
                END IF;
        END convert_key;

        FUNCTION cascade_delete_active(p_calc_type IN VARCHAR2) RETURN BOOLEAN IS
        BEGIN
                IF p_calc_type = '061' THEN
                        RETURN LOCAL_PROXCONVERT_P.v_deletea1000prox = 1;
                ELSE
                        RETURN LOCAL_PROXCONVERT_P.v_delete065prox = 1;
                END IF;
        END cascade_delete_active;

        PROCEDURE set_cascade_delete(p_calc_type IN VARCHAR2, p_value IN NUMBER) IS
        BEGIN
                IF p_calc_type = '061' THEN
                        LOCAL_PROXCONVERT_P.v_deletea1000prox := p_value;
                ELSE
                        LOCAL_PROXCONVERT_P.v_delete065prox := p_value;
                END IF;
        END set_cascade_delete;

        PROCEDURE remember(p_action IN CHAR, p_patronid IN keymappinginfo.patronid%TYPE,
                           p_keyvalue IN keymappinginfo.keyvalue%TYPE, p_mediatype IN keymappinginfo.mediatype%TYPE) IS
                i PLS_INTEGER := g_changes.COUNT + 1;
        BEGIN
                g_changes(i).action    := p_action;
                g_changes(i).patronid  := p_patronid;
                g_changes(i).keyvalue  := p_keyvalue;
                g_changes(i).mediatype := p_mediatype;
        END remember;

        -- source row inserted/updated: create or update its calculated row
        PROCEDURE apply_upsert(c IN t_change) IS
                v_calc   keymappinginfo.mediatype%TYPE := calc_type(c.mediatype);
                v_newkey keymappinginfo.keyvalue%TYPE  := convert_key(v_calc, c.keyvalue);
        BEGIN
                UPDATE keymappinginfo SET keyvalue = v_newkey
                 WHERE patronid = c.patronid AND mediatype = v_calc;
                IF SQL%ROWCOUNT = 0 THEN
                        INSERT INTO keymappinginfo ( keyvalue, patronid, mediatype, inst )
                        VALUES ( v_newkey, c.patronid, v_calc, -1 );
                END IF;
        END apply_upsert;

        PROCEDURE apply_delete(c IN t_change) IS
                v_src   keymappinginfo.mediatype%TYPE := source_type(c.mediatype);
                v_calc  keymappinginfo.mediatype%TYPE := calc_type(c.mediatype);
                v_count NUMBER;
        BEGIN
                IF c.mediatype = v_src THEN
                        -- source row deleted: delete its calculated row as well
                        set_cascade_delete(v_calc, 1);
                        BEGIN
                                DELETE keymappinginfo WHERE patronid = c.patronid AND mediatype = v_calc;
                        EXCEPTION
                                WHEN OTHERS THEN
                                        set_cascade_delete(v_calc, 0);
                                        RAISE;
                        END;
                        set_cascade_delete(v_calc, 0);
                ELSE
                        -- calculated row deleted directly: the only way to delete it is to delete
                        -- its source row, so put it back while the source row still exists
                        SELECT COUNT(*) INTO v_count FROM keymappinginfo
                         WHERE patronid = c.patronid AND mediatype = v_src;
                        IF v_count > 0 THEN
                                SELECT COUNT(*) INTO v_count FROM keymappinginfo
                                 WHERE patronid = c.patronid AND mediatype = v_calc;
                                IF v_count = 0 THEN
                                        INSERT INTO keymappinginfo ( keyvalue, patronid, mediatype, inst )
                                        VALUES ( c.keyvalue, c.patronid, v_calc, -1 );
                                END IF;
                        END IF;
                END IF;
        END apply_delete;

AFTER EACH ROW IS
BEGIN
        IF DELETING THEN
                -- skip calculated rows this trigger is deleting itself (cascade from a source delete)
                IF source_type(:old.mediatype) IS NOT NULL
                   AND NOT (:old.mediatype = calc_type(:old.mediatype) AND cascade_delete_active(:old.mediatype)) THEN
                        remember('D', :old.patronid, :old.keyvalue, :old.mediatype);
                END IF;
        ELSIF :new.mediatype IN ('060', '064') THEN
                remember('U', :new.patronid, :new.keyvalue, :new.mediatype);
        END IF;
END AFTER EACH ROW;

AFTER STATEMENT IS
BEGIN
        FOR i IN REVERSE 1 .. g_changes.COUNT LOOP
                IF g_changes(i).action = 'U' THEN
                        apply_upsert(g_changes(i));
                ELSE
                        apply_delete(g_changes(i));
                END IF;
        END LOOP;
        g_changes.DELETE;
END AFTER STATEMENT;

END LOCAL_PROXCONVERT_C;
/
