-- Rollback to the 10-7-2026 versions (two triggers, array in the package):
--   @rollback.sql
whenever sqlerror exit failure rollback

DROP TRIGGER "DIEBOLD"."LOCAL_PROXCONVERT_C";

@@original/local_proxconvert_p.sql
@@original/local_proxconvert_trigger_r.sql
@@original/local_proxconvert_s.sql
