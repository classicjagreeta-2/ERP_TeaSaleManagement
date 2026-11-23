-- =====================================================================
-- TeaSaleManagement (HS) -- root menu order: Sample Tea, Sale, then the rest (2026-11-17)
-- =====================================================================
-- Root headings (PID JAG1) are shown in ORDERCODE order. Sample Tea is 05000000F and Sale 06000000F; the
-- visible "Enquiries & Reports" heading (HS_RPT0 2) moves from 03000000F to 07000000F so it comes after them.
-- (02000000F Inputs and 04000000F MIS are hidden.) A heading has no VBMENU_DONE row, and rights rows key on
-- ID + INDEX_ORA, so nothing else changes. Run as CLASSICNEW; re-runnable.
-- =====================================================================
UPDATE VBMENU_HOSALES SET ORDERCODE = '07000000F'
 WHERE MODULE = 'HS' AND ID = 'HS_RPT0' AND INDEX_ORA = '2' AND ORDERCODE = '03000000F'
   AND NOT EXISTS (SELECT 1 FROM VBMENU_HOSALES x WHERE x.MODULE = 'HS' AND x.ORDERCODE = '07000000F');

COMMIT;

-- Verify
SELECT ID, INDEX_ORA, ORDERCODE, NAME, PERDOTNETMENU FROM VBMENU_HOSALES WHERE MODULE = 'HS' AND PID = 'JAG1' ORDER BY ORDERCODE;

-- UNDO: UPDATE VBMENU_HOSALES SET ORDERCODE = '03000000F' WHERE MODULE = 'HS' AND ID = 'HS_RPT0' AND INDEX_ORA = '2'; COMMIT;
