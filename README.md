# نظام إدارة التصاريح الموحد — Unified Permit Management System (PMS)

نظام ويب داخلي (Intranet / On-Premises) لشركة الليبية للحديد والصلب، يغطي **المرحلتين** ضمن حل واحد بواجهة **Blazor Server**.

| المرحلة | النطاق |
|---------|--------|
| **الأولى** | دخول/خروج الموظفين بيومتريًا (موظفين، أجهزة، صلاحيات، سجل حركة) |
| **الثانية** | تصاريح المواد / الزوار / المركبات + اعتمادات + واجهة ضابط البوابة |

---

## هيكلية الحل (محفوظة وموسَّعة)

```
PMS.web/
├── Core/                    # المجال والواجهات والنماذج
│   ├── Entities/
│   │   ├── AccessControl/   # مرحلة 1: Employee, Reader, MovementLog, …
│   │   ├── Workflow/        # WorkflowDefinition, PermitApproval, Notification
│   │   ├── Permits/         # Equipment/Visitor/VehiclePermitDetail (TPT)
│   │   └── …                # Permit, Gate, حركات المواد القديمة، User
│   ├── Enums/               # حالات موحّدة + أنواع قديمة
│   ├── Models/              # ViewModels مشتركة (بدل طبقة الويب)
│   ├── Common/              # StatusMapper (حالة قديمة ↔ موحّدة)
│   └── Interfaces/Services/
│       ├── Organization/
│       ├── AccessControl/
│       └── Workflow/
├── DataAccess/              # EF Core + UnitOfWork + Migrations
├── Services/
│   ├── Organization/
│   ├── AccessControl/
│   ├── Workflow/
│   └── PermitServices.cs    # منطق تصاريح المواد القائم
└── PMS.web/                # Blazor Server (واجهة أساسية)
    └── Components/
        ├── Layout/
        ├── Pages/
        │   ├── Identity/    # دخول / خروج
        │   ├── Home/        # لوحة التحكم
        │   ├── Phase1/      # موظفين، أجهزة، صلاحيات، حركات
        │   ├── Phase2/      # تصاريح، ضابط بوابة، Workflow
        │   ├── Admin/       # بوابات، مستخدمون
        │   └── Reports/
        └── Shared/
```

**تدفق الاعتماديات:** `PMS.web → Services → DataAccess → Core`

---

## التشغيل

```bash
dotnet build PMS.sln
dotnet run --project PMS.web
```

- الواجهة الأساسية: Blazor على `/login` و`/dashboard` ووحدات المرحلتين.
- واجهة MVC القديمة (إن لزم): تحت المسار `/legacy/...`
- طباعة PDF للتصاريح تبقى عبر `PermitController`.

### قاعدة البيانات

النموذج (Domain Model) تم توحيده بالكامل (مفتاح `Guid` وحيد لكل الكيانات، `Permit.GateId` كعلاقة حقيقية،
تجاهل الكيانات اليتيمة `ApproveMovment/Role/Permission/UserRole/RolePermission`، و`DepartmentDto` بدون جدول).
كل الـ migrations القديمة حُذفت واستُبدلت بـ migration نظيفة واحدة:

`DataAccess/Migrations/<timestamp>_InitialUnified.cs`

تطبيق الـ migration على قاعدة بيانات جديدة/فارغة:

```bash
dotnet ef database update --project DataAccess --startup-project PMS.web --context DataContext
```

#### إعادة تهيئة قاعدة البيانات من الصفر (Reset)

> ⚠️ هذا يحذف كل البيانات الحالية في `PMSDB`. لا تُنفَّذ على بيئة إنتاج تحتوي بيانات حقيقية.

```bash
dotnet ef database drop --force --project DataAccess --startup-project PMS.web --context DataContext
dotnet ef database update --project DataAccess --startup-project PMS.web --context DataContext
```

سلسلة الاتصال (`ConnectionStrings:DefaultConnection`) موجودة في `PMS.web/appsettings.json` —
لا تضع كلمات مرور حقيقية في هذا الملف عند رفعه لنظام التحكم بالإصدار.

إذا فشل `database drop` لأسباب صلاحيات، استخدم `sqlcmd`/`Invoke-Sqlcmd` مباشرة على السيرفر لتنفيذ
`DROP DATABASE PMSDB;` ثم `CREATE DATABASE PMSDB;`، ثم أعد تنفيذ `dotnet ef database update`.

لإنشاء migration جديدة لاحقًا بعد تعديل الكيانات:

```bash
dotnet ef migrations add <MigrationName> --project DataAccess --startup-project PMS.web --context DataContext
```

---

## الحالة الحالية

| المكوّن | الحالة |
|---------|--------|
| طبقات الحل + تقسيمات المجالات | جاهز |
| Blazor Server + RTL عربي | جاهز |
| تصاريح المواد (قائمة/إنشاء/تعديل/عرض/اعتماد) | مرحَّل إلى Blazor |
| بوابات ومستخدمون | Blazor |
| كيانات وخدمات المرحلة 1 | هيكل + stubs/أساسيات |
| جدول `HumansMovment` | أُزيل — الزوار عبر `VisitorPermitDetails` |
| `PermitType` مفتاح مضاعف (int/Guid) | تم توحيده إلى `Guid` فقط |
| `Permit.gateId` (int بدون علاقة) | أصبح `Permit.GateId` (Guid?) بعلاقة `NoAction` حقيقية مع `Gate` |
| Migrations قديمة متعددة | حُذفت جميعها، migration واحدة نظيفة `InitialUnified` |
| Workflow محرّك التهيئة | هيكل جاهز للتوسعة |
| تكامل ZKTeco / Attendance API | لم يُنفَّذ بعد (حسب خطة Scrum) |

المرجع التحليلي: وثائق SRS / Database Design / Scrum timeline لنظام التصاريح الموحّد v2.
