-- =====================================================================
-- TeaSaleManagement (HS) -- "Tea Tasting Entry" + "Outturn Sample Issue" menus, Inputs re-ordered (2026-11-17)
-- =====================================================================
-- New screens (VB6 HO Sales trn_sample_tasting.frm / TRN_OUTTURN_SAMPLE.frm, pConf_tag "E"):
--   Tea Tasting Entry      -> MVC TeaTastingController    -> API TeaTasting    (T_SAMPLE_TAST_HEAD / _DET)
--   Outturn Sample Issue   -> MVC OutturnSampleController -> API OutturnSample (T_OUTTURN_SAMPLE, sale type '5')
--
-- The HS menu is ordered by ORDERCODE, so the "Inputs" sequence asked for -- Tea Sample Receive Entry,
-- Tea Tasting Entry, Outturn Sample Issue, Sauda Entry, Confirmation Entry, then the others -- is made by
-- moving the ORDERCODE of Tea Sample Receive and Sauda Entry (VBMENU_DONE.MENUCODE follows). ID /
-- INDEX_ORA are not changed, so the existing rights rows (USRACS_HOSALES: MNUID + INDEX_ORA) stay valid.
--
--   Inputs (heading)              02000000F   HS_MAIN 1
--     Tea Sample Receive Entry    02040100T   HS_INP  8    (was 02040900T)   TeaSampleReceive
--     Tea Tasting Entry           02040200T   HS_INP  9    NEW               TeaTasting
--     Outturn Sample Issue        02040250T   HS_INP 11    NEW               OutturnSample
--     Sauda Entry                 02040300T   HS_INP 37    (was 02043800T)   SaudaEntry
--     Confirmation Entry          02040400T   HS_INP  3    (unchanged, folder > Domestic 02040401T)
--     Sales Invoice Entry         02040500T   HS_INP  4    (unchanged)
--     Auction Release             02040600T   HS_INP  5    (unchanged)
--     Auction Catalogue Entry     02040700T   HS_INP  6    (unchanged)
--     Withdrawl Entry             02040800T   HS_INP  7    (unchanged)
-- INDEX_ORA 9 / 11 are the VB HO menu's own indexes ("10. Tea Sample Tasting", "12. Outturn Sample Entry").
--
-- Run as CLASSICNEW. Every statement skips rows that already exist / are already moved.
-- Run on CLASSICDEV 2026-11-17 (not on CLASSICTRIAL) -- all rows applied, verified with step 5.
-- =====================================================================

-- 1. Re-order: Tea Sample Receive and Sauda Entry move up (menu row + screen mapping)
UPDATE VBMENU_HOSALES SET ORDERCODE = '02040100T'
WHERE  MODULE = 'HS' AND ID = 'HS_INP' AND INDEX_ORA = '8' AND ORDERCODE = '02040900T'
AND    NOT EXISTS (SELECT 1 FROM VBMENU_HOSALES x WHERE x.MODULE = 'HS' AND x.ORDERCODE = '02040100T');

UPDATE VBMENU_DONE SET MENUCODE = '02040100T'
WHERE  MODULECODE = 'HS' AND MENUCODE = '02040900T' AND CONTROLLER = 'TeaSampleReceive';

UPDATE VBMENU_HOSALES SET ORDERCODE = '02040300T'
WHERE  MODULE = 'HS' AND ID = 'HS_INP' AND INDEX_ORA = '37' AND ORDERCODE = '02043800T'
AND    NOT EXISTS (SELECT 1 FROM VBMENU_HOSALES x WHERE x.MODULE = 'HS' AND x.ORDERCODE = '02040300T');

UPDATE VBMENU_DONE SET MENUCODE = '02040300T'
WHERE  MODULECODE = 'HS' AND MENUCODE = '02043800T' AND CONTROLLER = 'SaudaEntry';

-- 2. New menu items under "Inputs" (display columns copied from Tea Sample Receive's row)
INSERT INTO VBMENU_HOSALES
       (ID, NAME, INDEX_ORA, PID, IMAGE, TAG, TYPE, SIMAGE, ORDERCODE, PRNM, PGSZ, ORNT, FNTSZ,
        FNTNM, DTLOCK, MACHINE_NO, TMPORDER, FTEXT, MENUTAG, PERDOTNETMENU, ATVDOTNETMENU, MODULE)
SELECT 'HS_INP', 'Tea Tasting Entry', '9', 'HS_MAIN!1', h.IMAGE, h.TAG, 'E', h.SIMAGE, '02040200T',
       h.PRNM, h.PGSZ, h.ORNT, h.FNTSZ, h.FNTNM, h.DTLOCK, h.MACHINE_NO, h.TMPORDER, h.FTEXT,
       'HS', 'Y', 'Y', 'HS'
FROM   (SELECT * FROM VBMENU_HOSALES WHERE MODULE = 'HS' AND ID = 'HS_INP' AND INDEX_ORA = '8' AND ROWNUM = 1) h
WHERE  NOT EXISTS (SELECT 1 FROM VBMENU_HOSALES x WHERE x.MODULE = 'HS' AND x.ORDERCODE = '02040200T')
AND    NOT EXISTS (SELECT 1 FROM VBMENU_HOSALES x WHERE x.MODULE = 'HS' AND x.ID = 'HS_INP' AND x.INDEX_ORA = '9');

INSERT INTO VBMENU_HOSALES
       (ID, NAME, INDEX_ORA, PID, IMAGE, TAG, TYPE, SIMAGE, ORDERCODE, PRNM, PGSZ, ORNT, FNTSZ,
        FNTNM, DTLOCK, MACHINE_NO, TMPORDER, FTEXT, MENUTAG, PERDOTNETMENU, ATVDOTNETMENU, MODULE)
SELECT 'HS_INP', 'Outturn Sample Issue', '11', 'HS_MAIN!1', h.IMAGE, h.TAG, 'E', h.SIMAGE, '02040250T',
       h.PRNM, h.PGSZ, h.ORNT, h.FNTSZ, h.FNTNM, h.DTLOCK, h.MACHINE_NO, h.TMPORDER, h.FTEXT,
       'HS', 'Y', 'Y', 'HS'
FROM   (SELECT * FROM VBMENU_HOSALES WHERE MODULE = 'HS' AND ID = 'HS_INP' AND INDEX_ORA = '8' AND ROWNUM = 1) h
WHERE  NOT EXISTS (SELECT 1 FROM VBMENU_HOSALES x WHERE x.MODULE = 'HS' AND x.ORDERCODE = '02040250T')
AND    NOT EXISTS (SELECT 1 FROM VBMENU_HOSALES x WHERE x.MODULE = 'HS' AND x.ID = 'HS_INP' AND x.INDEX_ORA = '11');

-- 3. Screen mapping (CONTROLLER = AEDV key = record-lock screen)
INSERT INTO VBMENU_DONE
       (ID, PROJECTNAME, MENUNAME, MENUCODE, CONTROLLER, ACTIONMETHOD, IS4SUBDOMAIN, MODULECODE, ISACTIVE)
SELECT (SELECT NVL(MAX(ID), 0) FROM VBMENU_DONE) + 1, 'HS', 'Tea Tasting Entry', '02040200T',
       'TeaTasting', 'Index', 1, 'HS', 1
FROM   dual
WHERE  NOT EXISTS (SELECT 1 FROM VBMENU_DONE x WHERE x.MODULECODE = 'HS' AND x.MENUCODE = '02040200T');

INSERT INTO VBMENU_DONE
       (ID, PROJECTNAME, MENUNAME, MENUCODE, CONTROLLER, ACTIONMETHOD, IS4SUBDOMAIN, MODULECODE, ISACTIVE)
SELECT (SELECT NVL(MAX(ID), 0) FROM VBMENU_DONE) + 1, 'HS', 'Outturn Sample Issue', '02040250T',
       'OutturnSample', 'Index', 1, 'HS', 1
FROM   dual
WHERE  NOT EXISTS (SELECT 1 FROM VBMENU_DONE x WHERE x.MODULECODE = 'HS' AND x.MENUCODE = '02040250T');

-- 4. Rights: CORETEAM gets AEDV on both items, back-date days 0 (= today only, like its other HS rows).
--    To grant another user, repeat with that user id. The user must open the menu page again (or log in).
INSERT INTO FACT_JAYA2027.USRACS_HOSALES
       (ID, LOCA, UID_ORA, MNUID, INDEX_ORA, AEDV, EDAY, ADAY, DDAY, USER_NAME, USER_ENTDT,
        OS_USER, TERMINAL_ID, SCHEMA_NM)
SELECT (SELECT NVL(MAX(ID), 0) FROM FACT_JAYA2027.USRACS_HOSALES) + ROWNUM,
       'JAY', 'CORETEAM', m.ID, m.INDEX_ORA, 'AEDV', 0, 0, 0, USER, SYSDATE,
       USER, 'setup_hs_tasting_outturn.sql', 'FACT_JAYA2027'
FROM   VBMENU_HOSALES m
WHERE  m.MODULE = 'HS' AND m.ORDERCODE IN ('02040200T', '02040250T')
AND    NOT EXISTS (SELECT 1 FROM FACT_JAYA2027.USRACS_HOSALES x
                   WHERE x.UID_ORA = 'CORETEAM' AND x.MNUID = m.ID AND x.INDEX_ORA = m.INDEX_ORA);

-- 4b. Same rights in FACT_JSTIL2027 (the HS "HEAD_OFFICE" login) -- only where CORETEAM already has HS "Inputs" there.
INSERT INTO FACT_JSTIL2027.USRACS_HOSALES
       (ID, LOCA, UID_ORA, MNUID, INDEX_ORA, AEDV, EDAY, ADAY, DDAY, USER_NAME, USER_ENTDT,
        OS_USER, TERMINAL_ID, SCHEMA_NM)
SELECT (SELECT NVL(MAX(ID), 0) FROM FACT_JSTIL2027.USRACS_HOSALES) + ROWNUM,
       'JST', 'CORETEAM', m.ID, m.INDEX_ORA, 'AEDV', 0, 0, 0, USER, SYSDATE,
       USER, 'setup_hs_tasting_outturn.sql', 'FACT_JSTIL2027'
FROM   VBMENU_HOSALES m
WHERE  m.MODULE = 'HS' AND m.ORDERCODE IN ('02040200T', '02040250T')
AND    EXISTS (SELECT 1 FROM FACT_JSTIL2027.USRACS_HOSALES h
               WHERE h.UID_ORA = 'CORETEAM' AND h.MNUID = 'HS_MAIN' AND h.INDEX_ORA = '1')
AND    NOT EXISTS (SELECT 1 FROM FACT_JSTIL2027.USRACS_HOSALES x
                   WHERE x.UID_ORA = 'CORETEAM' AND x.MNUID = m.ID AND x.INDEX_ORA = m.INDEX_ORA);

COMMIT;

-- 5. Verify
SELECT m.ID, m.INDEX_ORA, m.PID, m.ORDERCODE, m.NAME, m.TYPE, d.CONTROLLER, d.ACTIONMETHOD, d.ISACTIVE
FROM   VBMENU_HOSALES m
LEFT JOIN VBMENU_DONE d ON d.MENUCODE = m.ORDERCODE AND d.MODULECODE = 'HS'
WHERE  m.MODULE = 'HS' AND m.ORDERCODE LIKE '0204%'
ORDER  BY m.ORDERCODE;

-- =====================================================================
-- UNDO (not part of the setup)
-- =====================================================================
-- DELETE FROM FACT_JAYA2027.USRACS_HOSALES  WHERE MNUID = 'HS_INP' AND INDEX_ORA IN ('9', '11');
-- DELETE FROM FACT_JSTIL2027.USRACS_HOSALES WHERE MNUID = 'HS_INP' AND INDEX_ORA IN ('9', '11');
-- DELETE FROM VBMENU_DONE    WHERE MODULECODE = 'HS' AND MENUCODE IN ('02040200T', '02040250T');
-- DELETE FROM VBMENU_HOSALES WHERE MODULE = 'HS' AND ORDERCODE IN ('02040200T', '02040250T');
-- UPDATE VBMENU_HOSALES SET ORDERCODE = '02040900T' WHERE MODULE = 'HS' AND ID = 'HS_INP' AND INDEX_ORA = '8'  AND ORDERCODE = '02040100T';
-- UPDATE VBMENU_DONE    SET MENUCODE  = '02040900T' WHERE MODULECODE = 'HS' AND MENUCODE = '02040100T' AND CONTROLLER = 'TeaSampleReceive';
-- UPDATE VBMENU_HOSALES SET ORDERCODE = '02043800T' WHERE MODULE = 'HS' AND ID = 'HS_INP' AND INDEX_ORA = '37' AND ORDERCODE = '02040300T';
-- UPDATE VBMENU_DONE    SET MENUCODE  = '02043800T' WHERE MODULECODE = 'HS' AND MENUCODE = '02040300T' AND CONTROLLER = 'SaudaEntry';
-- COMMIT;
