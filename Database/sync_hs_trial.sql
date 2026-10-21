-- =====================================================================
-- TeaSaleManagement -- copy the HS (Tea Sale Management) CLASSICNEW setup from CLASSICDEV to
-- CLASSICTRIAL, as it stood on CLASSICDEV on 2026-10-21
-- =====================================================================
-- Run as CLASSICNEW on CLASSICTRIAL. Every statement skips rows that already exist, so it is safe
-- to run again. IDs are taken from CLASSICTRIAL's own tables (MAX + 1); USER_SCHEMA_LINK.ID is an
-- identity column and is left to the database.
--
-- A comparison on 2026-10-21 found CLASSICTRIAL already had everything in FACT_JAYA2027
-- (USRACS_HOSALES with CORETEAM's 19 rights rows, M_SALETYPE '4' UNIT null, M_SALESCENTRE 0001
-- ACODE, the lock columns). What it lacked was the CLASSICNEW part, copied here:
--   MODULE row HS, VBMENU_HOSALES (19 rows: Inputs from setup_hs_module.sql + the Masters menu
--   added on CLASSICDEV later), VBMENU_DONE (16 rows), CLASSIC_CONTROL HS row for FACT_JAYA2027,
--   USER_SCHEMA_LINK CORETEAM / FACT_JAYA2027 / JAYF / HS.
-- =====================================================================

-- 1. MODULE (CLASSICTRIAL's MODULE has no UNIT_FIELD column -- CLASSICDEV's row has 'CODE,NAME' there)
INSERT INTO MODULE
       (MODULE_NAME, MENU_TABLE, CONTROL_TABLE, MENU_SORT_BY, PERMISSION, MODULE_CODE, USRACS_TABLE,
        ID, LOCA, TECHNOLOGY, HOME_PAGE, LOGOUT_PAGE, IS_SUBMENU, UNIT_TABLE)
SELECT 'Tea Sale Management', 'VBMENU_HOSALES', 'CLASSIC_CONTROL', 'JAG1', 'Y', 'HS', 'USRACS_HOSALES',
       (SELECT NVL(MAX(ID), 0) + 1 FROM MODULE), NULL, 'MVC', NULL, NULL, NULL, 'M_UNIT'
FROM   dual
WHERE  NOT EXISTS (SELECT 1 FROM MODULE x WHERE x.MODULE_CODE = 'HS');

-- 2. VBMENU_HOSALES (MODULE = 'HS')
INSERT INTO VBMENU_HOSALES
       (ID, NAME, INDEX_ORA, PID, IMAGE, TAG, TYPE, SIMAGE, ORDERCODE, PGSZ, ORNT, FNTSZ,
        DTLOCK, TMPORDER, MENUTAG, PERDOTNETMENU, ATVDOTNETMENU, MODULE)
SELECT n.ID, n.NAME, n.IDX, n.PID, 2, 0, n.TYP, 0, n.OC, 0, 0, 0, n.DTL, 0, 'HS', 'Y', 'Y', 'HS'
FROM  (SELECT 'HS_MST0' ID, 'Masters' NAME, '0' IDX, 'JAG1' PID, NULL TYP, '01000000F' OC, NULL DTL FROM dual UNION ALL
       SELECT 'HS_MSTE', 'Vehicle Type Master',      '0',  'HS_MST0!0', 'E', '01050100T', 'Y' FROM dual UNION ALL
       SELECT 'HS_MSTE', 'Vehicle Master',           '1',  'HS_MST0!0', 'E', '01050200T', 'Y' FROM dual UNION ALL
       SELECT 'HS_MSTE', 'Transporter Master',       '2',  'HS_MST0!0', 'E', '01100200T', 'Y' FROM dual UNION ALL
       SELECT 'HS_MAIN', 'Inputs',                   '1',  'JAG1',      NULL, '02000000F', NULL FROM dual UNION ALL
       SELECT 'HS_INP',  'Sales Invoice Entry',      '4',  'HS_MAIN!1', NULL, '02040500T', 'Y' FROM dual UNION ALL
       SELECT 'HS_SALE', 'Private Sale',             '0',  'HS_INP!4',  'E', '02040501T', 'Y' FROM dual UNION ALL
       SELECT 'HS_SALE', 'Auction Sale',             '2',  'HS_INP!4',  'E', '02040503T', 'Y' FROM dual UNION ALL
       SELECT 'HS_INP',  'Auction Release',          '5',  'HS_MAIN!1', 'E', '02040600T', 'Y' FROM dual UNION ALL
       SELECT 'HS_INP',  'Auction Catalogue Entry',  '6',  'HS_MAIN!1', 'E', '02040700T', 'Y' FROM dual UNION ALL
       SELECT 'HS_INP',  'Withdrawl Entry',          '7',  'HS_MAIN!1', 'E', '02040800T', 'Y' FROM dual UNION ALL
       SELECT 'HS_INP',  'Tea Sample Receive Entry', '8',  'HS_MAIN!1', 'E', '02040900T', 'Y' FROM dual UNION ALL
       SELECT 'HS_INP',  'Sauda Entry',              '37', 'HS_MAIN!1', 'E', '02043800T', 'Y' FROM dual UNION ALL
       SELECT 'HS_MSTE', 'Clerk Master',             '3',  'HS_MST0!0', 'E', '03000130T', 'Y' FROM dual UNION ALL
       SELECT 'HS_MSTE', 'Route Master',             '4',  'HS_MST0!0', 'E', '03000140T', 'Y' FROM dual UNION ALL
       SELECT 'HS_MSTE', 'Centre Master',            '5',  'HS_MST0!0', 'E', '03000150T', 'Y' FROM dual UNION ALL
       SELECT 'HS_MSTE', 'Weighment Point Master',   '6',  'HS_MST0!0', 'E', '03000155T', 'Y' FROM dual UNION ALL
       SELECT 'HS_MSTE', 'Driver Master',            '7',  'HS_MST0!0', 'E', '03000160T', 'Y' FROM dual UNION ALL
       SELECT 'HS_MSTE', 'Helper/Loader Master',     '8',  'HS_MST0!0', 'E', '03000170T', 'Y' FROM dual) n
WHERE  NOT EXISTS (SELECT 1 FROM VBMENU_HOSALES x WHERE x.MODULE = 'HS' AND x.ORDERCODE = n.OC);

-- 3. VBMENU_DONE (MODULECODE = 'HS')
INSERT INTO VBMENU_DONE
       (ID, PROJECTNAME, MENUNAME, MENUCODE, CONTROLLER, ACTIONMETHOD, IS4SUBDOMAIN, MODULECODE, ISACTIVE)
SELECT (SELECT NVL(MAX(ID), 0) FROM VBMENU_DONE) + ROWNUM, 'HS', n.NAME, n.OC, n.CTRL, 'Index', 1, 'HS', 1
FROM  (SELECT '01050100T' OC, 'Vehicle Type Master' NAME, 'VehicleTypeMaster' CTRL FROM dual UNION ALL
       SELECT '01050200T', 'Vehicle Master',           'VehicleMaster'        FROM dual UNION ALL
       SELECT '01100200T', 'Transporter Master',       'TransporterMaster'    FROM dual UNION ALL
       SELECT '02040501T', 'Private Sale',             'PrivateSaleInvoice'   FROM dual UNION ALL
       SELECT '02040503T', 'Auction Sale',             'AuctionSaleInvoice'   FROM dual UNION ALL
       SELECT '02040600T', 'Auction Release',          'AuctionRelease'       FROM dual UNION ALL
       SELECT '02040700T', 'Auction Catalogue Entry',  'AuctionCatalogue'     FROM dual UNION ALL
       SELECT '02040800T', 'Withdrawl Entry',          'WithdrawlEntry'       FROM dual UNION ALL
       SELECT '02040900T', 'Tea Sample Receive Entry', 'TeaSampleReceive'     FROM dual UNION ALL
       SELECT '02043800T', 'Sauda Entry',              'SaudaEntry'           FROM dual UNION ALL
       SELECT '03000130T', 'Clerk Master',             'ClerkMaster'          FROM dual UNION ALL
       SELECT '03000140T', 'Route Master',             'RouteMaster'          FROM dual UNION ALL
       SELECT '03000150T', 'Centre Master',            'CentreMaster'         FROM dual UNION ALL
       SELECT '03000155T', 'Weighment Point Master',   'WeighmentPointMaster' FROM dual UNION ALL
       SELECT '03000160T', 'Driver Master',            'DriverMaster'         FROM dual UNION ALL
       SELECT '03000170T', 'Helper/Loader Master',     'HelperMaster'         FROM dual) n
WHERE  NOT EXISTS (SELECT 1 FROM VBMENU_DONE x WHERE x.MODULECODE = 'HS' AND x.MENUCODE = n.OC);

-- 4. CLASSIC_CONTROL: the HS company / year row for FACT_JAYA2027 (CLASSICDEV's values)
INSERT INTO CLASSIC_CONTROL
       (SRLNO, NAME, YRFR, YRTO, SIMB, YR, ADD1, UNITDEST, USR_ID, LUSR_ID, DATAVER, EXP_TAG, IMP_TAG,
        MERGE_TAG, MIRROR_TAG, TRIG_TAG, SCHEMA_SALES, SCHEMA_FINANCE, SCHEMA_PAY, SCHEMA_INV,
        SCHEMA_FACTORY, MAIN_UNIT, VNAME, ACTIVE_TAG, MODULE, LOCA, ID, MAIN_UNITDEST)
SELECT 24, 'Evergreen Bought Leaf Factory', TO_DATE('01/04/2026', 'DD/MM/YYYY'), TO_DATE('31/03/2027', 'DD/MM/YYYY'),
       'JST', '2027', 'Neptune Road, SKY way', 'JAYANTIKA', 'FACT_JAYA2027', 'FACT_JAYA2026', 1, 'N', 'N',
       'N', 'N', 'N', 'FIN_JSTIL2027', 'FIN_JSTGARDEN2027', 'PAY_JAYA_NOR2027', 'INVE_JSTGARDEN2027',
       'FACT_JAYA2027', 'JAYA', '.', 'Y', 'HS', 'JAY', (SELECT NVL(MAX(ID), 0) + 1 FROM CLASSIC_CONTROL), 'JAYANTIKA'
FROM   dual
WHERE  NOT EXISTS (SELECT 1 FROM CLASSIC_CONTROL x WHERE x.MODULE = 'HS' AND x.USR_ID = 'FACT_JAYA2027');

-- 5. USER_SCHEMA_LINK: CORETEAM sees module HS on FACT_JAYA2027, unit JAYF (ID = identity)
INSERT INTO USER_SCHEMA_LINK
       (LOCA, UNIT, USER_ORA, USR_ID, MODULE_NAME, ENTDT, TIME_ORA, MACHINE_NO, API_ALLOW, MODULE_CODE, TECHNOLOGY)
SELECT 'JST', 'JAYF', 'CORETEAM', 'FACT_JAYA2027', 'Tea Sale Management', TRUNC(SYSDATE), TO_CHAR(SYSDATE, 'HH24:MI'),
       'sync_hs_trial.sql', 1, 'HS', 'MVC'
FROM   dual
WHERE  NOT EXISTS (SELECT 1 FROM USER_SCHEMA_LINK x
                   WHERE x.USER_ORA = 'CORETEAM' AND x.USR_ID = 'FACT_JAYA2027' AND x.MODULE_CODE = 'HS');

COMMIT;

-- 6. Verify
SELECT m.ORDERCODE, m.NAME, d.CONTROLLER, p.AEDV
FROM   VBMENU_HOSALES m
LEFT JOIN VBMENU_DONE d ON d.MENUCODE = m.ORDERCODE AND d.MODULECODE = 'HS'
LEFT JOIN FACT_JAYA2027.USRACS_HOSALES p ON p.MNUID = m.ID AND p.INDEX_ORA = m.INDEX_ORA AND p.UID_ORA = 'CORETEAM'
WHERE  m.MODULE = 'HS'
ORDER  BY m.ORDERCODE;

-- UNDO (not part of the sync):
-- DELETE FROM USER_SCHEMA_LINK WHERE MODULE_CODE = 'HS' AND USER_ORA = 'CORETEAM' AND USR_ID = 'FACT_JAYA2027';
-- DELETE FROM CLASSIC_CONTROL  WHERE MODULE = 'HS' AND USR_ID = 'FACT_JAYA2027';
-- DELETE FROM VBMENU_DONE      WHERE MODULECODE = 'HS';
-- DELETE FROM VBMENU_HOSALES   WHERE MODULE = 'HS';
-- DELETE FROM MODULE           WHERE MODULE_CODE = 'HS';
-- COMMIT;
