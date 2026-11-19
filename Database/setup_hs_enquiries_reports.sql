-- =====================================================================
-- HS (TeaSaleManagement) + TS (PacketSalesExport) -- "Enquiries & Reports" menus  (2026-11-10)
-- =====================================================================
-- HS: the two headings "Reports" and "MIS" become ONE heading "Enquiries & Reports":
--
--   Enquiries & Reports (heading)     03000000F   HS_RPT0 2  JAG1               (was "Reports")
--     Sales (folder)                  03010000T   HS_RPT  0  HS_RPT0!2
--       Tax Invoice Printing          03020100T   HS_RPTS 0  HS_RPT!0           TaxInvoicePrinting / Index
--       Export Invoice Printing (TT)  03020900T   HS_RPTS 8  HS_RPT!0           HIDDEN -- moved to TS (below)
--       Auction Release Printing      03021300T   HS_RPTS 12 HS_RPT!0           AuctionReleasePrinting / Index
--     Tax Invoice Register            04010500F   HS_MIS  5  HS_RPT0!2          TaxInvoiceRegister / Index     (PID was HS_MIS0!3)
--     Unsold List                     04010800F   HS_MIS  8  HS_RPT0!2          UnsoldList / Index
--     Souda Book Register             04011000F   HS_MIS  10 HS_RPT0!2          SoudaBookRegister / Index
--     Tea Purchase Register           04011600F   HS_MIS  16 HS_RPT0!2          HoTeaPurchaseRegister / Index
--   MIS (heading)                     04000000F   HS_MIS0 3  JAG1               HIDDEN (empty now)
--
-- TS: a new heading after "Export & Other Entries" (01060000F, JAGUAR 25):
--
--   Enquiries & Reports (heading)     01070000F   JAGUAR  26 JAG1
--     Export Invoice Printing         01071010T   PSE_RPT 0  JAGUAR!26          ExportInvoicePrinting / Index
--
-- Nothing is deleted and no ID / INDEX_ORA / ORDERCODE changes, so every user's existing rights rows
-- (USRACS_HOSALES, MNUID + INDEX_ORA) keep working: whoever sees Reports / MIS today sees them in the
-- new place. The menu API builds the tree from PID and sorts siblings by ORDERCODE.
-- Run as CLASSICNEW. Every statement can be run again. Users must open the menu page again (or log in).
-- =====================================================================

SET DEFINE OFF

-- 1. HS: "Reports" -> "Enquiries & Reports"
UPDATE VBMENU_HOSALES SET NAME = 'Enquiries & Reports'
WHERE  MODULE = 'HS' AND ORDERCODE = '03000000F' AND ID = 'HS_RPT0' AND NAME <> 'Enquiries & Reports';

-- 2. HS: the four MIS registers move directly under it (after the "Sales" folder); the "MIS" heading is hidden
UPDATE VBMENU_HOSALES SET PID = 'HS_RPT0!2'
WHERE  MODULE = 'HS' AND ID = 'HS_MIS' AND ORDERCODE IN ('04010500F', '04010800F', '04011000F', '04011600F')
AND    PID <> 'HS_RPT0!2';

UPDATE VBMENU_HOSALES SET PERDOTNETMENU = 'N', ATVDOTNETMENU = 'N'
WHERE  MODULE = 'HS' AND ORDERCODE = '04000000F' AND ID = 'HS_MIS0'
AND    (NVL(PERDOTNETMENU, 'Y') <> 'N' OR NVL(ATVDOTNETMENU, 'Y') <> 'N');

-- 3. HS: "Export Invoice Printing (TT)" hidden (like TS's Domestic / Birla Tea on 2026-11-04)
UPDATE VBMENU_HOSALES SET PERDOTNETMENU = 'N', ATVDOTNETMENU = 'N'
WHERE  MODULE = 'HS' AND ORDERCODE = '03020900T'
AND    (NVL(PERDOTNETMENU, 'Y') <> 'N' OR NVL(ATVDOTNETMENU, 'Y') <> 'N');

UPDATE VBMENU_DONE SET ISACTIVE = 0
WHERE  MODULECODE = 'HS' AND MENUCODE = '03020900T' AND NVL(ISACTIVE, 1) <> 0;

-- 4. TS: heading "Enquiries & Reports" (TYPE null = sub-menu) and its item (TYPE 'E' = screen);
--    display columns as in setup_ts_module.sql section 6
INSERT INTO VBMENU_TEA_SALES
       (ID, NAME, INDEX_ORA, PID, IMAGE, TAG, TYPE, SIMAGE, ORDERCODE, PGSZ, ORNT, FNTSZ, DTLOCK,
        TMPORDER, MENUTAG, PERDOTNETMENU, ATVDOTNETMENU, MODULE)
SELECT n.ID, n.NAME, n.IDX, n.PID, 2, 0, n.TP, 0, n.OC, 0, 0, 0, DECODE(n.TP, 'E', 'Y', NULL),
       0, 'TS', 'Y', 'Y', 'TS'
FROM  (SELECT '01070000F' OC, 'JAGUAR' ID, '26' IDX, 'JAG1' PID, 'Enquiries & Reports' NAME, CAST(NULL AS VARCHAR2(2)) TP FROM dual UNION ALL
       SELECT '01071010T', 'PSE_RPT', '0', 'JAGUAR!26', 'Export Invoice Printing', 'E' FROM dual) n
WHERE  NOT EXISTS (SELECT 1 FROM VBMENU_TEA_SALES x WHERE x.MODULE = 'TS' AND x.ORDERCODE = n.OC)
AND    NOT EXISTS (SELECT 1 FROM VBMENU_TEA_SALES x WHERE x.ID = n.ID AND x.INDEX_ORA = n.IDX);

-- 5. TS: screen mapping (CONTROLLER is also the AEDV key)
INSERT INTO VBMENU_DONE (ID, PROJECTNAME, MENUNAME, MENUCODE, CONTROLLER, ACTIONMETHOD, IS4SUBDOMAIN, MODULECODE, ISACTIVE)
SELECT (SELECT NVL(MAX(ID), 0) FROM VBMENU_DONE) + 1, 'TS', 'Export Invoice Printing', '01071010T', 'ExportInvoicePrinting', 'Index', 1, 'TS', 1
FROM dual WHERE NOT EXISTS (SELECT 1 FROM VBMENU_DONE x WHERE x.MODULECODE = 'TS' AND x.MENUCODE = '01071010T');

-- 6. TS rights: CORETEAM gets AEDV (a report needs only V) on the heading and the item, days 0.
--    To grant another user, repeat with that user id.
INSERT INTO FACT_JSTIL2027.USRACS_TEA_SALES
       (ID, LOCA, UID_ORA, MNUID, INDEX_ORA, AEDV, EDAY, ADAY, DDAY, USER_NAME, USER_ENTDT,
        OS_USER, TERMINAL_ID, SCHEMA_NM)
SELECT (SELECT NVL(MAX(ID), 0) FROM FACT_JSTIL2027.USRACS_TEA_SALES) + ROWNUM,
       'JST', 'CORETEAM', m.ID, m.INDEX_ORA, 'AEDV', 0, 0, 0, USER, SYSDATE,
       USER, 'setup_hs_enquiries_reports', 'FACT_JSTIL2027'
FROM   VBMENU_TEA_SALES m
WHERE  m.MODULE = 'TS' AND m.ORDERCODE IN ('01070000F', '01071010T')
AND    NOT EXISTS (SELECT 1 FROM FACT_JSTIL2027.USRACS_TEA_SALES x
                   WHERE x.UID_ORA = 'CORETEAM' AND x.MNUID = m.ID AND x.INDEX_ORA = m.INDEX_ORA);

COMMIT;

-- 7. Verify
SELECT m.ID, m.INDEX_ORA, m.PID, m.ORDERCODE, m.NAME, m.TYPE, m.PERDOTNETMENU, d.CONTROLLER, d.ISACTIVE
FROM   VBMENU_HOSALES m
LEFT JOIN VBMENU_DONE d ON d.MENUCODE = m.ORDERCODE AND d.MODULECODE = 'HS'
WHERE  m.MODULE = 'HS' AND (m.ORDERCODE LIKE '03%' OR m.ORDERCODE LIKE '04%')
ORDER  BY m.ORDERCODE;

SELECT m.ID, m.INDEX_ORA, m.PID, m.ORDERCODE, m.NAME, m.TYPE, m.PERDOTNETMENU, d.CONTROLLER, d.ISACTIVE
FROM   VBMENU_TEA_SALES m
LEFT JOIN VBMENU_DONE d ON d.MENUCODE = m.ORDERCODE AND d.MODULECODE = 'TS'
WHERE  m.MODULE = 'TS' AND m.ORDERCODE LIKE '0107%'
ORDER  BY m.ORDERCODE;

-- =====================================================================
-- UNDO (not part of the setup)
-- =====================================================================
-- UPDATE VBMENU_HOSALES SET NAME = 'Reports' WHERE MODULE = 'HS' AND ORDERCODE = '03000000F';
-- UPDATE VBMENU_HOSALES SET PID = 'HS_MIS0!3' WHERE MODULE = 'HS' AND ID = 'HS_MIS' AND ORDERCODE IN ('04010500F', '04010800F', '04011000F', '04011600F');
-- UPDATE VBMENU_HOSALES SET PERDOTNETMENU = 'Y', ATVDOTNETMENU = 'Y' WHERE MODULE = 'HS' AND ORDERCODE = '04000000F';
-- UPDATE VBMENU_HOSALES SET PERDOTNETMENU = 'Y', ATVDOTNETMENU = 'Y' WHERE MODULE = 'HS' AND ORDERCODE = '03020900T';
-- UPDATE VBMENU_DONE    SET ISACTIVE = 1 WHERE MODULECODE = 'HS' AND MENUCODE = '03020900T';
-- DELETE FROM FACT_JSTIL2027.USRACS_TEA_SALES WHERE (MNUID = 'JAGUAR' AND INDEX_ORA = '26') OR MNUID = 'PSE_RPT';
-- DELETE FROM VBMENU_DONE      WHERE MODULECODE = 'TS' AND MENUCODE = '01071010T';
-- DELETE FROM VBMENU_TEA_SALES WHERE MODULE = 'TS' AND ORDERCODE IN ('01070000F', '01071010T');
-- COMMIT;
