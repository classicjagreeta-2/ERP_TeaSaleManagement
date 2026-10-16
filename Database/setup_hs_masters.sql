-- =====================================================================
-- TeaSaleManagement -- module HS: menu "Masters" (factory / Bought Leaf masters), 2026-10-15
-- =====================================================================
-- Run as CLASSICNEW on CLASSICDEV after setup_hs_module.sql. Every statement is safe to run again (it skips
-- rows that already exist). Section 9 at the end takes it all out again.
--
-- The heading is the VB HO package's "1. Master" (VBMENU_HO CLASSIC!0, ORDERCODE 01000000F), so it sorts
-- before "Inputs" (02000000F). The entries keep the VB menu codes of their forms: the factory VB menu
-- (VBMENU_FACTORY: 6.1 Vehicle Type Master 01050100T, 6.2 Vehicle Master 01050200T, 10.2 Transporter Master
-- 01100200T) and the BL module's out-grower masters (VBMENU_BL_FACTORY 03000130T .. 03000170T). Weighment
-- Point Master is new (doc 15/10/26) and gets 03000155T (after Centre). The menu API reads VBMENU_DONE by
-- MODULECODE, so these codes do not clash with the BL rows of the same code. Menu IDs are new (HS_MST0 /
-- HS_MSTE), like the HS_MAIN / HS_INP rows.
--
--   menu entry                 ORDERCODE   controller / action (= AEDV key, record-lock screen)   table
--   Masters (heading)          01000000F
--     Vehicle Type Master      01050100T   VehicleTypeMaster / Index                            M_VTYPE
--     Vehicle Master           01050200T   VehicleMaster / Index                                M_VEHMAS
--     Transporter Master       01100200T   TransporterMaster / Index                            M_TRANSP
--     Clerk Master             03000130T   ClerkMaster / Index                                  M_CLERK
--     Route Master             03000140T   RouteMaster / Index                                  M_ROUTE
--     Centre Master            03000150T   CentreMaster / Index                                 M_CENTRE
--     Weighment Point Master   03000155T   WeighmentPointMaster / Index                         M_WEIGHMENT_POINT
--     Driver Master            03000160T   DriverMaster / Index                                 M_DRIVER
--     Helper/Loader Master     03000170T   HelperMaster / Index                                 M_HELPER
--
-- The tables already exist in FACT_JAYA2027 (made 2026-10-14 / 15 with their sequences, insert triggers and
-- history tables); this script creates none.
-- =====================================================================

-- ---------------------------------------------------------------------
-- 1. Menu tree: "Masters" > 9 entries. IMAGE / TAG / SIMAGE ... as the HS "Withdrawl Entry" row (TYPE 'E' =
--    an entry screen); the heading as the HS "Inputs" heading.
-- ---------------------------------------------------------------------
INSERT INTO VBMENU_HOSALES
       (ID, NAME, INDEX_ORA, PID, IMAGE, TAG, TYPE, SIMAGE, ORDERCODE, PRNM, PGSZ, ORNT, FNTSZ,
        FNTNM, DTLOCK, MACHINE_NO, TMPORDER, FTEXT, MENUTAG, PERDOTNETMENU, ATVDOTNETMENU, MODULE)
SELECT 'HS_MST0', 'Masters', '0', 'JAG1', h.IMAGE, h.TAG, h.TYPE, h.SIMAGE, '01000000F', h.PRNM, h.PGSZ, h.ORNT,
       h.FNTSZ, h.FNTNM, h.DTLOCK, h.MACHINE_NO, h.TMPORDER, h.FTEXT, 'HS', 'Y', 'Y', 'HS'
FROM   VBMENU_HOSALES h
WHERE  h.MODULE = 'HS' AND h.ORDERCODE = '02000000F'
AND    NOT EXISTS (SELECT 1 FROM VBMENU_HOSALES x WHERE x.MODULE = 'HS' AND x.ORDERCODE = '01000000F');

INSERT INTO VBMENU_HOSALES
       (ID, NAME, INDEX_ORA, PID, IMAGE, TAG, TYPE, SIMAGE, ORDERCODE, PRNM, PGSZ, ORNT, FNTSZ,
        FNTNM, DTLOCK, MACHINE_NO, TMPORDER, FTEXT, MENUTAG, PERDOTNETMENU, ATVDOTNETMENU, MODULE)
SELECT 'HS_MSTE', n.NM, n.IDX, 'HS_MST0!0', h.IMAGE, h.TAG, 'E', h.SIMAGE, n.OC, h.PRNM, h.PGSZ, h.ORNT,
       h.FNTSZ, h.FNTNM, h.DTLOCK, h.MACHINE_NO, h.TMPORDER, h.FTEXT, 'HS', 'Y', 'Y', 'HS'
FROM  (SELECT '0' IDX, '01050100T' OC, 'Vehicle Type Master' NM FROM dual UNION ALL
       SELECT '1', '01050200T', 'Vehicle Master'         FROM dual UNION ALL
       SELECT '2', '01100200T', 'Transporter Master'     FROM dual UNION ALL
       SELECT '3', '03000130T', 'Clerk Master'           FROM dual UNION ALL
       SELECT '4', '03000140T', 'Route Master'           FROM dual UNION ALL
       SELECT '5', '03000150T', 'Centre Master'          FROM dual UNION ALL
       SELECT '6', '03000155T', 'Weighment Point Master' FROM dual UNION ALL
       SELECT '7', '03000160T', 'Driver Master'          FROM dual UNION ALL
       SELECT '8', '03000170T', 'Helper/Loader Master'   FROM dual) n,
       (SELECT * FROM VBMENU_HOSALES WHERE MODULE = 'HS' AND ORDERCODE = '02040800T' AND ROWNUM = 1) h
WHERE  NOT EXISTS (SELECT 1 FROM VBMENU_HOSALES x WHERE x.MODULE = 'HS' AND x.ORDERCODE = n.OC);

-- ---------------------------------------------------------------------
-- 2. Screen mapping: menu code -> controller / action (the heading has none).
-- ---------------------------------------------------------------------
INSERT INTO VBMENU_DONE
       (ID, PROJECTNAME, MENUNAME, MENUCODE, CONTROLLER, ACTIONMETHOD, IS4SUBDOMAIN, MODULECODE, ISACTIVE)
SELECT (SELECT NVL(MAX(ID), 0) FROM VBMENU_DONE) + ROWNUM, 'HS', m.NAME, n.OC, n.CTRL, 'Index', 1, 'HS', 1
FROM  (SELECT '01050100T' OC, 'VehicleTypeMaster' CTRL FROM dual UNION ALL
       SELECT '01050200T', 'VehicleMaster'        FROM dual UNION ALL
       SELECT '01100200T', 'TransporterMaster'    FROM dual UNION ALL
       SELECT '03000130T', 'ClerkMaster'          FROM dual UNION ALL
       SELECT '03000140T', 'RouteMaster'          FROM dual UNION ALL
       SELECT '03000150T', 'CentreMaster'         FROM dual UNION ALL
       SELECT '03000155T', 'WeighmentPointMaster' FROM dual UNION ALL
       SELECT '03000160T', 'DriverMaster'         FROM dual UNION ALL
       SELECT '03000170T', 'HelperMaster'         FROM dual) n
JOIN   VBMENU_HOSALES m ON m.ORDERCODE = n.OC AND m.MODULE = 'HS'
WHERE  NOT EXISTS (SELECT 1 FROM VBMENU_DONE x WHERE x.MODULECODE = 'HS' AND x.MENUCODE = n.OC);

-- ---------------------------------------------------------------------
-- 3. Rights: CORETEAM gets AEDV on the heading and the 9 entries (FACT_JAYA2027.USRACS_HOSALES), back-date
--    days 0 like its other HS rows (masters have no date, so only the A / E / D / V letters matter).
--    To grant another user, repeat with that user id.
-- ---------------------------------------------------------------------
INSERT INTO FACT_JAYA2027.USRACS_HOSALES
       (ID, LOCA, UID_ORA, MNUID, INDEX_ORA, AEDV, EDAY, ADAY, DDAY, USER_NAME, USER_ENTDT,
        OS_USER, TERMINAL_ID, SCHEMA_NM)
SELECT (SELECT NVL(MAX(ID), 0) FROM FACT_JAYA2027.USRACS_HOSALES) + ROWNUM,
       'JAY', 'CORETEAM', m.ID, m.INDEX_ORA, 'AEDV', 0, 0, 0, USER, SYSDATE,
       USER, 'setup_hs_masters.sql', 'FACT_JAYA2027'
FROM   VBMENU_HOSALES m
WHERE  m.MODULE = 'HS' AND m.ID IN ('HS_MST0', 'HS_MSTE')
AND    NOT EXISTS (SELECT 1 FROM FACT_JAYA2027.USRACS_HOSALES x
                   WHERE x.UID_ORA = 'CORETEAM' AND x.MNUID = m.ID AND x.INDEX_ORA = m.INDEX_ORA);

COMMIT;

-- ---------------------------------------------------------------------
-- 7. Verify: the Masters menu with its mapping, and CORETEAM's rights on it
-- ---------------------------------------------------------------------
SELECT m.ID, m.INDEX_ORA, m.PID, m.ORDERCODE, m.NAME, m.TYPE, d.CONTROLLER, d.ACTIONMETHOD, d.ISACTIVE
FROM   VBMENU_HOSALES m
LEFT JOIN VBMENU_DONE d ON d.MENUCODE = m.ORDERCODE AND d.MODULECODE = 'HS'
WHERE  m.MODULE = 'HS' AND m.ID IN ('HS_MST0', 'HS_MSTE')
ORDER  BY m.ORDERCODE;

SELECT m.ORDERCODE, m.NAME, p.AEDV, p.ADAY, p.EDAY, p.DDAY
FROM   FACT_JAYA2027.USRACS_HOSALES p
JOIN   VBMENU_HOSALES m ON m.ID = p.MNUID AND m.INDEX_ORA = p.INDEX_ORA
WHERE  p.UID_ORA = 'CORETEAM' AND m.MODULE = 'HS' AND m.ID IN ('HS_MST0', 'HS_MSTE')
ORDER  BY m.ORDERCODE;

-- =====================================================================
-- 9. UNDO -- not part of the setup; run only to take the Masters menu out again.
-- =====================================================================
-- DELETE FROM FACT_JAYA2027.USRACS_HOSALES WHERE MNUID IN ('HS_MST0', 'HS_MSTE');
-- DELETE FROM VBMENU_DONE    WHERE MODULECODE = 'HS' AND MENUCODE IN ('01050100T', '01050200T', '01100200T', '03000130T',
--                                  '03000140T', '03000150T', '03000155T', '03000160T', '03000170T');
-- DELETE FROM VBMENU_HOSALES WHERE MODULE = 'HS' AND ID IN ('HS_MST0', 'HS_MSTE');
-- COMMIT;
