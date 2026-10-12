-- Deploy (SQL*Plus / SQL Developer "Run Script", connected as DIEBOLD):
--   @deploy.sql
-- Replaces LOCAL_PROXCONVERT_TRIGGER_R + LOCAL_PROXCONVERT_S with LOCAL_PROXCONVERT_C.
-- Both old triggers must be dropped, otherwise every change is processed twice.
whenever sqlerror exit failure rollback

@@local_proxconvert_p.sql

DROP TRIGGER "DIEBOLD"."LOCAL_PROXCONVERT_TRIGGER_R";
DROP TRIGGER "DIEBOLD"."LOCAL_PROXCONVERT_S";

@@local_proxconvert_c.sql

show errors trigger LOCAL_PROXCONVERT_C

select object_name, object_type, status from user_objects
 where object_name in ('LOCAL_PROXCONVERT_P', 'LOCAL_PROXCONVERT_C');
