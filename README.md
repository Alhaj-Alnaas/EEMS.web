# نظام إدارة التصاريح الموحد — Unified Permit Management System (PMS)

نظام ويب داخلي (Intranet / On-Premises) لشركة الليبية للحديد والصلب، يغطي **المرحلتين** ضمن حل واحد بواجهة **Blazor Server**.

| المرحلة | النطاق |
|---------|--------|
| **الأولى** | دخول/خروج الموظفين بيومتريًا (موظفين، أجهزة، صلاحيات، سجل حركة) |
| **الثانية** | تصاريح المواد / الزوار / المركبات + اعتمادات + واجهة ضابط البوابة |

---

الشرح الكامل لطبقات الحل ومسار الطلب والأجهزة والنشر موجود في [docs/هيكلية-المشروع.md](docs/هيكلية-المشروع.md).

**تدفق الاعتماديات:** `PMS.web → Services → DataAccess → Core`

---

## التشغيل

```bash
dotnet build PMS.sln
dotnet run --project PMS.web
```

- الواجهة: Blazor على `/login` و`/dashboard` ووحدات المرحلتين.
- طباعة PDF لتصريح المواد: `GET /permits/{id}/pdf`.
- قارئات ZKTeco: `http://<host>:5260/iclock`.

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
| الواجهة | Blazor Server هي المسار الوحيد. صفحات MVC المكررة أُزيلت |
| المرحلة 1 | موظفون، قارئات ZKTeco على `/iclock`، صلاحيات عبور، سجل حركة |
| المرحلة 2 | مواد / زوار / مركبات، اعتماد، تنفيذ على البوابة، تقارير |
| تصريح موظف وتصاريح عمالة | صفحات نائبة، المنطق غير مكتمل |
| النشر | `publish-server.cmd` ثم خدمة Windows `PMS Web Host` |

المرجع التحليلي: وثائق SRS / Database Design / Scrum timeline لنظام التصاريح الموحّد v2.
