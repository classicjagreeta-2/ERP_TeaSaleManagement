-- =====================================================================
-- TeaSaleManagement -- module HS: remove the 9 "Masters" sub-menu entries (the "Masters" heading HS_MST0 is kept)
-- =====================================================================
-- Run as CLASSICNEW on CLASSICTRIAL. Removes the menu rows (HS_MSTE), their screen mappings and CORETEAM's
-- rights rows in both HS companies. To bring them back, run setup_hs_masters.sql (and grant_hs_jstil rights again).

DELETE FROM FACT_JAYA2027.USRACS_HOSALES WHERE MNUID = 'HS_MSTE';
DELETE FROM FACT_JSTIL2027.USRACS_HOSALES WHERE MNUID = 'HS_MSTE';
DELETE FROM VBMENU_DONE    WHERE MODULECODE = 'HS' AND MENUCODE IN ('01050100T', '01050200T', '01100200T', '03000130T',
                                 '03000140T', '03000150T', '03000155T', '03000160T', '03000170T');
DELETE FROM VBMENU_HOSALES WHERE MODULE = 'HS' AND ID = 'HS_MSTE';
COMMIT;

-- Verify: only the heading 01000000F and the Inputs rows should be left
SELECT ORDERCODE, NAME FROM VBMENU_HOSALES WHERE MODULE = 'HS' ORDER BY ORDERCODE;

-- Also remove the empty "Masters" heading:
-- DELETE FROM FACT_JAYA2027.USRACS_HOSALES WHERE MNUID = 'HS_MST0';
-- DELETE FROM FACT_JSTIL2027.USRACS_HOSALES WHERE MNUID = 'HS_MST0';
-- DELETE FROM VBMENU_HOSALES WHERE MODULE = 'HS' AND ID = 'HS_MST0';
-- COMMIT;
