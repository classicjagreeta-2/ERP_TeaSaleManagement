-- CLASSICTRIAL: HEAD_OFFICE login (schema FACT_JSTIL2027) gets ORA-00942 when opening a
-- Private / Auction Sale invoice. The API reads the finance tables FIN_JSTIL2027.BH11 / BH21 /
-- BHSUB / BHVT / MAST_PARAM / MAST_TDS_HEAD / SLMAS / TRN_TDS_DETAIL as user FACT_JSTIL2027,
-- which on CLASSICTRIAL holds only CONNECT + RESOURCE (only BHINV is granted directly).
-- On CLASSICDEV the same user has the roles below, which carry those grants.
-- The roles already exist on CLASSICTRIAL (FACT_JSTIL2027ALL 2692 privs, FIN_JSTIL2027ALL 1664).
-- Run as a DBA. Users must log in again (new API connections) for the roles to apply.

GRANT FACT_JSTIL2027ALL TO FACT_JSTIL2027;
GRANT FIN_JSTIL2027ALL  TO FACT_JSTIL2027;
GRANT CLASSICNEWALL     TO FACT_JSTIL2027;
ALTER USER FACT_JSTIL2027 DEFAULT ROLE ALL;

-- Check:
-- SELECT granted_role, default_role FROM dba_role_privs WHERE grantee = 'FACT_JSTIL2027';
