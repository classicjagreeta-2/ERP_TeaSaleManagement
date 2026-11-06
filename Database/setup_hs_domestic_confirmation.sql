-- =====================================================================
-- TeaSaleManagement (HS) -- "Confirmation Entry > Domestic" menu  (2026-11-04)
-- =====================================================================
-- The Domestic Confirmation (VB6 TRN_OFFER_CONF.frm, pConf_tag "D", VB menu "4.1 Domestic") moves
-- from the TS module (ERP_PacketSalesExport, Confirmation Entry > Domestic) to HS. In TS only
-- "Export" stays: the TS rows "Domestic" (01051031T) and "Birla Tea" (01051033T) are hidden
-- (PERDOTNETMENU / ATVDOTNETMENU = 'N', VBMENU_DONE.ISACTIVE = 0) -- section 5 below, nothing deleted.
-- Run as CLASSICNEW. Every statement skips rows that already exist / are already set.
-- Run on CLASSICTRIAL and CLASSICDEV 2026-11-04 (step 4b added afterwards and re-run on both).
--
--   menu entry                ORDERCODE   ID / INDEX_ORA / PID          controller / action
--   Inputs (heading)          02000000F   HS_MAIN 1
--     Confirmation Entry      02040400T   HS_INP  3  HS_MAIN!1          (folder)
--       Domestic              02040401T   HS_CNF  0  HS_INP!3           DomesticConfirmation / Index
--
-- Same shape as "Sales Invoice Entry" (02040500T, HS_INP 4) > Private / Auction Sale (HS_SALE, PID
-- HS_INP!4), whose rows the display columns are copied from. INDEX_ORA 3 puts it just before Sales
-- Invoice Entry, where the VB HO menu has "4. Confirmation Entry".
-- =====================================================================

-- 1. Folder "Confirmation Entry" under "Inputs" (TYPE null = sub-menu, no screen)
INSERT INTO VBMENU_HOSALES
       (ID, NAME, INDEX_ORA, PID, IMAGE, TAG, TYPE, SIMAGE, ORDERCODE, PRNM, PGSZ, ORNT, FNTSZ,
        FNTNM, DTLOCK, MACHINE_NO, TMPORDER, FTEXT, MENUTAG, PERDOTNETMENU, ATVDOTNETMENU, MODULE)
SELECT 'HS_INP', 'Confirmation Entry', '3', 'HS_MAIN!1', h.IMAGE, h.TAG, NULL, h.SIMAGE, '02040400T',
       h.PRNM, h.PGSZ, h.ORNT, h.FNTSZ, h.FNTNM, h.DTLOCK, h.MACHINE_NO, h.TMPORDER, h.FTEXT,
       'HS', 'Y', 'Y', 'HS'
FROM   (SELECT * FROM VBMENU_HOSALES WHERE MODULE = 'HS' AND ORDERCODE = '02040500T' AND ROWNUM = 1) h
WHERE  NOT EXISTS (SELECT 1 FROM VBMENU_HOSALES x WHERE x.MODULE = 'HS' AND x.ORDERCODE = '02040400T')
AND    NOT EXISTS (SELECT 1 FROM VBMENU_HOSALES x WHERE x.MODULE = 'HS' AND x.ID = 'HS_INP' AND x.INDEX_ORA = '3');

-- 2. Item "Domestic" inside it
INSERT INTO VBMENU_HOSALES
       (ID, NAME, INDEX_ORA, PID, IMAGE, TAG, TYPE, SIMAGE, ORDERCODE, PRNM, PGSZ, ORNT, FNTSZ,
        FNTNM, DTLOCK, MACHINE_NO, TMPORDER, FTEXT, MENUTAG, PERDOTNETMENU, ATVDOTNETMENU, MODULE)
SELECT 'HS_CNF', 'Domestic', '0', 'HS_INP!3', h.IMAGE, h.TAG, 'E', h.SIMAGE, '02040401T',
       h.PRNM, h.PGSZ, h.ORNT, h.FNTSZ, h.FNTNM, h.DTLOCK, h.MACHINE_NO, h.TMPORDER, h.FTEXT,
       'HS', 'Y', 'Y', 'HS'
FROM   (SELECT * FROM VBMENU_HOSALES WHERE MODULE = 'HS' AND ORDERCODE = '02040501T' AND ROWNUM = 1) h
WHERE  NOT EXISTS (SELECT 1 FROM VBMENU_HOSALES x WHERE x.MODULE = 'HS' AND x.ORDERCODE = '02040401T')
AND    NOT EXISTS (SELECT 1 FROM VBMENU_HOSALES x WHERE x.MODULE = 'HS' AND x.ID = 'HS_CNF' AND x.INDEX_ORA = '0');

-- 3. Screen mapping (CONTROLLER is also the AEDV key and the record-lock screen)
INSERT INTO VBMENU_DONE
       (ID, PROJECTNAME, MENUNAME, MENUCODE, CONTROLLER, ACTIONMETHOD, IS4SUBDOMAIN, MODULECODE, ISACTIVE)
SELECT (SELECT NVL(MAX(ID), 0) FROM VBMENU_DONE) + 1, 'HS', 'Domestic', '02040401T',
       'DomesticConfirmation', 'Index', 1, 'HS', 1
FROM   dual
WHERE  NOT EXISTS (SELECT 1 FROM VBMENU_DONE x WHERE x.MODULECODE = 'HS' AND x.MENUCODE = '02040401T');

-- 4. Rights: CORETEAM gets AEDV on the folder and the item, back-date days 0 (= today only, like its
--    other HS rows). To grant another user, repeat with that user id. The user must open the menu
--    page again (or log in) to see it.
INSERT INTO FACT_JAYA2027.USRACS_HOSALES
       (ID, LOCA, UID_ORA, MNUID, INDEX_ORA, AEDV, EDAY, ADAY, DDAY, USER_NAME, USER_ENTDT,
        OS_USER, TERMINAL_ID, SCHEMA_NM)
SELECT (SELECT NVL(MAX(ID), 0) FROM FACT_JAYA2027.USRACS_HOSALES) + ROWNUM,
       'JAY', 'CORETEAM', m.ID, m.INDEX_ORA, 'AEDV', 0, 0, 0, USER, SYSDATE,
       USER, 'setup_hs_domestic_confirmation.sql', 'FACT_JAYA2027'
FROM   VBMENU_HOSALES m
WHERE  m.MODULE = 'HS' AND m.ORDERCODE IN ('02040400T', '02040401T')
AND    NOT EXISTS (SELECT 1 FROM FACT_JAYA2027.USRACS_HOSALES x
                   WHERE x.UID_ORA = 'CORETEAM' AND x.MNUID = m.ID AND x.INDEX_ORA = m.INDEX_ORA);

-- 4b. Same rights in FACT_JSTIL2027 -- the schema of the HS login "Neptune ... HEAD_OFFICE" (CLASSIC_CONTROL
--     HS row LOCA JST). Only where CORETEAM already has HS "Inputs" there (skips a DB without HS rights).
INSERT INTO FACT_JSTIL2027.USRACS_HOSALES
       (ID, LOCA, UID_ORA, MNUID, INDEX_ORA, AEDV, EDAY, ADAY, DDAY, USER_NAME, USER_ENTDT,
        OS_USER, TERMINAL_ID, SCHEMA_NM)
SELECT (SELECT NVL(MAX(ID), 0) FROM FACT_JSTIL2027.USRACS_HOSALES) + ROWNUM,
       'JST', 'CORETEAM', m.ID, m.INDEX_ORA, 'AEDV', 0, 0, 0, USER, SYSDATE,
       USER, 'setup_hs_domestic_confirmation.sql', 'FACT_JSTIL2027'
FROM   VBMENU_HOSALES m
WHERE  m.MODULE = 'HS' AND m.ORDERCODE IN ('02040400T', '02040401T')
AND    EXISTS (SELECT 1 FROM FACT_JSTIL2027.USRACS_HOSALES h
               WHERE h.UID_ORA = 'CORETEAM' AND h.MNUID = 'HS_MAIN' AND h.INDEX_ORA = '1')
AND    NOT EXISTS (SELECT 1 FROM FACT_JSTIL2027.USRACS_HOSALES x
                   WHERE x.UID_ORA = 'CORETEAM' AND x.MNUID = m.ID AND x.INDEX_ORA = m.INDEX_ORA);

-- 5. TS (ERP_PacketSalesExport): Confirmation Entry keeps only "Export"
UPDATE VBMENU_TEA_SALES SET PERDOTNETMENU = 'N', ATVDOTNETMENU = 'N'
WHERE  MODULE = 'TS' AND ORDERCODE IN ('01051031T', '01051033T')
AND    (NVL(PERDOTNETMENU, 'Y') <> 'N' OR NVL(ATVDOTNETMENU, 'Y') <> 'N');

UPDATE VBMENU_DONE SET ISACTIVE = 0
WHERE  MODULECODE = 'TS' AND MENUCODE IN ('01051031T', '01051033T') AND NVL(ISACTIVE, 1) <> 0;

COMMIT;

-- 6. Verify
SELECT m.ID, m.INDEX_ORA, m.PID, m.ORDERCODE, m.NAME, m.TYPE, d.CONTROLLER, d.ACTIONMETHOD, d.ISACTIVE
FROM   VBMENU_HOSALES m
LEFT JOIN VBMENU_DONE d ON d.MENUCODE = m.ORDERCODE AND d.MODULECODE = 'HS'
WHERE  m.MODULE = 'HS'
ORDER  BY m.ORDERCODE;

-- =====================================================================
-- UNDO (not part of the setup)
-- =====================================================================
-- DELETE FROM FACT_JAYA2027.USRACS_HOSALES WHERE (MNUID = 'HS_INP' AND INDEX_ORA = '3') OR (MNUID = 'HS_CNF' AND INDEX_ORA = '0');
-- DELETE FROM FACT_JSTIL2027.USRACS_HOSALES WHERE (MNUID = 'HS_INP' AND INDEX_ORA = '3') OR (MNUID = 'HS_CNF' AND INDEX_ORA = '0');
-- DELETE FROM VBMENU_DONE    WHERE MODULECODE = 'HS' AND MENUCODE = '02040401T';
-- DELETE FROM VBMENU_HOSALES WHERE MODULE = 'HS' AND ORDERCODE IN ('02040400T', '02040401T');
-- UPDATE VBMENU_TEA_SALES SET PERDOTNETMENU = 'Y', ATVDOTNETMENU = 'Y' WHERE MODULE = 'TS' AND ORDERCODE IN ('01051031T', '01051033T');
-- UPDATE VBMENU_DONE SET ISACTIVE = 1 WHERE MODULECODE = 'TS' AND MENUCODE IN ('01051031T', '01051033T');
-- COMMIT;
