-- update 10-7-2026 add function to add mediatype 065 if mediatype 064 is added
create or replace TRIGGER "DIEBOLD"."LOCAL_PROXCONVERT_S" 
AFTER DELETE OR INSERT OR UPDATE
ON DIEBOLD.KEYMAPPINGINFO
declare
myrec keymappinginfo%rowtype;
TheCount        Number;
v_LoopIndex        BINARY_INTEGER;
DoUpdate        Number;
MyNumEntries        BINARY_INTEGER;
MyTempVarchar varchar2(500);
begin
        if LOCAL_PROXCONVERT_P.v_NumEntries > 0 then
                begin
                        MyNumEntries := LOCAL_PROXCONVERT_P.v_NumEntries;
                        for v_LoopIndex in REVERSE 1..MyNumEntries LOOP
                                -- dbms_output.put_line('in statement loop - loopcounter = '||to_char(v_loopindex)||', Media type is '||LOCAL_PROXCONVERT_P.v_keymappinginfo(v_LoopIndex).mediatype );
                                LOCAL_PROXCONVERT_P.v_NumEntries := LOCAL_PROXCONVERT_P.v_NumEntries - 1;
                                if INSERTING or UPDATING then
                                        -- dbms_output.put_line('in inserting/updating case - loopcounter = '||to_char(v_loopindex)||', Media type is '||LOCAL_PROXCONVERT_P.v_keymappinginfo(v_LoopIndex).mediatype );
                                        -- handle mediatype '060'
                                        if LOCAL_PROXCONVERT_P.v_keymappinginfo(v_LoopIndex).mediatype = '060' then
                                                select count(*) into TheCount from keymappinginfo a where a.patronid = LOCAL_PROXCONVERT_P.v_keymappinginfo(v_LoopIndex).patronid and a.mediatype = '061';
                                                if TheCount > 0 then        -- record already exists - update it
                                                        -- dbms_output.put_line('updating a1000 - loopcounter = '||to_char(v_loopindex));
                                                        select A1000PROXCVT(LOCAL_PROXCONVERT_P.v_keymappinginfo(v_LoopIndex).keyvalue) into MyTempVarchar from dual;
                                                        -- dbms_output.put_line('updating a1000 to '||MyTempVarchar);
                                                                update KEYMAPPINGINFO set keyvalue = (select A1000PROXCVT(LOCAL_PROXCONVERT_P.v_keymappinginfo(v_LoopIndex).keyvalue) from dual)
                                                                                where patronid = LOCAL_PROXCONVERT_P.v_keymappinginfo(v_LoopIndex).patronid and mediatype = '061';
                                                else
                                                        -- dbms_output.put_line('inserting a1000 - loopcounter = '||to_char(v_loopindex));
                                                                insert into KEYMAPPINGINFO ( keyvalue, patronid, mediatype, inst ) (select A1000PROXCVT(LOCAL_PROXCONVERT_P.v_keymappinginfo(v_LoopIndex).keyvalue),
                                                                                LOCAL_PROXCONVERT_P.v_keymappinginfo(v_LoopIndex).patronid, '061', -1 from dual );
                                                end if;
                                        end if;
                                        -- handle mediatype '064'
                                        if LOCAL_PROXCONVERT_P.v_keymappinginfo(v_LoopIndex).mediatype = '064' then
                                                select count(*) into TheCount from keymappinginfo a where a.patronid = LOCAL_PROXCONVERT_P.v_keymappinginfo(v_LoopIndex).patronid and a.mediatype = '065';
                                                if TheCount > 0 then        -- record already exists - update it
                                                        -- dbms_output.put_line('updating a1000 - loopcounter = '||to_char(v_loopindex));
                                                        select LOCAL_A1000PROXCVT_16(LOCAL_PROXCONVERT_P.v_keymappinginfo(v_LoopIndex).keyvalue) into MyTempVarchar from dual;
                                                        -- dbms_output.put_line('updating a1000 to '||MyTempVarchar);
                                                                update KEYMAPPINGINFO set keyvalue = (select LOCAL_A1000PROXCVT_16(LOCAL_PROXCONVERT_P.v_keymappinginfo(v_LoopIndex).keyvalue) from dual)
                                                                                where patronid = LOCAL_PROXCONVERT_P.v_keymappinginfo(v_LoopIndex).patronid and mediatype = '065';
                                                else
                                                        -- dbms_output.put_line('inserting a1000 - loopcounter = '||to_char(v_loopindex));
                                                                insert into KEYMAPPINGINFO ( keyvalue, patronid, mediatype, inst ) (select LOCAL_A1000PROXCVT_16(LOCAL_PROXCONVERT_P.v_keymappinginfo(v_LoopIndex).keyvalue),
                                                                                LOCAL_PROXCONVERT_P.v_keymappinginfo(v_LoopIndex).patronid, '065', -1 from dual );
                                                end if;
                                        end if;
                                end if;
                                 if deleTING then
                                 		-- handle 060/061
                                        if LOCAL_PROXCONVERT_P.v_keymappinginfo(v_LoopIndex).mediatype = '060' then  -- delete the a1000 media type as well
                                                select count(*) into TheCount from keymappinginfo a where a.patronid = LOCAL_PROXCONVERT_P.v_keymappinginfo(v_LoopIndex).patronid and a.mediatype = '061';
                                                if TheCount > 0 then        -- record already exists - delete it
                                                        -- dbms_output.put_line('deleting a1000 - loopcounter = '||to_char(v_loopindex));
                                                         LOCAL_PROXCONVERT_P.v_deletea1000prox :=1;
                                                        delete  keymappinginfo a where a.patronid = LOCAL_PROXCONVERT_P.v_keymappinginfo(v_LoopIndex).patronid and a.mediatype = '061';
                                                         LOCAL_PROXCONVERT_P.v_deletea1000prox :=0;
                                                        -- dbms_output.put_line('deleted a1000 - loopcounter = '||to_char(v_loopindex));
                                                end if;
                                        elsif LOCAL_PROXCONVERT_P.v_keymappinginfo(v_LoopIndex).mediatype = '061'  and LOCAL_PROXCONVERT_P.v_deletea1000prox = 0 then  --  put it back - the only way to delete the a1000 prox media type is to delete the dac prox type
                                                select count(*) into TheCount from keymappinginfo a where a.patronid = LOCAL_PROXCONVERT_P.v_keymappinginfo(v_LoopIndex).patronid and a.mediatype = '060';
                                                        if TheCount > 0 then        -- There is a DAC record, don't let the A1000 record go away...
                                                        select count(*) into TheCount from keymappinginfo a where a.patronid = LOCAL_PROXCONVERT_P.v_keymappinginfo(v_LoopIndex).patronid and a.mediatype = '061';
                                                        if TheCount = 0 then        -- If A1000 record was deleted - put it back
                                                                insert into KEYMAPPINGINFO ( keyvalue, patronid, mediatype, inst )  values (LOCAL_PROXCONVERT_P.v_keymappinginfo(v_LoopIndex).keyvalue,
                                                                                                LOCAL_PROXCONVERT_P.v_keymappinginfo(v_LoopIndex).patronid, LOCAL_PROXCONVERT_P.v_keymappinginfo(v_LoopIndex).mediatype, -1 );
                                                                -- dbms_output.put_line('inserting a1000 on attempted delete- loopcounter = '||to_char(v_loopindex));
                                                        end if;
                                                end if;
                                        end if;
                                        -- handle 064/065
                                        if LOCAL_PROXCONVERT_P.v_keymappinginfo(v_LoopIndex).mediatype = '064' then  -- delete the a1000 media type as well
                                                select count(*) into TheCount from keymappinginfo a where a.patronid = LOCAL_PROXCONVERT_P.v_keymappinginfo(v_LoopIndex).patronid and a.mediatype = '065';
                                                if TheCount > 0 then        -- record already exists - delete it
                                                        -- dbms_output.put_line('deleting a1000 - loopcounter = '||to_char(v_loopindex));
                                                         LOCAL_PROXCONVERT_P.v_deletea1000prox :=1;
                                                        delete  keymappinginfo a where a.patronid = LOCAL_PROXCONVERT_P.v_keymappinginfo(v_LoopIndex).patronid and a.mediatype = '065';
                                                         LOCAL_PROXCONVERT_P.v_deletea1000prox :=0;
                                                        -- dbms_output.put_line('deleted a1000 - loopcounter = '||to_char(v_loopindex));
                                                end if;
                                        elsif LOCAL_PROXCONVERT_P.v_keymappinginfo(v_LoopIndex).mediatype = '065'  and LOCAL_PROXCONVERT_P.v_deletea1000prox = 0 then  --  put it back - the only way to delete the a1000 prox media type is to delete the dac prox type
                                                select count(*) into TheCount from keymappinginfo a where a.patronid = LOCAL_PROXCONVERT_P.v_keymappinginfo(v_LoopIndex).patronid and a.mediatype = '064';
                                                        if TheCount > 0 then        -- There is a DAC record, don't let the A1000 record go away...
                                                        select count(*) into TheCount from keymappinginfo a where a.patronid = LOCAL_PROXCONVERT_P.v_keymappinginfo(v_LoopIndex).patronid and a.mediatype = '065';
                                                        if TheCount = 0 then        -- If A1000 record was deleted - put it back
                                                                insert into KEYMAPPINGINFO ( keyvalue, patronid, mediatype, inst )  values (LOCAL_PROXCONVERT_P.v_keymappinginfo(v_LoopIndex).keyvalue,
                                                                                                LOCAL_PROXCONVERT_P.v_keymappinginfo(v_LoopIndex).patronid, LOCAL_PROXCONVERT_P.v_keymappinginfo(v_LoopIndex).mediatype, -1 );
                                                                -- dbms_output.put_line('inserting a1000 on attempted delete- loopcounter = '||to_char(v_loopindex));
                                                        end if;
                                                end if;
                                        end if;
                                end if;
                         END loop;
                        LOCAL_PROXCONVERT_P.v_NumEntries := 0;
                        end;
        end if;
end LOCAL_PROXCONVERT_S;
/
