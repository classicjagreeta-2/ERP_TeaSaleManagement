-- =====================================================================
-- TeaSaleManagement (HS) -- every "Inputs" item moves under "Sample Tea > Inputs" (2026-11-17)
-- =====================================================================
-- The main "Inputs" heading (02000000F, HS_MAIN 1) is left empty, so it is hidden (PERDOTNETMENU /
-- ATVDOTNETMENU 'N', as the old "MIS" heading). Its items get PID HS_SMP!0 and an ORDERCODE in the
-- 0501xxxxT range (VBMENU_DONE.MENUCODE follows). ID / INDEX_ORA are not changed, so every user's
-- rights rows (USRACS_HOSALES: MNUID + INDEX_ORA) stay valid. The two folders keep their own children
-- (PID HS_INP!3 / HS_INP!4); only the children's ORDERCODEs change, to sort inside the new range.
--
--   Sample Tea > Inputs (05010000T)
--     Sample Drawing Entry        05010100T   (already there)
--     Tea Sample Receive Entry    05010200T   (already there)
--     Tea Tasting Entry           05010300T   was 02040200T
--     Outturn Sample Issue        05010400T   was 02040250T
--     Sauda Entry                 05010500T   was 02040300T
--     Confirmation Entry          05010600T   was 02040400T  (folder)
--       Domestic                  05010601T   was 02040401T
--     Sales Invoice Entry         05010700T   was 02040500T  (folder)
--       Private Sale              05010701T   was 02040501T
--       Auction Sale              05010703T   was 02040503T
--     Auction Release             05010800T   was 02040600T
--     Auction Catalogue Entry     05010900T   was 02040700T
--     Withdrawl Entry             05011000T   was 02040800T
--
-- Run as CLASSICNEW. Re-runnable: rows already moved are skipped.
-- =====================================================================

-- 1. New codes for the rows (direct items get PID HS_SMP!0; folder children keep their PID)
UPDATE VBMENU_HOSALES m
   SET ORDERCODE = (SELECT n.NEWC FROM (
                      SELECT '02040200T' OLDC, '05010300T' NEWC FROM dual UNION ALL
                      SELECT '02040250T', '05010400T' FROM dual UNION ALL
                      SELECT '02040300T', '05010500T' FROM dual UNION ALL
                      SELECT '02040400T', '05010600T' FROM dual UNION ALL
                      SELECT '02040401T', '05010601T' FROM dual UNION ALL
                      SELECT '02040500T', '05010700T' FROM dual UNION ALL
                      SELECT '02040501T', '05010701T' FROM dual UNION ALL
                      SELECT '02040503T', '05010703T' FROM dual UNION ALL
                      SELECT '02040600T', '05010800T' FROM dual UNION ALL
                      SELECT '02040700T', '05010900T' FROM dual UNION ALL
                      SELECT '02040800T', '05011000T' FROM dual) n WHERE n.OLDC = m.ORDERCODE),
       PID = CASE WHEN m.PID = 'HS_MAIN!1' THEN 'HS_SMP!0' ELSE m.PID END
 WHERE m.MODULE = 'HS'
   AND m.ORDERCODE IN ('02040200T', '02040250T', '02040300T', '02040400T', '02040401T', '02040500T',
                       '02040501T', '02040503T', '02040600T', '02040700T', '02040800T');

-- 2. Screen mapping follows the code
UPDATE VBMENU_DONE d
   SET MENUCODE = (SELECT n.NEWC FROM (
                      SELECT '02040200T' OLDC, '05010300T' NEWC FROM dual UNION ALL
                      SELECT '02040250T', '05010400T' FROM dual UNION ALL
                      SELECT '02040300T', '05010500T' FROM dual UNION ALL
                      SELECT '02040400T', '05010600T' FROM dual UNION ALL
                      SELECT '02040401T', '05010601T' FROM dual UNION ALL
                      SELECT '02040500T', '05010700T' FROM dual UNION ALL
                      SELECT '02040501T', '05010701T' FROM dual UNION ALL
                      SELECT '02040503T', '05010703T' FROM dual UNION ALL
                      SELECT '02040600T', '05010800T' FROM dual UNION ALL
                      SELECT '02040700T', '05010900T' FROM dual UNION ALL
                      SELECT '02040800T', '05011000T' FROM dual) n WHERE n.OLDC = d.MENUCODE)
 WHERE d.MODULECODE = 'HS'
   AND d.MENUCODE IN ('02040200T', '02040250T', '02040300T', '02040400T', '02040401T', '02040500T',
                      '02040501T', '02040503T', '02040600T', '02040700T', '02040800T');

-- 3. Hide the now-empty "Inputs" heading
UPDATE VBMENU_HOSALES SET PERDOTNETMENU = 'N', ATVDOTNETMENU = 'N'
 WHERE MODULE = 'HS' AND ORDERCODE = '02000000F'
   AND NOT EXISTS (SELECT 1 FROM VBMENU_HOSALES x WHERE x.MODULE = 'HS' AND x.PID = 'HS_MAIN!1');

COMMIT;

-- 4. Verify
SELECT m.ID, m.INDEX_ORA, m.PID, m.ORDERCODE, m.NAME, m.PERDOTNETMENU, d.CONTROLLER
FROM   VBMENU_HOSALES m
LEFT JOIN VBMENU_DONE d ON d.MENUCODE = m.ORDERCODE AND d.MODULECODE = 'HS'
WHERE  m.MODULE = 'HS' AND (m.ORDERCODE LIKE '05%' OR m.ORDERCODE LIKE '02%')
ORDER  BY m.ORDERCODE;

-- =====================================================================
-- UNDO (not part of the setup): run steps 1 and 2 with OLDC / NEWC swapped (and PID 'HS_SMP!0' ->
-- 'HS_MAIN!1' for the direct items, i.e. not the four folder children), then:
-- UPDATE VBMENU_HOSALES SET PERDOTNETMENU = 'Y', ATVDOTNETMENU = 'Y' WHERE MODULE = 'HS' AND ORDERCODE = '02000000F';
-- COMMIT;
-- =====================================================================
