# Reporting catalog fixture contract

This fixture is a deterministic, disposable reporting catalog used to prove provider
metadata discovery. `contract.json` is provider-neutral: an implementation for any
database engine must create the fixed catalog name, schemas, row counts, logical
views, relationships, and portable features listed there. Provider-specific DDL and
extensions belong beneath a provider directory, never in the portable contract.

The catalog deliberately contains ordinary reporting objects and awkward metadata:
identity and composite keys, multiple foreign-key paths, a self hierarchy, filtered
unique indexes, computed/rowversion/temporal columns, keyless views, descriptions,
and unsupported programmable objects. It is test data only, has no external
dependencies, and uses fixed values rather than clocks, random values, or generated
business identifiers.

SQL Server implementation assets are in `sqlserver/`. Its extension list records
SQL Server-only rowversion, temporal, `hierarchyid`, `sql_variant`, and unsupported
object coverage. Bootstrap and teardown operate only on
`TheSqlODataMcp_TestCatalog`; callers must connect to `master`.
