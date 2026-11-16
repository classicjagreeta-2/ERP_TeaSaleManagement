-- =====================================================================
-- CLASSICDEV: Tea Sale Management (HS) login for FACT_JSTIL2027  (2026-11-16)
-- =====================================================================
-- CLASSICDEV had only one HS company (FACT_JAYA2027, LOCA JAY). CLASSICTRIAL also has the Head Office one
-- (FACT_JSTIL2027, LOCA JST -- the HO data, and the Packet Tea purchase contracts the Purchase Contract
-- Register lists). This script gives CLASSICDEV the same three things CLASSICTRIAL has:
--
--   1. CLASSIC_CONTROL   : an HS company / year row for FACT_JSTIL2027 (copy of its PT row, YR 2027 as on TRIAL)
--   2. USRACS_HOSALES    : CORETEAM gets AEDV on every HS menu row, in FACT_JSTIL2027 (LOCA JST)
--   3. USER_SCHEMA_LINK  : CORETEAM / FACT_JSTIL2027 / unit JSTI / module HS
--
-- Run as CLASSICNEW. Every statement skips rows that already exist. Log in again afterwards and pick the
-- FACT_JSTIL2027 company under Tea Sale Management.
-- =====================================================================

-- 1. Company / year row
INSERT INTO CLASSIC_CONTROL
       (SRLNO, NAME, YRFR, YRTO, ACFIL, ACMFIL, ACREF, SIMB, YR, ADD1, CITY, PIN, UNITDEST, USR_ID,
        LUSR_ID, DATAVER, EXP_TAG, IMP_TAG, MERGE_TAG, MIRROR_TAG, TRIG_TAG, SCHEMA_SALES,
        SCHEMA_FINANCE, SCHEMA_PROD, SCHEMA_PAY, SCHEMA_INV, SCHEMA_FACTORY, MAIN_UNIT, VNAME,
        ACTIVE_TAG, GRP, MODULE, LOCA, SCHEMA_RM, ID, MAIN_UNITDEST, SCHEMA_TYPE)
SELECT c.SRLNO, c.NAME, c.YRFR, c.YRTO, c.ACFIL, c.ACMFIL, c.ACREF, c.SIMB, '2027', c.ADD1, c.CITY, c.PIN,
       c.UNITDEST, c.USR_ID, c.LUSR_ID, c.DATAVER, c.EXP_TAG, c.IMP_TAG, c.MERGE_TAG, c.MIRROR_TAG,
       c.TRIG_TAG, c.SCHEMA_SALES, c.SCHEMA_FINANCE, c.SCHEMA_PROD, c.SCHEMA_PAY, c.SCHEMA_INV,
       c.SCHEMA_FACTORY, c.MAIN_UNIT, c.VNAME, c.ACTIVE_TAG, c.GRP, 'HS', c.LOCA, c.SCHEMA_RM,
       (SELECT NVL(MAX(ID), 0) + 1 FROM CLASSIC_CONTROL), c.MAIN_UNITDEST, c.SCHEMA_TYPE
FROM   CLASSIC_CONTROL c
WHERE  c.MODULE = 'PT' AND c.USR_ID = 'FACT_JSTIL2027' AND ROWNUM = 1
AND    NOT EXISTS (SELECT 1 FROM CLASSIC_CONTROL x WHERE x.MODULE = 'HS' AND x.USR_ID = 'FACT_JSTIL2027');

-- 2. Rights: CORETEAM gets AEDV on every HS entry (headings included), back-date days 0
INSERT INTO FACT_JSTIL2027.USRACS_HOSALES
       (ID, LOCA, UID_ORA, MNUID, INDEX_ORA, AEDV, EDAY, ADAY, DDAY, USER_NAME, USER_ENTDT,
        OS_USER, TERMINAL_ID, SCHEMA_NM)
SELECT (SELECT NVL(MAX(ID), 0) FROM FACT_JSTIL2027.USRACS_HOSALES) + ROWNUM,
       'JST', 'CORETEAM', m.ID, m.INDEX_ORA, 'AEDV', 0, 0, 0, USER, SYSDATE,
       USER, 'CLASSICDEV_hs_head_office_login', 'FACT_JSTIL2027'
FROM   VBMENU_HOSALES m
WHERE  m.MODULE = 'HS'
AND    NOT EXISTS (SELECT 1 FROM FACT_JSTIL2027.USRACS_HOSALES x
                   WHERE x.UID_ORA = 'CORETEAM' AND x.MNUID = m.ID AND x.INDEX_ORA = m.INDEX_ORA);

-- 3. Module + unit link (ID is an identity column -- never insert it)
INSERT INTO USER_SCHEMA_LINK
       (LOCA, UNIT, USER_ORA, USR_ID, MODULE_NAME, ENTDT, TIME_ORA, MACHINE_NO, API_ALLOW, MODULE_CODE, TECHNOLOGY)
SELECT 'JST', 'JSTI', 'CORETEAM', 'FACT_JSTIL2027', 'Tea Sale Management', TRUNC(SYSDATE), TO_CHAR(SYSDATE, 'HH24:MI'),
       'hs_head_office_login', 1, 'HS', 'MVC'
FROM   dual
WHERE  NOT EXISTS (SELECT 1 FROM USER_SCHEMA_LINK x
                   WHERE x.USER_ORA = 'CORETEAM' AND x.USR_ID = 'FACT_JSTIL2027' AND x.MODULE_CODE = 'HS');

-- 3b. Second unit GORA (2026-11-16): the Venus Tea (PT) purchase contracts are stored under it
INSERT INTO USER_SCHEMA_LINK
       (LOCA, UNIT, USER_ORA, USR_ID, MODULE_NAME, ENTDT, TIME_ORA, MACHINE_NO, API_ALLOW, MODULE_CODE, TECHNOLOGY)
SELECT 'JST', 'GORA', 'CORETEAM', 'FACT_JSTIL2027', 'Tea Sale Management', TRUNC(SYSDATE), TO_CHAR(SYSDATE, 'HH24:MI'),
       'hs_head_office_login', 1, 'HS', 'MVC'
FROM   dual
WHERE  NOT EXISTS (SELECT 1 FROM USER_SCHEMA_LINK x
                   WHERE x.USER_ORA = 'CORETEAM' AND x.USR_ID = 'FACT_JSTIL2027' AND TRIM(x.UNIT) = 'GORA');

-- 3c. Vendor names of the Purchase Contract Register (run 2026-11-16)
GRANT SELECT ON INVE_JSTGARDEN2027.MAST_VENDOR TO FACT_JSTIL2027ALL;

COMMIT;

-- 4. Verify
SELECT USR_ID, NAME, LOCA, YR, MODULE, SCHEMA_FINANCE, SCHEMA_INV FROM CLASSIC_CONTROL WHERE MODULE = 'HS';

SELECT COUNT(*) HS_RIGHTS FROM FACT_JSTIL2027.USRACS_HOSALES
WHERE  UID_ORA = 'CORETEAM' AND MNUID IN (SELECT ID FROM VBMENU_HOSALES WHERE MODULE = 'HS');

SELECT ID, USR_ID, UNIT, MODULE_CODE FROM USER_SCHEMA_LINK WHERE USER_ORA = 'CORETEAM' AND MODULE_CODE = 'HS';

-- =====================================================================
-- UNDO (not part of the setup)
-- =====================================================================
-- DELETE FROM USER_SCHEMA_LINK WHERE MODULE_CODE = 'HS' AND USER_ORA = 'CORETEAM' AND USR_ID = 'FACT_JSTIL2027';
-- DELETE FROM FACT_JSTIL2027.USRACS_HOSALES WHERE UID_ORA = 'CORETEAM' AND TERMINAL_ID = 'CLASSICDEV_hs_head_office_login';
-- DELETE FROM CLASSIC_CONTROL WHERE MODULE = 'HS' AND USR_ID = 'FACT_JSTIL2027';
-- COMMIT;
