# PMS - نظام إدارة التصاريح الموحد (Unified Permit Management System)

## نظرة عامة (Overview)

يهدف هذا التطبيق إلى توحيد نظام **PMS** (Entry/Exit Management System) في منصة واحدة
مبنية على **Blazor Server (.NET 8)**، تغطي مرحلتين من العمل:

This application unifies the PMS platform into a single **Blazor Server (.NET 8)**
front-end covering two phases of work, while preserving the existing layered
solution (`Core` → `DataAccess` → `Services` → `PMS.web`).

| Phase | Arabic | Description |
|---|---|---|
| **Phase 1** | المرحلة الأولى - الدخول البيومتري | Biometric employee access control: employees, biometric templates, readers/devices, gate permissions, movement log. |
| **Phase 2** | المرحلة الثانية - تصاريح المعدات والزوار والسيارات | Equipment/materials, visitor and vehicle permits, multi-step approval workflow, gate officer verification. |

النظام القديم كان مبني بـ ASP.NET Core MVC (Razor Views + Controllers) وتم ترحيله
تدريجياً إلى **Blazor Server** مع الإبقاء على البنية الطبقية الحالية
(`Core`، `DataAccess`، `Services`، `PMS.web`) دون حذف أي كيانات أو خدمات تعمل حالياً
في المرحلة الثانية (`Permit`, `Gate`, `EquipMatiMovment`, `CarMovment`, ...).

## البنية الطبقية (Layered Architecture)

```
PMS.sln
 ├─ Core          -> Entities, Enums, Interfaces, ViewModels (Core.Models), Common helpers
 ├─ DataAccess    -> EF Core DbContext, Repositories, UnitOfWork, Migrations, Configurations
 ├─ Services      -> Business logic implementations (Organization / AccessControl / Workflow / legacy Permit services)
 └─ PMS.web      -> Blazor Server UI (primary) + legacy MVC controllers/views (kept, reachable under /legacy)
```

لم يتم حذف أي مشروع من المشاريع الأربعة الحالية، وتم فقط **تحويل** `PMS.web`
من MVC إلى Blazor Server وإعادة تنظيم الكيانات والخدمات الجديدة داخل نفس المشاريع.

## خريطة مجلدات Core الجديدة (New Core folders)

```
Core/
 ├─ Enums/BaseEnums.cs                     -> extended with PermitClassification, UnifiedPermitStatus,
 │                                            PermitPhase, MovementDirection, DeviceCommandStatus,
 │                                            WorkflowStepType, BiometricTemplateType, MovementSource,
 │                                            ApprovalDecision (legacy enums kept untouched)
 ├─ Entities/
 │   ├─ AccessControl/                     -> Department, Employee, BiometricTemplate, Reader,
 │   │                                        EmployeePermission, DeviceCommand, MovementLog (Phase 1)
 │   ├─ Workflow/                          -> WorkflowDefinition, WorkflowStep, PermitApproval, Notification
 │   ├─ Permits/                           -> EquipmentPermitDetail, VisitorPermitDetail, VehiclePermitDetail
 │   │                                        (clean 1:1 detail records, coexist with legacy EquipMatiMovment/
 │   │                                        CarMovment/HumanMovment which remain the source of truth for the
 │   │                                        current materials flow)
 │   └─ Permit.cs                          -> extended with nullable ValidFrom/ValidTo/UnifiedStatus/
 │                                            WorkflowDefinitionId/RequesterEmployeeId/HostEmployeeId
 ├─ Models/                                -> PermitViewModel, LoginViewModel, CreateUserViewModel,
 │                                            ViewUserViewModel, ChangePasswordViewModel (moved out of
 │                                            PMS.web/ViewModels for clean layering; the legacy
 │                                            PMS.web.ViewModels classes are still kept for the old MVC views)
 ├─ Interfaces/Services/
 │   ├─ Organization/                      -> IEmployeeService, IDepartmentMasterService
 │   ├─ AccessControl/                     -> IBiometricTemplateService, IReaderService,
 │   │                                        IEmployeePermissionService, IDeviceCommandService,
 │   │                                        IMovementLogService
 │   └─ Workflow/                          -> IWorkflowEngine, INotificationService
 └─ Common/StatusMapper.cs                 -> maps legacy char status (I/A/J/R/C/D) <-> UnifiedPermitStatus
```

## DataAccess

- `DataContext` now exposes `DbSet<T>` for every new Phase 1/Phase 2 entity in addition to the legacy sets.
- `OnModelCreating` configures the new relationships (Employee→Department, Reader→Gate,
  WorkflowStep→WorkflowDefinition, PermitApproval→Permit, Equipment/Visitor/VehiclePermitDetail 1:1 with Permit,
  a `CHECK` constraint on `MovementLog` requiring `EmployeeId` or `PermitId`).
- `DataAccess/Configurations/EmployeeConfiguration.cs` demonstrates the `IEntityTypeConfiguration<T>` pattern
  recommended for new entities going forward.
- `IUnitOfWork` / `UnitOfWork` gained repositories for `Employees`, `Departments`, `Readers`, `MovementLogs`,
  `DeviceCommands`, `EmployeePermissions`, `BiometricTemplates`, `WorkflowDefinitions`, `WorkflowSteps`,
  `PermitApprovals`, `Notifications` (legacy repositories unchanged).

> **Migration note**: all new Permit properties are nullable and all new entities are additive, so a new
> EF Core migration (`dotnet ef migrations add UnifiedPermitManagement`) can be generated without breaking
> existing data or migrations.

## Services

```
Services/
 ├─ Organization/     -> EmployeeService, DepartmentMasterService
 ├─ AccessControl/     -> ReaderService, MovementLogService, BiometricTemplateService,
 │                        EmployeePermissionService, DeviceCommandService
 ├─ Workflow/          -> WorkflowEngine (first-cut approval routing), NotificationService
 ├─ Permits/           -> reserved for future permit-detail services (see _NOTE.md)
 ├─ PermitServices.cs  -> UNCHANGED location (still implements IPermit), only its `using` statements were
 │                        fixed (PMS.web.ViewModels -> Core.Models) to remove an invalid cross-project reference
 ├─ GateServices.cs, DepartmentService.cs, PermitTypeServices.cs, ProcedureMovmentService.cs -> unchanged
```

## PMS.web (Blazor Server)

```
PMS.web/
 ├─ Components/
 │   ├─ App.razor, Routes.razor, _Imports.razor
 │   ├─ Layout/          -> MainLayout (RTL, orange navbar), NavMenu, EmptyLayout (login)
 │   ├─ Shared/          -> RedirectToLogin
 │   ├─ Pages/Identity/  -> Login.razor ("/login" and "/"), Logout.razor
 │   ├─ Pages/Home/      -> Dashboard.razor ("/dashboard")
 │   ├─ Pages/Phase1/    -> Employees, Devices (Readers/Monitor), Permissions, Movements
 │   ├─ Pages/Phase2/    -> Permits (Index/Material Create-Edit-View, Visitor/Vehicle placeholders),
 │   │                      GateOfficer/VerifyPermit, Workflow/WorkflowDefinitions
 │   ├─ Pages/Admin/     -> Gates (Index/Edit), Users (Index/Create)
 │   └─ Pages/Reports/   -> ReportsHome
 ├─ Identity/IdentityRevalidatingAuthenticationStateProvider.cs
 ├─ wwwroot/css/app.css  -> RTL Arabic-first styling, orange brand accent
 ├─ Controllers/, Views/ -> legacy MVC, KEPT and still reachable under `/legacy/{controller}/{action}` plus the
 │                          `PrintPermitPdf` endpoint used by the new Blazor permit view page for PDF export
 └─ Program.cs           -> rewritten for Blazor Server (Interactive Server render mode) while keeping
                             Identity, DataContext and all existing DI registrations
```

### تسجيل الدخول (Login)

صفحة `/login` تعيد استخدام منطق `LoginController` القديم:
- التحقق من أن `JobStatus == "AE"` قبل السماح بالدخول.
- طلب اختيار بوابة للمستخدمين من نوع `SaftyUser`.
- تسجيل الدخول عبر `SignInManager<User>` مباشرة (الصفحة تعمل بعرض ثابت من جهة الخادم Static SSR
  حتى تتمكن من كتابة كوكي المصادقة مباشرة في استجابة HTTP، بينما بقية الصفحات تفعّل التفاعل عبر
  `@rendermode InteractiveServer`).

### التوجيه (Routing)

- الواجهة الافتراضية الآن هي Blazor (`MapRazorComponents<App>().AddInteractiveServerRenderMode()`).
- الكونترولرات القديمة ما زالت متاحة تحت المسار `/legacy/{controller}/{action}/{id?}` لتفادي كسر أي رابط قديم،
  بالإضافة إلى نقطة نهاية طباعة PDF (`/legacy/Permit/PrintPermitPdf`) المستخدمة من صفحة عرض التصريح الجديدة.

## التشغيل (Running locally)

```bash
cd PMS.web
dotnet restore
dotnet ef database update --project ../DataAccess --startup-project .
dotnet run
```

اضبط سلسلة الاتصال بقاعدة البيانات في `appsettings.Development.json` (لا تضع كلمات مرور حقيقية في README
أو في أي ملف يتم رفعه لنظام التحكم بالإصدار).

## الحالة الحالية والخطوات القادمة (Status & next steps)

- ✅ المرحلة الثانية (تصاريح المواد): عرض/إضافة/تعديل/اعتماد/طباعة تعمل بالكامل عبر Blazor.
- ✅ المرحلة الأولى: صفحات الموظفين وأجهزة القراءة وسجل الحركة تعمل بعرض/إضافة أساسي (CRUD مبسّط).
- 🚧 تصاريح الزوار والسيارات المستقلة: واجهات مبدئية (placeholder) بانتظار تصميم مسار الاعتماد الكامل.
- 🚧 محرك مسارات الاعتماد (`IWorkflowEngine`) ونظام الإشعارات (`INotificationService`): تطبيق أولي، يحتاج
  استكمال منطق الترقية بين الخطوات وربطه بواجهة `admin/workflows`.
