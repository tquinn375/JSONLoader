-- update 10-12-2026: the pending-change list no longer lives here.
-- It used to be a session-level array (v_keymappinginfo / v_NumEntries) that
-- outlived the statement that filled it, so a failed or nested statement could
-- leave old 060/064 entries behind for the next statement to replay.
-- The list now lives inside the compound trigger LOCAL_PROXCONVERT_C, which
-- gives every statement its own private copy that is thrown away when the
-- statement ends (or fails).
--
-- What remains here are the cascade-delete guards, one per media type pair,
-- so the 060/061 pair and the 064/065 pair are handled independently.
create or replace Package         LOCAL_PROXCONVERT_P
as
-- Set to 1 by LOCAL_PROXCONVERT_C while it deletes a calculated row because its
-- source row was deleted; the deleted calculated row is then not "put back".
v_deletea1000prox number := 0;          -- guards 061 (calculated from 060 or 064)
v_delete065prox   number := 0;          -- no longer used: 065 was retired 10-14-2026

-- Legacy members, no longer used by the triggers. Kept so that any other
-- object that references them still compiles.
type t_keymappinginfo is table of keymappinginfo%rowtype
        index by binary_integer;
v_keymappinginfo t_keymappinginfo;
v_NumEntries BINARY_INTEGER := 0;
v_UpdatingKey Number := 0;
end LOCAL_PROXCONVERT_P;
/
