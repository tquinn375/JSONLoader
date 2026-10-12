-- Manual check of LOCAL_PROXCONVERT_C. Uses one existing patron and rolls
-- everything back at the end. Run in SQL*Plus / SQL Developer as DIEBOLD:
--   @test_local_proxconvert.sql
-- If KEYMAPPINGINFO has other NOT NULL columns, add them to the inserts.
set serveroutput on
define patron = &patronid_to_test_with

savepoint before_proxconvert_test;

prompt == start: remove every managed media type for the patron
delete keymappinginfo where patronid = &patron and mediatype in ('060','061','064','065');
select mediatype, keyvalue from keymappinginfo where patronid = &patron and mediatype in ('060','061','064','065') order by 1;

prompt == add 064 only: expect 064 and 065, and NO 060/061
insert into keymappinginfo ( keyvalue, patronid, mediatype, inst ) values ( '12345', &patron, '064', -1 );
select mediatype, keyvalue from keymappinginfo where patronid = &patron and mediatype in ('060','061','064','065') order by 1;

prompt == add 060: expect 060/061 added, 064/065 unchanged
insert into keymappinginfo ( keyvalue, patronid, mediatype, inst ) values ( '67890', &patron, '060', -1 );
select mediatype, keyvalue from keymappinginfo where patronid = &patron and mediatype in ('060','061','064','065') order by 1;

prompt == delete 065 directly: expect it to be put back (064 still exists)
delete keymappinginfo where patronid = &patron and mediatype = '065';
select mediatype, keyvalue from keymappinginfo where patronid = &patron and mediatype in ('060','061','064','065') order by 1;

prompt == delete 060: expect 060 and 061 gone, 064/065 unchanged
delete keymappinginfo where patronid = &patron and mediatype = '060';
select mediatype, keyvalue from keymappinginfo where patronid = &patron and mediatype in ('060','061','064','065') order by 1;

rollback to savepoint before_proxconvert_test;
prompt == test changes rolled back
