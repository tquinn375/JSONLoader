create or replace Package         LOCAL_PROXCONVERT_P
as
type t_keymappinginfo is table of keymappinginfo%rowtype        --  list of keymapping changes - populated
        index by binary_integer;                                -- by row level trigger, applied by table level trigger (after change)
v_keymappinginfo t_keymappinginfo;
v_NumEntries BINARY_INTEGER := 0;
v_UpdatingKey Number := 0;
v_deletea1000prox number := 0;
end LOCAL_PROXCONVERT_P;
/
