-- CLASSICTRIAL: give CORETEAM the FACT_JAYA2027 (Neptune Tea) units, same as on CLASSICDEV.
-- Without a UNIT on any CORETEAM / FACT_JAYA2027 row of USER_SCHEMA_LINK, every HS screen says
-- "No unit is available to you". On CLASSICDEV the two rows below carry JAYB / JAYF; on
-- CLASSICTRIAL their UNIT is blank. Updates only -- no new rows.

UPDATE CLASSICNEW.USER_SCHEMA_LINK
   SET UNIT = 'JAYB'
 WHERE UPPER(USER_ORA) = 'CORETEAM' AND USR_ID = 'FACT_JAYA2027'
   AND MODULE_NAME = 'Green Leaf Procurement & Production' AND TRIM(UNIT) IS NULL;

UPDATE CLASSICNEW.USER_SCHEMA_LINK
   SET UNIT = 'JAYF'
 WHERE UPPER(USER_ORA) = 'CORETEAM' AND USR_ID = 'FACT_JAYA2027'
   AND MODULE_NAME = 'Tea Sale Management' AND TRIM(UNIT) IS NULL;

COMMIT;
