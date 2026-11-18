-- =====================================================================
-- TeaSaleManagement (HS) -- "Purchase Contract Register"  (2026-11-16)
-- =====================================================================
-- A new report under "Enquiries & Reports", after "Tea Purchase Register":
--
--   Enquiries & Reports (heading)     03000000F   HS_RPT0 2  JAG1
--     ...
--     Tea Purchase Register           04011600F   HS_MIS  16 HS_RPT0!2          HoTeaPurchaseRegister / Index
--     Purchase Contract Register      04011700F   HS_MIS  17 HS_RPT0!2          PurchaseContractRegister / Index   <-- new
--
-- MVC  : Controllers\HS\Reports\PurchaseContractRegisterController.cs, Views\PurchaseContractRegister
-- API  : Controllers\TeaSale\Report\HoPurchaseContractRegisterController.cs (T_TEA_PURCH_CONTRACT of the login schema)
-- Run as CLASSICNEW. Every statement skips rows that already exist, so the script can be run again.
-- Users must open the menu page again (or log in) to see the new entry.
-- =====================================================================

-- 1. Menu item (TYPE 'E' = screen); display columns copied from "Tea Purchase Register"
INSERT INTO VBMENU_HOSALES
       (ID, NAME, INDEX_ORA, PID, IMAGE, TAG, TYPE, SIMAGE, ORDERCODE, PRNM, PGSZ, ORNT, FNTSZ,
        FNTNM, DTLOCK, MACHINE_NO, TMPORDER, FTEXT, MENUTAG, PERDOTNETMENU, ATVDOTNETMENU, MODULE)
SELECT 'HS_MIS', 'Purchase Contract Register', '17', h.PID, h.IMAGE, h.TAG, 'E', h.SIMAGE, '04011700F',
       h.PRNM, h.PGSZ, h.ORNT, h.FNTSZ, h.FNTNM, h.DTLOCK, h.MACHINE_NO, h.TMPORDER, h.FTEXT,
       'HS', 'Y', 'Y', 'HS'
FROM   (SELECT * FROM VBMENU_HOSALES WHERE MODULE = 'HS' AND ORDERCODE = '04011600F' AND ROWNUM = 1) h
WHERE  NOT EXISTS (SELECT 1 FROM VBMENU_HOSALES x WHERE x.MODULE = 'HS' AND x.ORDERCODE = '04011700F')
AND    NOT EXISTS (SELECT 1 FROM VBMENU_HOSALES x WHERE x.MODULE = 'HS' AND x.ID = 'HS_MIS' AND x.INDEX_ORA = '17');

-- 2. Screen mapping (CONTROLLER is also the AEDV key)
INSERT INTO VBMENU_DONE (ID, PROJECTNAME, MENUNAME, MENUCODE, CONTROLLER, ACTIONMETHOD, IS4SUBDOMAIN, MODULECODE, ISACTIVE)
SELECT (SELECT NVL(MAX(ID), 0) FROM VBMENU_DONE) + 1, 'HS', 'Purchase Contract Register', '04011700F', 'PurchaseContractRegister', 'Index', 1, 'HS', 1
FROM dual WHERE NOT EXISTS (SELECT 1 FROM VBMENU_DONE x WHERE x.MODULECODE = 'HS' AND x.MENUCODE = '04011700F');

-- 3. Rights: CORETEAM gets AEDV (a report needs only V), days 0 -- login FACT_JAYA2027 (LOCA JAY)
INSERT INTO FACT_JAYA2027.USRACS_HOSALES
       (ID, LOCA, UID_ORA, MNUID, INDEX_ORA, AEDV, EDAY, ADAY, DDAY, USER_NAME, USER_ENTDT,
        OS_USER, TERMINAL_ID, SCHEMA_NM)
SELECT (SELECT NVL(MAX(ID), 0) FROM FACT_JAYA2027.USRACS_HOSALES) + ROWNUM,
       'JAY', 'CORETEAM', m.ID, m.INDEX_ORA, 'AEDV', 0, 0, 0, USER, SYSDATE,
       USER, 'setup_hs_purch_contract', 'FACT_JAYA2027'
FROM   VBMENU_HOSALES m
WHERE  m.MODULE = 'HS' AND m.ORDERCODE = '04011700F'
AND    NOT EXISTS (SELECT 1 FROM FACT_JAYA2027.USRACS_HOSALES x
                   WHERE x.UID_ORA = 'CORETEAM' AND x.MNUID = m.ID AND x.INDEX_ORA = m.INDEX_ORA);

-- 3b. Same right in FACT_JSTIL2027 (HS login HEAD_OFFICE, LOCA JST -- where the purchase contracts are).
--     Only where CORETEAM already has the HS "Enquiries & Reports" heading there (CLASSICTRIAL; not CLASSICDEV,
--     which has no HS login for FACT_JSTIL2027).
INSERT INTO FACT_JSTIL2027.USRACS_HOSALES
       (ID, LOCA, UID_ORA, MNUID, INDEX_ORA, AEDV, EDAY, ADAY, DDAY, USER_NAME, USER_ENTDT,
        OS_USER, TERMINAL_ID, SCHEMA_NM)
SELECT (SELECT NVL(MAX(ID), 0) FROM FACT_JSTIL2027.USRACS_HOSALES) + ROWNUM,
       'JST', 'CORETEAM', m.ID, m.INDEX_ORA, 'AEDV', 0, 0, 0, USER, SYSDATE,
       USER, 'setup_hs_purch_contract', 'FACT_JSTIL2027'
FROM   VBMENU_HOSALES m
WHERE  m.MODULE = 'HS' AND m.ORDERCODE = '04011700F'
AND    EXISTS (SELECT 1 FROM FACT_JSTIL2027.USRACS_HOSALES h
               WHERE h.UID_ORA = 'CORETEAM' AND h.MNUID = 'HS_RPT0' AND h.INDEX_ORA = '2')
AND    NOT EXISTS (SELECT 1 FROM FACT_JSTIL2027.USRACS_HOSALES x
                   WHERE x.UID_ORA = 'CORETEAM' AND x.MNUID = m.ID AND x.INDEX_ORA = m.INDEX_ORA);

COMMIT;

-- 4. Verify
SELECT m.ID, m.INDEX_ORA, m.PID, m.ORDERCODE, m.NAME, m.TYPE, m.PERDOTNETMENU, d.CONTROLLER, d.ACTIONMETHOD, d.ISACTIVE
FROM   VBMENU_HOSALES m
LEFT JOIN VBMENU_DONE d ON d.MENUCODE = m.ORDERCODE AND d.MODULECODE = 'HS'
WHERE  m.MODULE = 'HS' AND m.ORDERCODE LIKE '04%'
ORDER  BY m.ORDERCODE;

-- =====================================================================
-- OPTIONAL (not run by the setup) -- Vendor Name column
-- =====================================================================
-- The report reads the vendor names from the login's inventory schema (CLASSIC_CONTROL.SCHEMA_INV:
-- INVE_JSTGARDEN2027 for FACT_JSTIL2027). The API works as the factory user, whose rights come from the role
-- <schema>ALL; without this grant the register still comes out, with the Vendor Name blank and a note.
-- GRANT SELECT ON INVE_JSTGARDEN2027.MAST_VENDOR TO FACT_JSTIL2027ALL;

-- =====================================================================
-- UNDO (not part of the setup)
-- =====================================================================
-- DELETE FROM FACT_JAYA2027.USRACS_HOSALES  WHERE MNUID = 'HS_MIS' AND INDEX_ORA = '17';
-- DELETE FROM FACT_JSTIL2027.USRACS_HOSALES WHERE MNUID = 'HS_MIS' AND INDEX_ORA = '17';
-- DELETE FROM VBMENU_DONE    WHERE MODULECODE = 'HS' AND MENUCODE = '04011700F';
-- DELETE FROM VBMENU_HOSALES WHERE MODULE = 'HS' AND ORDERCODE = '04011700F';
-- COMMIT;
