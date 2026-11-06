-- =====================================================================
-- CLASSICTRIAL: MODULE.UNIT_FIELD is missing (CLASSICDEV has it; sync_hs_trial.sql notes the gap).
-- Run as CLASSICNEW on CLASSICTRIAL. Safe to run again.
-- UNIT_FIELD lists the unit-table columns the app reads; CLASSICDEV's HS row has 'CODE,NAME'.
-- Only the HS row is set here -- other modules' CLASSICDEV values were not checked.
-- =====================================================================

-- 1. Add the column if it is not there yet
DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count
    FROM   user_tab_columns
    WHERE  table_name = 'MODULE' AND column_name = 'UNIT_FIELD';

    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE MODULE ADD (UNIT_FIELD VARCHAR2(100))';
    END IF;
END;
/

-- 2. Fill it for Tea Sale Management (HS), matching CLASSICDEV
UPDATE MODULE
SET    UNIT_FIELD = 'CODE,NAME'
WHERE  MODULE_CODE = 'HS'
AND    UNIT_FIELD IS NULL;

COMMIT;
