-- update 10-7-2026 update to capture info if mediatype is 060, 061, 064, 065.  Adding 064, 065
CREATE OR REPLACE TRIGGER "DIEBOLD"."LOCAL_PROXCONVERT_TRIGGER_R" 
BEFORE DELETE OR INSERT OR UPDATE
ON DIEBOLD.KEYMAPPINGINFO
REFERENCING NEW AS NEW OLD AS OLD
FOR EACH ROW
begin
        if deleting  and LOCAL_PROXCONVERT_P.v_deletea1000prox = 0 then
                if :old.mediatype = '061' or  :old.mediatype = '060' or :old.mediatype = '065' or  :old.mediatype = '064'then
                                LOCAL_PROXCONVERT_P.v_NumEntries := LOCAL_PROXCONVERT_P.v_NumEntries +1;
                                LOCAL_PROXCONVERT_P.v_keymappinginfo(LOCAL_PROXCONVERT_P.v_NumEntries).patronid := :old.PATRONID;
                                LOCAL_PROXCONVERT_P.v_keymappinginfo(LOCAL_PROXCONVERT_P.v_NumEntries).keyValue := :old.KEYVALUE;
                                LOCAL_PROXCONVERT_P.v_keymappinginfo(LOCAL_PROXCONVERT_P.v_NumEntries).mediatype := :old.mediatype;
                end if;
        end if;
        if inserting or updating then
                if :new.MEDIATYPE  = '060' or :new.MEDIATYPE  = '064' then
                                LOCAL_PROXCONVERT_P.v_NumEntries := LOCAL_PROXCONVERT_P.v_NumEntries +1;
                                LOCAL_PROXCONVERT_P.v_keymappinginfo(LOCAL_PROXCONVERT_P.v_NumEntries).patronid := :new.PATRONID;
                                LOCAL_PROXCONVERT_P.v_keymappinginfo(LOCAL_PROXCONVERT_P.v_NumEntries).keyValue := :new.KEYVALUE;
                                LOCAL_PROXCONVERT_P.v_keymappinginfo(LOCAL_PROXCONVERT_P.v_NumEntries).mediatype := :new.mediatype;
                                                -- dbms_output.put_line('in row trigger - pointer = '||to_char(LOCAL_PROXCONVERT_P.v_NumEntries)||', Media type is '||:new.mediatype );
                end if;
        end if;
end LOCAL_PROXCONVERT_TRIGGER_R;
/
