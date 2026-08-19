# Services/Permits

`PermitServices.cs` (implementing `IPermit`) intentionally remains at
`Services/PermitServices.cs` to avoid breaking existing dependency
injection registrations and namespaces (`Services.PermitServices`).

New Phase 2 permit-related services (workflow-aware permit orchestration,
detail-record services for equipment/visitor/vehicle permits, etc.) should
be added under this `Services/Permits/` folder going forward, following the
same pattern as `Services/AccessControl` and `Services/Organization`.
