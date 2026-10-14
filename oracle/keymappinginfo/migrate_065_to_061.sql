-- One-time migration for the 10-14-2026 change: 064 now calculates into 061, 065 is retired.
-- Run AFTER the new LOCAL_PROXCONVERT_C is compiled. Review the output, then COMMIT or ROLLBACK.

prompt == patrons with BOTH 060 and 064 (should be none; resolve these by hand first)
select patronid from keymappinginfo where mediatype = '060'
intersect
select patronid from keymappinginfo where mediatype = '064';

prompt == recalculate 061 for every 064 patron
-- Re-saving the 064 keyvalue makes the trigger create or update 061 from it.
update keymappinginfo set keyvalue = keyvalue where mediatype = '064';

prompt == remove 065 (the trigger no longer manages it)
delete keymappinginfo where mediatype = '065';

prompt == check: 064 patrons without a 061 (expect none)
select k.patronid from keymappinginfo k
 where k.mediatype = '064'
   and not exists (select 1 from keymappinginfo c where c.patronid = k.patronid and c.mediatype = '061');

prompt == review the results above, then COMMIT or ROLLBACK
