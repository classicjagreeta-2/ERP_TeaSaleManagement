-- =====================================================================
-- TeaSaleManagement (HS) -- root menu "Sale" (2026-11-17)
-- =====================================================================
--   Sale (heading)                      06000000F   HS_SAL0  5   PID JAG1
--     Inputs (folder)                   06010000T   HS_SL    0   PID HS_SAL0!5
--       Sales Invoice Entry (folder)    06010100T   HS_INP   4   PID HS_SL!0     (was 05010700T)
--         Private Sale                  06010101T   HS_SALE  0   PID HS_INP!4    (was 05010701T)
--         Auction Sale                  06010103T   HS_SALE  2   PID HS_INP!4    (was 05010703T)
--       Auction Release                 06010200T   HS_INP   5   PID HS_SL!0     (was 05010800T)
--       Auction Catalogue Entry         06010300T   HS_INP   6   PID HS_SL!0     (was 05010900T)
--       Withdrawl Entry                 06010400T   HS_INP   7   PID HS_SL!0     (was 05011000T)
--     Enquiries & Reports (folder)      06020000T   HS_SLR   1   PID HS_SAL0!5
--       Tax Invoice Printing            06020100T   HS_RPTS  0   PID HS_SLR!1    (was 03020100T)
--       Export Invoice Printing (TT)    06020900T   HS_RPTS  8   PID HS_SLR!1    (was 03020900T, stays hidden)
--       Auction Release Printing        06021300T   HS_RPTS 12   PID HS_SLR!1    (was 03021300T)
--
-- The moved rows keep ID / INDEX_ORA, so every user's rights rows stay valid; VBMENU_DONE.MENUCODE follows the
-- new codes. The "Sales" report folder (03010000T, HS_RPT 0) is then empty and is hidden; the registers stay
-- under the top-level "Enquiries & Reports". Sample Tea > Inputs keeps Sample Drawing, Tea Sample Receive,
-- Tea Tasting, Outturn Sample, Sauda and Confirmation Entry.
-- Rights: CORETEAM gets AEDV (0 back-date days) on the new heading and the two new folders, in FACT_JAYA2027
-- and -- where CORETEAM already has HS "Inputs" -- FACT_JSTIL2027.
-- Run as CLASSICNEW. Re-runnable: existing / already-moved rows are skipped.
-- =====================================================================

-- 1. Heading "Sale" (copied from the "Sample Tea" heading) and its two folders (copied from Sample Tea's folders)
INSERT INTO VBMENU_HOSALES
       (ID, NAME, INDEX_ORA, PID, IMAGE, TAG, TYPE, SIMAGE, ORDERCODE, PRNM, PGSZ, ORNT, FNTSZ,
        FNTNM, DTLOCK, MACHINE_NO, TMPORDER, FTEXT, MENUTAG, PERDOTNETMENU, ATVDOTNETMENU, MODULE)
SELECT n.ID, n.NAME, n.IX, n.PID, h.IMAGE, h.TAG, NULL, h.SIMAGE, n.OC,
       h.PRNM, h.PGSZ, h.ORNT, h.FNTSZ, h.FNTNM, h.DTLOCK, h.MACHINE_NO, h.TMPORDER, h.FTEXT, 'HS', 'Y', 'Y', 'HS'
FROM   (SELECT 'HS_SAL0' ID, 'Sale' NAME, '5' IX, 'JAG1' PID, '06000000F' OC, '05000000F' SRC FROM dual UNION ALL
        SELECT 'HS_SL', 'Inputs', '0', 'HS_SAL0!5', '06010000T', '05010000T' FROM dual UNION ALL
        SELECT 'HS_SLR', 'Enquiries & Reports', '1', 'HS_SAL0!5', '06020000T', '05020000T' FROM dual) n
JOIN   VBMENU_HOSALES h ON h.MODULE = 'HS' AND h.ORDERCODE = n.SRC
WHERE  NOT EXISTS (SELECT 1 FROM VBMENU_HOSALES x WHERE x.MODULE = 'HS' AND x.ORDERCODE = n.OC)
AND    NOT EXISTS (SELECT 1 FROM VBMENU_HOSALES x WHERE x.MODULE = 'HS' AND x.ID = n.ID AND x.INDEX_ORA = n.IX);

-- 2. Move the rows (new code; direct items / reports get their new folder as PID, Sales Invoice children keep theirs)
UPDATE VBMENU_HOSALES m
   SET ORDERCODE = (SELECT n.NEWC FROM (
                      SELECT '05010700T' OLDC, '06010100T' NEWC FROM dual UNION ALL
                      SELECT '05010701T', '06010101T' FROM dual UNION ALL
                      SELECT '05010703T', '06010103T' FROM dual UNION ALL
                      SELECT '05010800T', '06010200T' FROM dual UNION ALL
                      SELECT '05010900T', '06010300T' FROM dual UNION ALL
                      SELECT '05011000T', '06010400T' FROM dual UNION ALL
                      SELECT '03020100T', '06020100T' FROM dual UNION ALL
                      SELECT '03020900T', '06020900T' FROM dual UNION ALL
                      SELECT '03021300T', '06021300T' FROM dual) n WHERE n.OLDC = m.ORDERCODE),
       PID = CASE WHEN m.PID = 'HS_SMP!0' THEN 'HS_SL!0'
                  WHEN m.PID = 'HS_RPT!0' THEN 'HS_SLR!1'
                  ELSE m.PID END
 WHERE m.MODULE = 'HS'
   AND m.ORDERCODE IN ('05010700T', '05010701T', '05010703T', '05010800T', '05010900T', '05011000T',
                       '03020100T', '03020900T', '03021300T');

-- 3. Screen mapping follows the code
UPDATE VBMENU_DONE d
   SET MENUCODE = (SELECT n.NEWC FROM (
                      SELECT '05010700T' OLDC, '06010100T' NEWC FROM dual UNION ALL
                      SELECT '05010701T', '06010101T' FROM dual UNION ALL
                      SELECT '05010703T', '06010103T' FROM dual UNION ALL
                      SELECT '05010800T', '06010200T' FROM dual UNION ALL
                      SELECT '05010900T', '06010300T' FROM dual UNION ALL
                      SELECT '05011000T', '06010400T' FROM dual UNION ALL
                      SELECT '03020100T', '06020100T' FROM dual UNION ALL
                      SELECT '03020900T', '06020900T' FROM dual UNION ALL
                      SELECT '03021300T', '06021300T' FROM dual) n WHERE n.OLDC = d.MENUCODE)
 WHERE d.MODULECODE = 'HS'
   AND d.MENUCODE IN ('05010700T', '05010701T', '05010703T', '05010800T', '05010900T', '05011000T',
                      '03020100T', '03020900T', '03021300T');

-- 4. Hide the now-empty "Sales" report folder
UPDATE VBMENU_HOSALES SET PERDOTNETMENU = 'N', ATVDOTNETMENU = 'N'
 WHERE MODULE = 'HS' AND ORDERCODE = '03010000T'
   AND NOT EXISTS (SELECT 1 FROM VBMENU_HOSALES x WHERE x.MODULE = 'HS' AND x.PID = 'HS_RPT!0');

-- 5. Rights: CORETEAM, AEDV, back-date days 0, on the heading and both folders.
INSERT INTO FACT_JAYA2027.USRACS_HOSALES
       (ID, LOCA, UID_ORA, MNUID, INDEX_ORA, AEDV, EDAY, ADAY, DDAY, USER_NAME, USER_ENTDT,
        OS_USER, TERMINAL_ID, SCHEMA_NM)
SELECT (SELECT NVL(MAX(ID), 0) FROM FACT_JAYA2027.USRACS_HOSALES) + ROWNUM,
       'JAY', 'CORETEAM', m.ID, m.INDEX_ORA, 'AEDV', 0, 0, 0, USER, SYSDATE,
       USER, 'setup_hs_sale_root.sql', 'FACT_JAYA2027'
FROM   VBMENU_HOSALES m
WHERE  m.MODULE = 'HS' AND m.ORDERCODE IN ('06000000F', '06010000T', '06020000T')
AND    NOT EXISTS (SELECT 1 FROM FACT_JAYA2027.USRACS_HOSALES x
                   WHERE x.UID_ORA = 'CORETEAM' AND x.MNUID = m.ID AND x.INDEX_ORA = m.INDEX_ORA);

INSERT INTO FACT_JSTIL2027.USRACS_HOSALES
       (ID, LOCA, UID_ORA, MNUID, INDEX_ORA, AEDV, EDAY, ADAY, DDAY, USER_NAME, USER_ENTDT,
        OS_USER, TERMINAL_ID, SCHEMA_NM)
SELECT (SELECT NVL(MAX(ID), 0) FROM FACT_JSTIL2027.USRACS_HOSALES) + ROWNUM,
       'JST', 'CORETEAM', m.ID, m.INDEX_ORA, 'AEDV', 0, 0, 0, USER, SYSDATE,
       USER, 'setup_hs_sale_root.sql', 'FACT_JSTIL2027'
FROM   VBMENU_HOSALES m
WHERE  m.MODULE = 'HS' AND m.ORDERCODE IN ('06000000F', '06010000T', '06020000T')
AND    EXISTS (SELECT 1 FROM FACT_JSTIL2027.USRACS_HOSALES h
               WHERE h.UID_ORA = 'CORETEAM' AND h.MNUID = 'HS_MAIN' AND h.INDEX_ORA = '1')
AND    NOT EXISTS (SELECT 1 FROM FACT_JSTIL2027.USRACS_HOSALES x
                   WHERE x.UID_ORA = 'CORETEAM' AND x.MNUID = m.ID AND x.INDEX_ORA = m.INDEX_ORA);

COMMIT;

-- 6. Verify
SELECT m.ID, m.INDEX_ORA, m.PID, m.ORDERCODE, m.NAME, m.PERDOTNETMENU, d.CONTROLLER
FROM   VBMENU_HOSALES m
LEFT JOIN VBMENU_DONE d ON d.MENUCODE = m.ORDERCODE AND d.MODULECODE = 'HS'
WHERE  m.MODULE = 'HS' AND m.ORDERCODE >= '03'
ORDER  BY m.ORDERCODE;

-- =====================================================================
-- UNDO (not part of the setup): run steps 2 and 3 with OLDC / NEWC swapped (PID 'HS_SL!0' -> 'HS_SMP!0',
-- 'HS_SLR!1' -> 'HS_RPT!0'), then:
-- UPDATE VBMENU_HOSALES SET PERDOTNETMENU = 'Y', ATVDOTNETMENU = 'Y' WHERE MODULE = 'HS' AND ORDERCODE = '03010000T';
-- DELETE FROM FACT_JAYA2027.USRACS_HOSALES  WHERE TERMINAL_ID = 'setup_hs_sale_root.sql';
-- DELETE FROM FACT_JSTIL2027.USRACS_HOSALES WHERE TERMINAL_ID = 'setup_hs_sale_root.sql';
-- DELETE FROM VBMENU_HOSALES WHERE MODULE = 'HS' AND ORDERCODE IN ('06000000F', '06010000T', '06020000T');
-- COMMIT;
-- =====================================================================
