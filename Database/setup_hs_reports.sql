-- =====================================================================
-- TeaSaleManagement (HS) -- "Reports > Sales" and "MIS" menus  (2026-11-05)
-- =====================================================================
-- Seven report screens ported from the old HO Sales .NET application (VB HO menu "3. Reports > 1. Sales"
-- and "4. MIS"; same ORDERCODEs as CLASSICNEW.VBMENU_HO). Run as CLASSICNEW. Every statement skips rows
-- that already exist, so the script can be run again.
--
--   menu entry                        ORDERCODE   ID / INDEX_ORA / PID          controller / action
--   Reports (heading)                 03000000F   HS_RPT0 2  JAG1
--     Sales (folder)                  03010000T   HS_RPT  0  HS_RPT0!2
--       Tax Invoice Printing          03020100T   HS_RPTS 0  HS_RPT!0           TaxInvoicePrinting / Index
--       Export Invoice Printing (TT)  03020900T   HS_RPTS 8  HS_RPT!0           ExportInvoicePrinting / Index
--       Auction Release Printing      03021300T   HS_RPTS 12 HS_RPT!0           AuctionReleasePrinting / Index
--   MIS (heading)                     04000000F   HS_MIS0 3  JAG1
--     Tax Invoice Register            04010500F   HS_MIS  5  HS_MIS0!3          TaxInvoiceRegister / Index
--     Unsold List                     04010800F   HS_MIS  8  HS_MIS0!3          UnsoldList / Index
--     Souda Book Register             04011000F   HS_MIS  10 HS_MIS0!3          SoudaBookRegister / Index
--     Tea Purchase Register           04011600F   HS_MIS  16 HS_MIS0!3          HoTeaPurchaseRegister / Index
--       (not "TeaPurchaseRegister": that controller name is already the PT module's 09150000F)
--
-- Display columns are copied from the HS "Inputs" heading (02000000F) for headings / folders and from
-- "Private Sale" (02040501T) for the items. The INDEX_ORAs are the VB HO menu's.
-- =====================================================================

-- 1. Headings and folder (TYPE null = sub-menu, no screen)
INSERT INTO VBMENU_HOSALES
       (ID, NAME, INDEX_ORA, PID, IMAGE, TAG, TYPE, SIMAGE, ORDERCODE, PRNM, PGSZ, ORNT, FNTSZ,
        FNTNM, DTLOCK, MACHINE_NO, TMPORDER, FTEXT, MENUTAG, PERDOTNETMENU, ATVDOTNETMENU, MODULE)
SELECT n.ID, n.NAME, n.IDX, n.PID, h.IMAGE, h.TAG, NULL, h.SIMAGE, n.CODE,
       h.PRNM, h.PGSZ, h.ORNT, h.FNTSZ, h.FNTNM, h.DTLOCK, h.MACHINE_NO, h.TMPORDER, h.FTEXT,
       'HS', 'Y', 'Y', 'HS'
FROM   (SELECT * FROM VBMENU_HOSALES WHERE MODULE = 'HS' AND ORDERCODE = '02000000F' AND ROWNUM = 1) h,
       (SELECT 'HS_RPT0' ID, 'Reports' NAME, '2' IDX, 'JAG1' PID, '03000000F' CODE FROM dual UNION ALL
        SELECT 'HS_RPT',      'Sales',        '0',     'HS_RPT0!2', '03010000T'    FROM dual UNION ALL
        SELECT 'HS_MIS0',     'MIS',          '3',     'JAG1',      '04000000F'    FROM dual) n
WHERE  NOT EXISTS (SELECT 1 FROM VBMENU_HOSALES x WHERE x.MODULE = 'HS' AND x.ORDERCODE = n.CODE)
AND    NOT EXISTS (SELECT 1 FROM VBMENU_HOSALES x WHERE x.MODULE = 'HS' AND x.ID = n.ID AND x.INDEX_ORA = n.IDX);

-- 2. Report items (TYPE 'E' = screen)
INSERT INTO VBMENU_HOSALES
       (ID, NAME, INDEX_ORA, PID, IMAGE, TAG, TYPE, SIMAGE, ORDERCODE, PRNM, PGSZ, ORNT, FNTSZ,
        FNTNM, DTLOCK, MACHINE_NO, TMPORDER, FTEXT, MENUTAG, PERDOTNETMENU, ATVDOTNETMENU, MODULE)
SELECT n.ID, n.NAME, n.IDX, n.PID, h.IMAGE, h.TAG, 'E', h.SIMAGE, n.CODE,
       h.PRNM, h.PGSZ, h.ORNT, h.FNTSZ, h.FNTNM, h.DTLOCK, h.MACHINE_NO, h.TMPORDER, h.FTEXT,
       'HS', 'Y', 'Y', 'HS'
FROM   (SELECT * FROM VBMENU_HOSALES WHERE MODULE = 'HS' AND ORDERCODE = '02040501T' AND ROWNUM = 1) h,
       (SELECT 'HS_RPTS' ID, 'Tax Invoice Printing' NAME, '0' IDX, 'HS_RPT!0' PID, '03020100T' CODE FROM dual UNION ALL
        SELECT 'HS_RPTS', 'Export Invoice Printing (TT)', '8',  'HS_RPT!0',  '03020900T' FROM dual UNION ALL
        SELECT 'HS_RPTS', 'Auction Release Printing',     '12', 'HS_RPT!0',  '03021300T' FROM dual UNION ALL
        SELECT 'HS_MIS',  'Tax Invoice Register',         '5',  'HS_MIS0!3', '04010500F' FROM dual UNION ALL
        SELECT 'HS_MIS',  'Unsold List',                  '8',  'HS_MIS0!3', '04010800F' FROM dual UNION ALL
        SELECT 'HS_MIS',  'Souda Book Register',          '10', 'HS_MIS0!3', '04011000F' FROM dual UNION ALL
        SELECT 'HS_MIS',  'Tea Purchase Register',        '16', 'HS_MIS0!3', '04011600F' FROM dual) n
WHERE  NOT EXISTS (SELECT 1 FROM VBMENU_HOSALES x WHERE x.MODULE = 'HS' AND x.ORDERCODE = n.CODE)
AND    NOT EXISTS (SELECT 1 FROM VBMENU_HOSALES x WHERE x.MODULE = 'HS' AND x.ID = n.ID AND x.INDEX_ORA = n.IDX);

-- 3. Screen mapping (CONTROLLER is also the AEDV key). One row per statement so MAX(ID)+1 stays unique.
INSERT INTO VBMENU_DONE (ID, PROJECTNAME, MENUNAME, MENUCODE, CONTROLLER, ACTIONMETHOD, IS4SUBDOMAIN, MODULECODE, ISACTIVE)
SELECT (SELECT NVL(MAX(ID), 0) FROM VBMENU_DONE) + 1, 'HS', 'Tax Invoice Printing', '03020100T', 'TaxInvoicePrinting', 'Index', 1, 'HS', 1
FROM dual WHERE NOT EXISTS (SELECT 1 FROM VBMENU_DONE x WHERE x.MODULECODE = 'HS' AND x.MENUCODE = '03020100T');

INSERT INTO VBMENU_DONE (ID, PROJECTNAME, MENUNAME, MENUCODE, CONTROLLER, ACTIONMETHOD, IS4SUBDOMAIN, MODULECODE, ISACTIVE)
SELECT (SELECT NVL(MAX(ID), 0) FROM VBMENU_DONE) + 1, 'HS', 'Export Invoice Printing (TT)', '03020900T', 'ExportInvoicePrinting', 'Index', 1, 'HS', 1
FROM dual WHERE NOT EXISTS (SELECT 1 FROM VBMENU_DONE x WHERE x.MODULECODE = 'HS' AND x.MENUCODE = '03020900T');

INSERT INTO VBMENU_DONE (ID, PROJECTNAME, MENUNAME, MENUCODE, CONTROLLER, ACTIONMETHOD, IS4SUBDOMAIN, MODULECODE, ISACTIVE)
SELECT (SELECT NVL(MAX(ID), 0) FROM VBMENU_DONE) + 1, 'HS', 'Auction Release Printing', '03021300T', 'AuctionReleasePrinting', 'Index', 1, 'HS', 1
FROM dual WHERE NOT EXISTS (SELECT 1 FROM VBMENU_DONE x WHERE x.MODULECODE = 'HS' AND x.MENUCODE = '03021300T');

INSERT INTO VBMENU_DONE (ID, PROJECTNAME, MENUNAME, MENUCODE, CONTROLLER, ACTIONMETHOD, IS4SUBDOMAIN, MODULECODE, ISACTIVE)
SELECT (SELECT NVL(MAX(ID), 0) FROM VBMENU_DONE) + 1, 'HS', 'Tax Invoice Register', '04010500F', 'TaxInvoiceRegister', 'Index', 1, 'HS', 1
FROM dual WHERE NOT EXISTS (SELECT 1 FROM VBMENU_DONE x WHERE x.MODULECODE = 'HS' AND x.MENUCODE = '04010500F');

INSERT INTO VBMENU_DONE (ID, PROJECTNAME, MENUNAME, MENUCODE, CONTROLLER, ACTIONMETHOD, IS4SUBDOMAIN, MODULECODE, ISACTIVE)
SELECT (SELECT NVL(MAX(ID), 0) FROM VBMENU_DONE) + 1, 'HS', 'Unsold List', '04010800F', 'UnsoldList', 'Index', 1, 'HS', 1
FROM dual WHERE NOT EXISTS (SELECT 1 FROM VBMENU_DONE x WHERE x.MODULECODE = 'HS' AND x.MENUCODE = '04010800F');

INSERT INTO VBMENU_DONE (ID, PROJECTNAME, MENUNAME, MENUCODE, CONTROLLER, ACTIONMETHOD, IS4SUBDOMAIN, MODULECODE, ISACTIVE)
SELECT (SELECT NVL(MAX(ID), 0) FROM VBMENU_DONE) + 1, 'HS', 'Souda Book Register', '04011000F', 'SoudaBookRegister', 'Index', 1, 'HS', 1
FROM dual WHERE NOT EXISTS (SELECT 1 FROM VBMENU_DONE x WHERE x.MODULECODE = 'HS' AND x.MENUCODE = '04011000F');

INSERT INTO VBMENU_DONE (ID, PROJECTNAME, MENUNAME, MENUCODE, CONTROLLER, ACTIONMETHOD, IS4SUBDOMAIN, MODULECODE, ISACTIVE)
SELECT (SELECT NVL(MAX(ID), 0) FROM VBMENU_DONE) + 1, 'HS', 'Tea Purchase Register', '04011600F', 'HoTeaPurchaseRegister', 'Index', 1, 'HS', 1
FROM dual WHERE NOT EXISTS (SELECT 1 FROM VBMENU_DONE x WHERE x.MODULECODE = 'HS' AND x.MENUCODE = '04011600F');

-- 4. Rights: CORETEAM gets AEDV (reports need only V) on the headings, folder and items, days 0.
--    The user must open the menu page again (or log in) to see them.
INSERT INTO FACT_JAYA2027.USRACS_HOSALES
       (ID, LOCA, UID_ORA, MNUID, INDEX_ORA, AEDV, EDAY, ADAY, DDAY, USER_NAME, USER_ENTDT,
        OS_USER, TERMINAL_ID, SCHEMA_NM)
SELECT (SELECT NVL(MAX(ID), 0) FROM FACT_JAYA2027.USRACS_HOSALES) + ROWNUM,
       'JAY', 'CORETEAM', m.ID, m.INDEX_ORA, 'AEDV', 0, 0, 0, USER, SYSDATE,
       USER, 'setup_hs_reports.sql', 'FACT_JAYA2027'
FROM   VBMENU_HOSALES m
WHERE  m.MODULE = 'HS'
AND    m.ORDERCODE IN ('03000000F', '03010000T', '03020100T', '03020900T', '03021300T',
                       '04000000F', '04010500F', '04010800F', '04011000F', '04011600F')
AND    NOT EXISTS (SELECT 1 FROM FACT_JAYA2027.USRACS_HOSALES x
                   WHERE x.UID_ORA = 'CORETEAM' AND x.MNUID = m.ID AND x.INDEX_ORA = m.INDEX_ORA);

-- 4b. Same rights in FACT_JSTIL2027 (HS login HEAD_OFFICE, LOCA JST -- the HO data). Only where CORETEAM
--     already has HS "Inputs" there.
INSERT INTO FACT_JSTIL2027.USRACS_HOSALES
       (ID, LOCA, UID_ORA, MNUID, INDEX_ORA, AEDV, EDAY, ADAY, DDAY, USER_NAME, USER_ENTDT,
        OS_USER, TERMINAL_ID, SCHEMA_NM)
SELECT (SELECT NVL(MAX(ID), 0) FROM FACT_JSTIL2027.USRACS_HOSALES) + ROWNUM,
       'JST', 'CORETEAM', m.ID, m.INDEX_ORA, 'AEDV', 0, 0, 0, USER, SYSDATE,
       USER, 'setup_hs_reports.sql', 'FACT_JSTIL2027'
FROM   VBMENU_HOSALES m
WHERE  m.MODULE = 'HS'
AND    m.ORDERCODE IN ('03000000F', '03010000T', '03020100T', '03020900T', '03021300T',
                       '04000000F', '04010500F', '04010800F', '04011000F', '04011600F')
AND    EXISTS (SELECT 1 FROM FACT_JSTIL2027.USRACS_HOSALES h
               WHERE h.UID_ORA = 'CORETEAM' AND h.MNUID = 'HS_MAIN' AND h.INDEX_ORA = '1')
AND    NOT EXISTS (SELECT 1 FROM FACT_JSTIL2027.USRACS_HOSALES x
                   WHERE x.UID_ORA = 'CORETEAM' AND x.MNUID = m.ID AND x.INDEX_ORA = m.INDEX_ORA);

COMMIT;

-- 5. Verify
SELECT m.ID, m.INDEX_ORA, m.PID, m.ORDERCODE, m.NAME, m.TYPE, d.CONTROLLER, d.ACTIONMETHOD, d.ISACTIVE
FROM   VBMENU_HOSALES m
LEFT JOIN VBMENU_DONE d ON d.MENUCODE = m.ORDERCODE AND d.MODULECODE = 'HS'
WHERE  m.MODULE = 'HS'
ORDER  BY m.ORDERCODE;

-- =====================================================================
-- UNDO (not part of the setup)
-- =====================================================================
-- DELETE FROM FACT_JAYA2027.USRACS_HOSALES  WHERE MNUID IN ('HS_RPT0', 'HS_RPT', 'HS_RPTS', 'HS_MIS0', 'HS_MIS');
-- DELETE FROM FACT_JSTIL2027.USRACS_HOSALES WHERE MNUID IN ('HS_RPT0', 'HS_RPT', 'HS_RPTS', 'HS_MIS0', 'HS_MIS');
-- DELETE FROM VBMENU_DONE    WHERE MODULECODE = 'HS' AND MENUCODE IN ('03020100T', '03020900T', '03021300T', '04010500F', '04010800F', '04011000F', '04011600F');
-- DELETE FROM VBMENU_HOSALES WHERE MODULE = 'HS' AND ORDERCODE IN ('03000000F', '03010000T', '03020100T', '03020900T', '03021300T', '04000000F', '04010500F', '04010800F', '04011000F', '04011600F');
-- COMMIT;
