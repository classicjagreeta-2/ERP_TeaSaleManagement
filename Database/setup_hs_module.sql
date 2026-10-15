-- =====================================================================
-- TeaSaleManagement -- module HS ("Tea Sale Management") menu setup
-- =====================================================================
-- Run as CLASSICNEW on CLASSICDEV (SQL*Plus / SQL Developer "Run Script"). Every statement is
-- safe to run again (it skips rows that already exist). Section 9 at the end takes it all out again.
--
-- How the menu is built (ClassicERPCoreAPI MenuController.GetAllByModule):
--   CLASSICNEW.MODULE              MODULE_CODE 'HS' -> MENU_TABLE VBMENU_HOSALES,
--                                  USRACS_TABLE USRACS_HOSALES, MENU_SORT_BY 'JAG1'
--   CLASSICNEW.VBMENU_HOSALES      the module's menu tree (rows with MODULE = 'HS')
--   CLASSICNEW.VBMENU_DONE         ORDERCODE (= MENUCODE) -> MVC controller / action
--   <factory schema>.USRACS_HOSALES  one row per user per menu entry (MNUID + INDEX_ORA);
--                                  a user only sees entries he has a row for, parents included.
--                                  The factory schema is the company picked after login --
--                                  FACT_JAYA2027 for this module.
--
-- The menu is the VB HO package's "2. Inputs" (VBMENU_HO, ID CLASSIC!1 / ENTRYWH / ENTSALE) for
-- the items that are ported: same ORDERCODE, same INDEX_ORA, same order, captions without the VB
-- numbering. The menu IDs are new ones (HS_MAIN / HS_INP / HS_SALE) on purpose: VBMENU_HOSALES
-- holds 108 stray MODULE = 'IN' rows -- reusing a VB ID could hand the new screens to whoever
-- holds old rights rows on it.
--
--   VB form                            menu entry                     controller / action
--   (heading, VBMENU_HO CLASSIC!1)     Inputs
--   (heading, ENTRYWH 4)                 Sales Invoice Entry
--   trn_salesInvoice (pSale_tag "")        Private Sale               PrivateSaleInvoice / Index
--   trn_salesInvoice (pSale_tag "4")       Auction Sale               AuctionSaleInvoice / Index
--   TRN_AUCTION_RELEASE                  Auction Release              AuctionRelease / Index
--   TRN_CATALOGUE (pCatalouge_Type HO)   Auction Catalogue Entry      AuctionCatalogue / Index
--   trn_withdrawl                        Withdrawl Entry              WithdrawlEntry / Index
--   TRN_SAMPLE_RECEIVE                   Tea Sample Receive Entry     TeaSampleReceive / Index
--   TRN_SAUDA (pConf_tag D)              Sauda Entry                  SaudaEntry / Index
-- =====================================================================

-- ---------------------------------------------------------------------
-- 0. Module row: HS -> VBMENU_HOSALES / USRACS_HOSALES (same shape as WH's row).
-- ---------------------------------------------------------------------
INSERT INTO MODULE
       (MODULE_NAME, MENU_TABLE, CONTROL_TABLE, MENU_SORT_BY, PERMISSION, MODULE_CODE, USRACS_TABLE,
        ID, LOCA, TECHNOLOGY, HOME_PAGE, LOGOUT_PAGE, IS_SUBMENU, UNIT_TABLE, UNIT_FIELD)
SELECT 'Tea Sale Management', 'VBMENU_HOSALES', w.CONTROL_TABLE, w.MENU_SORT_BY, w.PERMISSION, 'HS',
       'USRACS_HOSALES', (SELECT NVL(MAX(ID), 0) + 1 FROM MODULE), w.LOCA, w.TECHNOLOGY, w.HOME_PAGE,
       w.LOGOUT_PAGE, w.IS_SUBMENU, w.UNIT_TABLE, w.UNIT_FIELD
FROM   MODULE w
WHERE  w.MODULE_CODE = 'WH'
AND    NOT EXISTS (SELECT 1 FROM MODULE x WHERE x.MODULE_CODE = 'HS');

-- ---------------------------------------------------------------------
-- 1. Menu tree: "Inputs" > 6 entries, "Sales Invoice Entry" > Private Sale / Auction Sale.
--    IMAGE, TAG, TYPE, DTLOCK ... are taken from the VB menu row (VBMENU_HO).
-- ---------------------------------------------------------------------
INSERT INTO VBMENU_HOSALES
       (ID, NAME, INDEX_ORA, PID, IMAGE, TAG, TYPE, SIMAGE, ORDERCODE, PRNM, PGSZ, ORNT, FNTSZ,
        FNTNM, DTLOCK, MACHINE_NO, TMPORDER, FTEXT, MENUTAG, PERDOTNETMENU, ATVDOTNETMENU, MODULE)
SELECT n.NEW_ID, n.NEW_NAME, h.INDEX_ORA, n.NEW_PID, h.IMAGE, h.TAG, h.TYPE, h.SIMAGE, h.ORDERCODE,
       h.PRNM, h.PGSZ, h.ORNT, h.FNTSZ, h.FNTNM, h.DTLOCK, h.MACHINE_NO, h.TMPORDER, h.FTEXT,
       'HS', 'Y', 'Y', 'HS'
FROM  (SELECT 'CLASSIC' VB_ID, '1' VB_IDX, '02000000F' OC, 'HS_MAIN' NEW_ID, 'JAG1' NEW_PID, 'Inputs' NEW_NAME FROM dual UNION ALL
       SELECT 'ENTRYWH', '4',  '02040500T', 'HS_INP',  'HS_MAIN!1', 'Sales Invoice Entry'      FROM dual UNION ALL
       SELECT 'ENTSALE', '0',  '02040501T', 'HS_SALE', 'HS_INP!4',  'Private Sale'             FROM dual UNION ALL
       SELECT 'ENTSALE', '2',  '02040503T', 'HS_SALE', 'HS_INP!4',  'Auction Sale'             FROM dual UNION ALL
       SELECT 'ENTRYWH', '5',  '02040600T', 'HS_INP',  'HS_MAIN!1', 'Auction Release'          FROM dual UNION ALL
       SELECT 'ENTRYWH', '6',  '02040700T', 'HS_INP',  'HS_MAIN!1', 'Auction Catalogue Entry'  FROM dual UNION ALL
       SELECT 'ENTRYWH', '7',  '02040800T', 'HS_INP',  'HS_MAIN!1', 'Withdrawl Entry'          FROM dual UNION ALL
       SELECT 'ENTRYWH', '8',  '02040900T', 'HS_INP',  'HS_MAIN!1', 'Tea Sample Receive Entry' FROM dual UNION ALL
       SELECT 'ENTRYWH', '37', '02043800T', 'HS_INP',  'HS_MAIN!1', 'Sauda Entry'              FROM dual) n
JOIN   VBMENU_HO h ON h.ID = n.VB_ID AND h.INDEX_ORA = n.VB_IDX AND h.ORDERCODE = n.OC
WHERE  NOT EXISTS (SELECT 1 FROM VBMENU_HOSALES x WHERE x.MODULE = 'HS' AND x.ORDERCODE = n.OC);

-- ---------------------------------------------------------------------
-- 2. Screen mapping: menu code -> controller / action. CONTROLLER is also the screen's AEDV
--    key and its record-lock name. One row per menu code (the menu API reads it with
--    SingleOrDefault); the two headings (02000000F, 02040500T) have none.
-- ---------------------------------------------------------------------
INSERT INTO VBMENU_DONE
       (ID, PROJECTNAME, MENUNAME, MENUCODE, CONTROLLER, ACTIONMETHOD, IS4SUBDOMAIN, MODULECODE, ISACTIVE)
SELECT (SELECT NVL(MAX(ID), 0) FROM VBMENU_DONE) + ROWNUM, 'HS', m.NAME, n.OC, n.CTRL, n.ACT, 1, 'HS', 1
FROM  (SELECT '02040501T' OC, 'PrivateSaleInvoice' CTRL, 'Index' ACT FROM dual UNION ALL
       SELECT '02040503T', 'AuctionSaleInvoice', 'Index' FROM dual UNION ALL
       SELECT '02040600T', 'AuctionRelease',     'Index' FROM dual UNION ALL
       SELECT '02040700T', 'AuctionCatalogue',   'Index' FROM dual UNION ALL
       SELECT '02040800T', 'WithdrawlEntry',     'Index' FROM dual UNION ALL
       SELECT '02040900T', 'TeaSampleReceive',   'Index' FROM dual UNION ALL
       SELECT '02043800T', 'SaudaEntry',         'Index' FROM dual) n
JOIN   VBMENU_HOSALES m ON m.ORDERCODE = n.OC AND m.MODULE = 'HS'
WHERE  NOT EXISTS (SELECT 1 FROM VBMENU_DONE x WHERE x.MODULECODE = 'HS' AND x.MENUCODE = n.OC);

-- ---------------------------------------------------------------------
-- 3. Company / year row for module HS on FACT_JAYA2027: a copy of the schema's BL row (same
--    year, LOCA, sales / finance schemas). Company selection after login reads CLASSIC_CONTROL
--    where MODULE = 'HS'.
-- ---------------------------------------------------------------------
INSERT INTO CLASSIC_CONTROL
       (SRLNO, NAME, YRFR, YRTO, ACFIL, ACMFIL, ACREF, SIMB, YR, ADD1, CITY, PIN, UNITDEST, USR_ID,
        LUSR_ID, DATAVER, EXP_TAG, IMP_TAG, MERGE_TAG, MIRROR_TAG, TRIG_TAG, SCHEMA_SALES,
        SCHEMA_FINANCE, SCHEMA_PROD, SCHEMA_PAY, SCHEMA_INV, SCHEMA_FACTORY, MAIN_UNIT, VNAME,
        ACTIVE_TAG, GRP, MODULE, LOCA, SCHEMA_RM, ID, MAIN_UNITDEST, SCHEMA_TYPE)
SELECT c.SRLNO, c.NAME, c.YRFR, c.YRTO, c.ACFIL, c.ACMFIL, c.ACREF, c.SIMB, c.YR, c.ADD1, c.CITY, c.PIN,
       c.UNITDEST, c.USR_ID, c.LUSR_ID, c.DATAVER, c.EXP_TAG, c.IMP_TAG, c.MERGE_TAG, c.MIRROR_TAG,
       c.TRIG_TAG, c.SCHEMA_SALES, c.SCHEMA_FINANCE, c.SCHEMA_PROD, c.SCHEMA_PAY, c.SCHEMA_INV,
       c.SCHEMA_FACTORY, c.MAIN_UNIT, c.VNAME, c.ACTIVE_TAG, c.GRP, 'HS', c.LOCA, c.SCHEMA_RM,
       (SELECT NVL(MAX(ID), 0) + 1 FROM CLASSIC_CONTROL), c.MAIN_UNITDEST, c.SCHEMA_TYPE
FROM   CLASSIC_CONTROL c
WHERE  c.MODULE = 'BL' AND c.USR_ID = 'FACT_JAYA2027'
AND    NOT EXISTS (SELECT 1 FROM CLASSIC_CONTROL x WHERE x.MODULE = 'HS' AND x.USR_ID = c.USR_ID);

-- ---------------------------------------------------------------------
-- 4. Rights table: FACT_JAYA2027 has no USRACS_HOSALES yet -- create it with the layout of
--    FACT_JSTIL2027.USRACS_HOSALES (no rows copied). Skipped when it already exists.
-- ---------------------------------------------------------------------
BEGIN
  EXECUTE IMMEDIATE 'CREATE TABLE FACT_JAYA2027.USRACS_HOSALES AS SELECT * FROM FACT_JSTIL2027.USRACS_HOSALES WHERE 1 = 0';
EXCEPTION
  WHEN OTHERS THEN
    IF SQLCODE != -955 THEN RAISE; END IF;   -- ORA-00955: name is already used by an existing object
END;
/

-- ---------------------------------------------------------------------
-- 5. Rights: CORETEAM gets AEDV on every HS entry (headings included), back-date days 0
--    (= today only, like its other rows). To grant another user, repeat with that user id.
-- ---------------------------------------------------------------------
INSERT INTO FACT_JAYA2027.USRACS_HOSALES
       (ID, LOCA, UID_ORA, MNUID, INDEX_ORA, AEDV, EDAY, ADAY, DDAY, USER_NAME, USER_ENTDT,
        OS_USER, TERMINAL_ID, SCHEMA_NM)
SELECT (SELECT NVL(MAX(ID), 0) FROM FACT_JAYA2027.USRACS_HOSALES) + ROWNUM,
       'JAY', 'CORETEAM', m.ID, m.INDEX_ORA, 'AEDV', 0, 0, 0, USER, SYSDATE,
       USER, 'setup_hs_module.sql', 'FACT_JAYA2027'
FROM   VBMENU_HOSALES m
WHERE  m.MODULE = 'HS'
AND    NOT EXISTS (SELECT 1 FROM FACT_JAYA2027.USRACS_HOSALES x
                   WHERE x.UID_ORA = 'CORETEAM' AND x.MNUID = m.ID AND x.INDEX_ORA = m.INDEX_ORA);

-- ---------------------------------------------------------------------
-- 6. Module + unit link for CORETEAM: one USER_SCHEMA_LINK row (MODULE_CODE 'HS', schema
--    FACT_JAYA2027, unit JAYF = FACTORY-JAYANTIKA) makes the module and company selectable and
--    grants the factory unit. CORETEAM already holds JAYB (BOUGHT LEAF-JAYANTIK) on this schema
--    through its BL row -- the screens read a user's units from every USER_SCHEMA_LINK row of the
--    schema, whatever the module, so CORETEAM sees JAYB and JAYF.
-- ---------------------------------------------------------------------
INSERT INTO USER_SCHEMA_LINK
       (LOCA, UNIT, USER_ORA, USR_ID, MODULE_NAME, ENTDT, TIME_ORA, MACHINE_NO, API_ALLOW, MODULE_CODE, TECHNOLOGY)
SELECT 'JST', 'JAYF', 'CORETEAM', 'FACT_JAYA2027', 'Tea Sale Management', TRUNC(SYSDATE), TO_CHAR(SYSDATE, 'HH24:MI'),
       'setup_hs_module.sql', 1, 'HS', 'MVC'      -- ID is an identity column
FROM   dual
WHERE  NOT EXISTS (SELECT 1 FROM USER_SCHEMA_LINK x
                   WHERE x.USER_ORA = 'CORETEAM' AND x.USR_ID = 'FACT_JAYA2027' AND x.MODULE_CODE = 'HS');

COMMIT;

-- ---------------------------------------------------------------------
-- 6b. Master data (2026-10-14): FACT_JAYA2027.M_SALETYPE is a copy of FACT_JSTIL2027's, so the
--     Auction Sale type '4' carries UNIT 'JSTI', a unit this schema does not have. VB (and the
--     Auction Sale screen) only offers type '4' when its UNIT is null or the document's unit, so
--     the screen said "No Auction Sale type is set up for Unit JAYB". Null = every unit of the
--     schema. Undo: SET UNIT = 'JSTI'.
-- ---------------------------------------------------------------------
UPDATE FACT_JAYA2027.M_SALETYPE SET UNIT = NULL WHERE TRIM(CODE) = '4' AND TRIM(UNIT) = 'JSTI';

COMMIT;

-- ---------------------------------------------------------------------
-- 7. Verify: the HS menu with its mapping, and what CORETEAM's menu is built from
-- ---------------------------------------------------------------------
SELECT m.ID, m.INDEX_ORA, m.PID, m.ORDERCODE, m.NAME, m.TYPE, d.CONTROLLER, d.ACTIONMETHOD, d.ISACTIVE
FROM   VBMENU_HOSALES m
LEFT JOIN VBMENU_DONE d ON d.MENUCODE = m.ORDERCODE AND d.MODULECODE = 'HS'
WHERE  m.MODULE = 'HS'
ORDER  BY m.ORDERCODE;

SELECT m.ORDERCODE, m.NAME, p.AEDV, p.ADAY, p.EDAY, p.DDAY
FROM   FACT_JAYA2027.USRACS_HOSALES p
JOIN   VBMENU_HOSALES m ON m.ID = p.MNUID AND m.INDEX_ORA = p.INDEX_ORA
WHERE  p.UID_ORA = 'CORETEAM' AND m.MODULE = 'HS' AND m.PERDOTNETMENU = 'Y'
ORDER  BY m.ORDERCODE;

-- =====================================================================
-- 9. UNDO -- not part of the setup; run only to take the HS module out again.
-- =====================================================================
-- DELETE FROM FACT_JAYA2027.USRACS_HOSALES WHERE MNUID IN ('HS_MAIN', 'HS_INP', 'HS_SALE');
-- DELETE FROM USER_SCHEMA_LINK WHERE MODULE_CODE = 'HS' AND USER_ORA = 'CORETEAM' AND USR_ID = 'FACT_JAYA2027';
-- DELETE FROM VBMENU_DONE      WHERE MODULECODE = 'HS';
-- DELETE FROM VBMENU_HOSALES   WHERE MODULE = 'HS';
-- DELETE FROM CLASSIC_CONTROL  WHERE MODULE = 'HS' AND USR_ID = 'FACT_JAYA2027';
-- DELETE FROM MODULE           WHERE MODULE_CODE = 'HS';
-- DROP TABLE FACT_JAYA2027.USRACS_HOSALES;   -- only if nothing else has been granted in it
-- COMMIT;
