using System.Data;
using System.Globalization;
using Core.Common;
using Core.Entities;
using Core.Interfaces.Services;
using Core.Interfaces.Services.Labor;
using Core.Models;
using Core.Options;
using DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Services.Labor
{
    public class LaborRosterService : ILaborRosterService
    {
        private const int MaxRows = 5000;
        private readonly IDbContextFactory<DataContext> _factory;
        private readonly IAuditLogService _audit;
        private readonly LaborCompanySourceOptions _source;

        public LaborRosterService(
            IDbContextFactory<DataContext> factory,
            IAuditLogService audit,
            IOptions<LaborCompanySourceOptions> source)
        {
            _factory = factory;
            _audit = audit;
            _source = source.Value;
        }

        public async Task<List<LaborCompanyRow>> ListCompaniesAsync(CancellationToken ct = default)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var companies = await db.LaborCompanies.AsNoTracking()
                .Where(c => !c.isDeleted)
                .OrderBy(c => c.Name)
                .ToListAsync(ct);
            var counts = await db.LaborWorkers.AsNoTracking()
                .Where(w => !w.isDeleted)
                .GroupBy(w => w.CompanyId)
                .Select(g => new { Id = g.Key, Count = g.Count() })
                .ToListAsync(ct);
            var byCompany = counts.ToDictionary(x => x.Id, x => x.Count);

            return companies.Select(c => MapCompany(c, byCompany.GetValueOrDefault(c.Id))).ToList();
        }

        public async Task<LaborCompanyRow?> GetCompanyAsync(Guid id, CancellationToken ct = default)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var company = await db.LaborCompanies.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id && !c.isDeleted, ct);
            if (company == null)
                return null;

            var count = await db.LaborWorkers.CountAsync(w => w.CompanyId == id && !w.isDeleted, ct);
            return MapCompany(company, count);
        }

        public async Task<Guid> SaveCompanyAsync(LaborCompanyInput input, LaborActor actor, CancellationToken ct = default)
        {
            var number = Required(input.Number, "رقم الشركة", 32);
            var name = Required(input.Name, "اسم الشركة", 200);
            var address = Clip(input.Address, 300, "العنوان");
            var specialty = Clip(input.Specialty, 200, "التخصص");
            if (!LaborCompanyState.TryParse(input.Status, out var status))
                throw new InvalidOperationException("حالة الشركة يجب أن تكون مستمرة أو متوقفة.");
            if (input.ContractStart != null && input.ContractEnd != null
                && input.ContractEnd.Value.Date < input.ContractStart.Value.Date)
                throw new InvalidOperationException("تاريخ نهاية العقد يسبق تاريخ البداية.");

            await using var db = await _factory.CreateDbContextAsync(ct);
            var duplicate = await db.LaborCompanies.AnyAsync(c =>
                !c.isDeleted && c.Id != input.Id && c.Number == number, ct);
            if (duplicate)
                throw new InvalidOperationException("رقم الشركة مسجّل مسبقاً.");

            LaborCompany company;
            var creating = input.Id == Guid.Empty;
            if (creating)
            {
                company = new LaborCompany
                {
                    Number = number,
                    Name = name,
                    PermitCode = await NextPermitCodeAsync(db, ct)
                };
                Touch(company, actor, create: true);
                db.LaborCompanies.Add(company);
            }
            else
            {
                company = await db.LaborCompanies.FirstOrDefaultAsync(c => c.Id == input.Id && !c.isDeleted, ct)
                    ?? throw new InvalidOperationException("الشركة غير موجودة.");
                var workers = await db.LaborWorkers.AnyAsync(w => w.CompanyId == company.Id && !w.isDeleted, ct);
                if (workers && !string.Equals(company.Number, number, StringComparison.Ordinal))
                    throw new InvalidOperationException("لا يمكن تغيير رقم شركة لها عمالة مسجّلة.");
                company.Number = number;
                company.Name = name;
                Touch(company, actor, create: false);
            }

            company.Address = address;
            company.Specialty = specialty;
            company.Status = status;
            company.ContractStart = input.ContractStart?.Date;
            company.ContractEnd = input.ContractEnd?.Date;
            await db.SaveChangesAsync(ct);
            await AuditAsync(
                AuditArea.Administration,
                creating ? AuditAction.Add : AuditAction.Edit,
                (creating ? "إضافة شركة عمالة " : "تعديل شركة عمالة ") + company.Name + " (" + company.Number + ")",
                actor);
            return company.Id;
        }

        public async Task DeleteCompanyAsync(Guid id, LaborActor actor, CancellationToken ct = default)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var company = await db.LaborCompanies.FirstOrDefaultAsync(c => c.Id == id && !c.isDeleted, ct)
                ?? throw new InvalidOperationException("الشركة غير موجودة.");
            if (await db.LaborWorkers.AnyAsync(w => w.CompanyId == id && !w.isDeleted, ct))
                throw new InvalidOperationException("لا يمكن حذف شركة مرتبطة بها عمالة. يمكن إيقافها بدلاً من حذفها.");

            company.isDeleted = true;
            company.deletedBy = actor.UserName ?? "";
            company.deletedOn = DateTime.Now;
            await db.SaveChangesAsync(ct);
            await AuditAsync(AuditArea.Administration, AuditAction.Delete, "حذف شركة عمالة " + company.Name, actor);
        }

        public async Task<ExternalCompanyLookup> ListExternalAsync(CancellationToken ct = default)
        {
            var sql = (_source.ExternalCommand ?? "").Trim();
            if (sql.Length == 0)
            {
                return new ExternalCompanyLookup
                {
                    Available = false,
                    Message = "قائمة الشركات الخارجية غير مربوطة. استخدم الإدخال اليدوي إلى أن يُضبط مصدر الشركات."
                };
            }

            if (!sql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) || sql.Contains(';'))
            {
                return new ExternalCompanyLookup
                {
                    Available = false,
                    Message = "أمر قراءة الشركات الخارجية يجب أن يكون استعلام SELECT واحداً."
                };
            }

            try
            {
                await using var db = await _factory.CreateDbContextAsync(ct);
                var connection = db.Database.GetDbConnection();
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync(ct);

                await using var command = connection.CreateCommand();
                command.CommandTimeout = 40;
                command.CommandText = sql;
                var items = new List<ExternalLaborCompany>();
                await using var reader = await command.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    var number = LaborText.Compact(ReadText(reader, 0));
                    var name = LaborText.Compact(ReadText(reader, 1));
                    if (number.Length == 0 || name.Length == 0)
                        continue;
                    items.Add(new ExternalLaborCompany
                    {
                        Number = number,
                        Name = name,
                        Address = reader.FieldCount > 2 ? EmptyToNull(LaborText.Compact(ReadText(reader, 2))) : null,
                        Specialty = reader.FieldCount > 3 ? EmptyToNull(LaborText.Compact(ReadText(reader, 3))) : null
                    });
                }

                return new ExternalCompanyLookup { Available = true, Items = items };
            }
            catch (Exception ex)
            {
                return new ExternalCompanyLookup
                {
                    Available = false,
                    Message = "تعذر قراءة قاعدة الشركات: " + ex.Message
                };
            }
        }

        public async Task<LaborExternalImportResult> ImportExternalAsync(
            IReadOnlyList<ExternalLaborCompany> rows,
            LaborActor actor,
            CancellationToken ct = default)
        {
            if (rows == null || rows.Count == 0)
                throw new InvalidOperationException("اختر شركة واحدة على الأقل.");

            await using var db = await _factory.CreateDbContextAsync(ct);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var existing = await db.LaborCompanies
                .Where(c => !c.isDeleted)
                .Select(c => c.Number)
                .ToListAsync(ct);
            var known = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var added = 0;
            var skipped = 0;
            foreach (var row in rows)
            {
                var number = LaborText.Compact(row.Number);
                var name = LaborText.Compact(row.Name);
                if (number.Length == 0 || name.Length == 0 || number.Length > 32 || name.Length > 200)
                {
                    skipped++;
                    continue;
                }

                if (!known.Add(number))
                {
                    skipped++;
                    continue;
                }

                var company = new LaborCompany
                {
                    Number = number,
                    Name = name,
                    Address = Truncate(row.Address, 300),
                    Specialty = Truncate(row.Specialty, 200),
                    Status = LaborCompanyState.Active,
                    PermitCode = await NextPermitCodeAsync(db, ct)
                };
                Touch(company, actor, create: true);
                db.LaborCompanies.Add(company);
                await db.SaveChangesAsync(ct);
                added++;
            }

            await tx.CommitAsync(ct);
            if (added > 0)
            {
                await AuditAsync(
                    AuditArea.Administration,
                    AuditAction.Add,
                    "استيراد " + added + " شركة عمالة من القاعدة الخارجية",
                    actor);
            }

            return new LaborExternalImportResult { Added = added, Skipped = skipped };
        }

        public async Task<List<LaborWorkerRow>> ListWorkersAsync(Guid companyId, CancellationToken ct = default)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var company = await db.LaborCompanies.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == companyId && !c.isDeleted, ct);
            if (company == null)
                return new List<LaborWorkerRow>();

            var workers = await db.LaborWorkers.AsNoTracking()
                .Where(w => w.CompanyId == companyId && !w.isDeleted)
                .OrderBy(w => w.PermitNo)
                .ToListAsync(ct);
            return workers.Select(w => MapWorker(w, company)).ToList();
        }

        public async Task<Guid> SaveWorkerAsync(LaborWorkerInput input, LaborActor actor, CancellationToken ct = default)
        {
            var name = Required(input.FullName, "اسم العامل", 200);
            if (!LaborWorkerState.TryParse(input.Status, out var status))
                throw new InvalidOperationException("حالة العامل غير معروفة.");
            var nationality = Clip(input.Nationality, 80, "الجنسية");
            var passport = Clip(input.PassportNo, 40, "رقم الجواز");
            var address = Clip(input.Address, 300, "العنوان");
            var notes = Clip(input.Notes, 500, "الملاحظات");

            await using var db = await _factory.CreateDbContextAsync(ct);
            var company = await db.LaborCompanies.FirstOrDefaultAsync(c => c.Id == input.CompanyId && !c.isDeleted, ct)
                ?? throw new InvalidOperationException("اختر الشركة.");

            var others = await db.LaborWorkers
                .Where(w => w.CompanyId == company.Id && !w.isDeleted && w.Id != input.Id)
                .Select(w => new { w.FullName, w.PassportNo })
                .ToListAsync(ct);
            EnsurePersonIsDistinct(name, passport, others.Select(o => (o.FullName, o.PassportNo)));

            var creating = input.Id == Guid.Empty;
            LaborWorker worker;
            if (creating)
            {
                if (company.Status != LaborCompanyState.Active)
                    throw new InvalidOperationException("الشركة متوقفة، ولا يُضاف لها عامل.");

                worker = new LaborWorker
                {
                    CompanyId = company.Id,
                    PermitNo = await NextPermitNoAsync(db, company, ct),
                    FullName = name
                };
                Touch(worker, actor, create: true);
                db.LaborWorkers.Add(worker);
            }
            else
            {
                worker = await db.LaborWorkers.FirstOrDefaultAsync(w =>
                    w.Id == input.Id && w.CompanyId == company.Id && !w.isDeleted, ct)
                    ?? throw new InvalidOperationException("العامل غير موجود.");
                worker.FullName = name;
                Touch(worker, actor, create: false);
            }

            worker.Nationality = nationality;
            worker.PassportNo = passport;
            worker.Status = status;
            worker.Address = address;
            worker.Notes = notes;
            await db.SaveChangesAsync(ct);
            await AuditAsync(
                AuditArea.Permits,
                creating ? AuditAction.Add : AuditAction.Edit,
                (creating ? "إضافة عامل " : "تعديل عامل ") + worker.FullName + " تصريح " + worker.PermitNo,
                actor);
            return worker.Id;
        }

        public async Task<Guid> StageAsync(
            Guid companyId,
            string mode,
            string? fileName,
            IReadOnlyList<LaborSheetRow> rows,
            LaborActor actor,
            CancellationToken ct = default)
        {
            if (mode is not (LaborBatchMode.Add or LaborBatchMode.Match))
                throw new InvalidOperationException("نوع المعالجة غير معروف.");
            if (rows == null || rows.Count == 0)
                throw new InvalidOperationException("الملف لا يحتوي على عمالة.");
            if (rows.Count > MaxRows)
                throw new InvalidOperationException("الملف أكبر من " + MaxRows + " صفاً.");

            var prepared = PrepareRows(rows);
            await using var db = await _factory.CreateDbContextAsync(ct);
            var company = await db.LaborCompanies.FirstOrDefaultAsync(c => c.Id == companyId && !c.isDeleted, ct)
                ?? throw new InvalidOperationException("اختر الشركة.");
            if (mode == LaborBatchMode.Add && company.Status != LaborCompanyState.Active)
                throw new InvalidOperationException("الشركة متوقفة، ولا تُقبل إضافة عمالة لها.");

            var workers = await db.LaborWorkers
                .Where(w => w.CompanyId == company.Id && !w.isDeleted)
                .ToListAsync(ct);
            EnsureWorkersResolvable(workers);

            var lines = BuildLines(mode, workers, prepared, company.PermitCode);
            var batch = new LaborImportBatch
            {
                CompanyId = company.Id,
                Mode = mode,
                State = LaborBatchState.Draft,
                FileName = Clip(fileName, 260, "اسم الملف")
            };
            Touch(batch, actor, create: true);
            foreach (var line in lines)
            {
                line.BatchId = batch.Id;
                Touch(line, actor, create: true);
                batch.Lines.Add(line);
            }

            db.LaborImportBatches.Add(batch);
            await db.SaveChangesAsync(ct);
            return batch.Id;
        }

        public async Task<LaborBatchDetail?> GetBatchAsync(Guid id, CancellationToken ct = default)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var batch = await db.LaborImportBatches.AsNoTracking()
                .Include(b => b.Company)
                .Include(b => b.Lines)
                .FirstOrDefaultAsync(b => b.Id == id && !b.isDeleted, ct);
            if (batch == null)
                return null;

            return new LaborBatchDetail
            {
                Id = batch.Id,
                CompanyId = batch.CompanyId,
                CompanyName = batch.Company?.Name ?? "",
                Mode = batch.Mode,
                State = batch.State,
                FileName = batch.FileName,
                CreatedOn = batch.createdOn,
                AppliedOn = batch.AppliedOn,
                Lines = batch.Lines
                    .Where(l => !l.isDeleted)
                    .OrderBy(l => l.SortOrder)
                    .Select(l => new LaborBatchLineRow
                    {
                        Id = l.Id,
                        Action = l.Action,
                        FullName = l.FullName,
                        PassportNo = l.PassportNo,
                        CurrentPermitNo = l.CurrentPermitNo,
                        ProposedPermitNo = l.ProposedPermitNo,
                        CurrentStatus = l.CurrentStatus,
                        ProposedStatus = l.ProposedStatus
                    })
                    .ToList()
            };
        }

        public async Task<Guid?> LatestBatchIdAsync(Guid companyId, CancellationToken ct = default)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.LaborImportBatches.AsNoTracking()
                .Where(b => b.CompanyId == companyId && !b.isDeleted)
                .OrderByDescending(b => b.createdOn)
                .Select(b => (Guid?)b.Id)
                .FirstOrDefaultAsync(ct);
        }

        public async Task RenumberAsync(Guid batchId, LaborActor actor, CancellationToken ct = default)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var batch = await TrackedBatchAsync(db, batchId, ct);
            if (batch.State != LaborBatchState.Applied)
                throw new InvalidOperationException("أعِد الترقيم بعد اعتماد العمالة وحفظها.");

            var company = batch.Company ?? throw new InvalidOperationException("الشركة غير موجودة.");
            if (company.isDeleted)
                throw new InvalidOperationException("الشركة لم تعد موجودة.");

            var workers = await db.LaborWorkers
                .Where(w => w.CompanyId == company.Id && !w.isDeleted)
                .ToListAsync(ct);
            var year = DateTime.Now.Year;
            var active = workers
                .Where(w => w.Status == LaborWorkerState.Active)
                .OrderBy(w => w.FullName, StringComparer.Create(CultureInfo.GetCultureInfo("ar"), true))
                .ThenBy(w => w.createdOn)
                .ThenBy(w => w.Id)
                .ToList();
            if (active.Count == 0)
                throw new InvalidOperationException("لا يوجد عمال مستمرون لإعادة ترقيمهم.");

            var pool = new SortedSet<int>(Enumerable.Range(1, 999));
            foreach (var worker in workers)
            {
                if (worker.Status == LaborWorkerState.Active)
                    continue;
                if (LaborPermitNumber.TryReadSequence(worker.PermitNo, year, company.PermitCode, out var reserved))
                    pool.Remove(reserved);
            }

            if (active.Count > pool.Count)
                throw new InvalidOperationException("أرقام السنة الحالية لا تتسع للعمال المستمرين.");

            var numbers = new Dictionary<Guid, string>(active.Count);
            foreach (var worker in active)
                numbers[worker.Id] = LaborPermitNumber.Compose(year, company.PermitCode, Take(pool));

            foreach (var worker in active)
                worker.PermitNo = "T" + worker.Id.ToString("N")[..15];
            await db.SaveChangesAsync(ct);

            foreach (var worker in active)
            {
                worker.PermitNo = numbers[worker.Id];
                Touch(worker, actor, create: false);
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            await AuditAsync(
                AuditArea.Permits,
                AuditAction.Edit,
                "إعادة ترقيم " + active.Count + " عامل مستمر لشركة " + company.Name
                    + " ابتداءً من " + numbers[active[0].Id],
                actor);
        }

        public async Task ApplyAsync(Guid batchId, LaborActor actor, CancellationToken ct = default)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var batch = await TrackedBatchAsync(db, batchId, ct);
            if (batch.State != LaborBatchState.Draft)
                throw new InvalidOperationException("هذه الدفعة مُعتمدة أو ملغاة.");

            var company = batch.Company ?? throw new InvalidOperationException("الشركة غير موجودة.");
            if (company.isDeleted)
                throw new InvalidOperationException("الشركة لم تعد موجودة.");
            if (batch.Mode == LaborBatchMode.Add && company.Status != LaborCompanyState.Active)
                throw new InvalidOperationException("الشركة متوقفة، ولا تُقبل إضافة عمالة لها.");

            var lines = batch.Lines.Where(l => !l.isDeleted && l.Action != LaborLineAction.Skip).ToList();
            if (lines.Count == 0)
                throw new InvalidOperationException("لا توجد تعديلات لاعتمادها. يمكن إلغاء الأمر.");

            var workers = await db.LaborWorkers
                .Where(w => w.CompanyId == company.Id && !w.isDeleted)
                .ToListAsync(ct);
            var byId = workers.ToDictionary(w => w.Id);

            var adds = lines.Where(l => l.Action == LaborLineAction.Add).ToList();
            foreach (var line in adds)
            {
                if (string.IsNullOrWhiteSpace(line.ProposedPermitNo))
                    throw new InvalidOperationException("تعذر تحديد رقم تصريح للعامل " + line.FullName);
                if (ResolveWorker(workers, line.FullName, line.PassportNo) != null)
                    throw new InvalidOperationException("تغير سجل الشركة بعد إعداد المعاينة. ألغِ الدفعة وأعد إرفاق الملف.");
            }

            var taken = workers.Select(w => w.PermitNo).ToHashSet(StringComparer.Ordinal);
            foreach (var number in adds.Select(l => l.ProposedPermitNo!))
            {
                if (!taken.Add(number))
                    throw new InvalidOperationException("رقم التصريح " + number + " مكرر. ألغِ الدفعة وأعد إرفاق الملف.");
            }

            foreach (var line in lines)
            {
                switch (line.Action)
                {
                    case LaborLineAction.Add:
                        var created = new LaborWorker
                        {
                            CompanyId = company.Id,
                            PermitNo = line.ProposedPermitNo!,
                            FullName = line.FullName,
                            Nationality = line.Nationality,
                            PassportNo = line.PassportNo,
                            Status = line.ProposedStatus ?? LaborWorkerState.Active,
                            Address = line.Address,
                            Notes = line.Notes
                        };
                        Touch(created, actor, create: true);
                        db.LaborWorkers.Add(created);
                        break;
                    case LaborLineAction.UpdateStatus:
                    case LaborLineAction.Renumber:
                    case LaborLineAction.CloseService:
                        if (line.WorkerId == null || !byId.TryGetValue(line.WorkerId.Value, out var worker))
                            throw new InvalidOperationException("تغير سجل الشركة بعد إعداد المعاينة. ألغِ الدفعة وأعد إرفاق الملف.");
                        if (line.Action == LaborLineAction.CloseService)
                            worker.Status = LaborWorkerState.Ended;
                        else if (line.Action == LaborLineAction.UpdateStatus && !string.IsNullOrWhiteSpace(line.ProposedStatus))
                            worker.Status = line.ProposedStatus;
                        Touch(worker, actor, create: false);
                        break;
                }
            }

            batch.State = LaborBatchState.Applied;
            batch.AppliedOn = DateTime.Now;
            Touch(batch, actor, create: false);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            var closed = lines.Count(l => l.Action == LaborLineAction.CloseService);
            var updated = lines.Count(l => l.Action == LaborLineAction.UpdateStatus);
            var added = adds.Count;
            await AuditAsync(
                AuditArea.Permits,
                AuditAction.Edit,
                "اعتماد " + LaborBatchMode.Label(batch.Mode) + " لشركة " + company.Name
                    + " — إضافة " + added + "، تعديل " + updated + "، إغلاق خدمة " + closed,
                actor);
        }

        public async Task CancelAsync(Guid batchId, LaborActor actor, CancellationToken ct = default)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var batch = await TrackedBatchAsync(db, batchId, ct);
            if (batch.State != LaborBatchState.Draft)
                throw new InvalidOperationException("هذه الدفعة مُعتمدة أو ملغاة.");

            batch.State = LaborBatchState.Cancelled;
            Touch(batch, actor, create: false);
            await db.SaveChangesAsync(ct);
            await AuditAsync(
                AuditArea.Permits,
                AuditAction.Cancel,
                "إلغاء معالجة عمالة لشركة " + (batch.Company?.Name ?? ""),
                actor);
        }

        public async Task<List<LaborFollowRow>> SearchAsync(
            Guid? companyId,
            string? term,
            string? status,
            CancellationToken ct = default)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var query = from worker in db.LaborWorkers.AsNoTracking()
                        join company in db.LaborCompanies.AsNoTracking() on worker.CompanyId equals company.Id
                        where !worker.isDeleted && !company.isDeleted
                        select new { worker, company };

            if (companyId != null && companyId != Guid.Empty)
                query = query.Where(x => x.company.Id == companyId);
            if (!string.IsNullOrWhiteSpace(status) && LaborWorkerState.TryParse(status, out var parsed) && status != "All")
                query = query.Where(x => x.worker.Status == parsed);

            var needle = LaborText.Compact(term);
            if (needle.Length > 0)
            {
                query = query.Where(x =>
                    x.worker.FullName.Contains(needle)
                    || x.worker.PermitNo.Contains(needle)
                    || (x.worker.PassportNo != null && x.worker.PassportNo.Contains(needle))
                    || (x.worker.Nationality != null && x.worker.Nationality.Contains(needle))
                    || x.company.Name.Contains(needle)
                    || x.company.Number.Contains(needle));
            }

            var rows = await query
                .OrderBy(x => x.company.Name)
                .ThenBy(x => x.worker.PermitNo)
                .Take(5000)
                .ToListAsync(ct);

            return rows.Select(x => new LaborFollowRow
            {
                WorkerId = x.worker.Id,
                CompanyName = x.company.Name,
                CompanyNumber = x.company.Number,
                PermitNo = x.worker.PermitNo,
                FullName = x.worker.FullName,
                Nationality = x.worker.Nationality,
                PassportNo = x.worker.PassportNo,
                Status = x.worker.Status,
                Address = x.worker.Address,
                Notes = x.worker.Notes
            }).ToList();
        }

        public async Task MarkPrintedAsync(IReadOnlyList<Guid> workerIds, LaborActor actor, CancellationToken ct = default)
        {
            if (workerIds == null || workerIds.Count == 0)
                return;

            await using var db = await _factory.CreateDbContextAsync(ct);
            var ids = workerIds.Distinct().ToList();
            var workers = await db.LaborWorkers.Where(w => ids.Contains(w.Id) && !w.isDeleted).ToListAsync(ct);
            var now = DateTime.Now;
            foreach (var worker in workers)
            {
                if (worker.PrintedAt == null)
                    worker.PrintedAt = now;
                Touch(worker, actor, create: false);
            }

            await db.SaveChangesAsync(ct);
            await AuditAsync(AuditArea.Permits, AuditAction.Edit, "طباعة " + workers.Count + " تصريح عمالة", actor);
        }

        private static async Task<LaborImportBatch> TrackedBatchAsync(DataContext db, Guid id, CancellationToken ct)
        {
            return await db.LaborImportBatches
                .Include(b => b.Company)
                .Include(b => b.Lines)
                .FirstOrDefaultAsync(b => b.Id == id && !b.isDeleted, ct)
                ?? throw new InvalidOperationException("المعاينة غير موجودة.");
        }

        private static List<PreparedRow> PrepareRows(IReadOnlyList<LaborSheetRow> rows)
        {
            var prepared = new List<PreparedRow>(rows.Count);
            var passports = new HashSet<string>(StringComparer.Ordinal);
            var bareNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                var name = Required(row.Name, "اسم العامل في الصف " + row.RowNumber, 200);
                if (!LaborWorkerState.TryParse(row.StatusText, out var status))
                    throw new InvalidOperationException("حالة غير معروفة في الصف " + row.RowNumber + ".");

                var passport = Clip(row.PassportNo, 40, "رقم الجواز في الصف " + row.RowNumber);
                var nameKey = LaborText.MatchKey(name);
                var passportKey = LaborText.PassportKey(passport);
                if (passportKey.Length > 0 && !passports.Add(passportKey))
                    throw new InvalidOperationException("رقم الجواز مكرر داخل الملف في الصف " + row.RowNumber + ".");
                if (passportKey.Length == 0 && !bareNames.Add(nameKey))
                    throw new InvalidOperationException("الاسم «" + name + "» مكرر داخل الملف دون رقم جواز.");

                prepared.Add(new PreparedRow
                {
                    Name = name,
                    NameKey = nameKey,
                    PassportKey = passportKey,
                    Nationality = Clip(row.Nationality, 80, "الجنسية"),
                    PassportNo = passport,
                    Status = status,
                    Address = Clip(row.Address, 300, "العنوان"),
                    Notes = Clip(row.Notes, 500, "الملاحظات")
                });
            }

            foreach (var group in prepared.GroupBy(row => row.NameKey))
            {
                if (group.Count() > 1 && group.Any(row => row.PassportKey.Length == 0))
                    throw new InvalidOperationException("الاسم «" + group.First().Name + "» مكرر في الملف دون رقم جواز لكل الصفوف.");
            }

            return prepared;
        }

        private static void EnsureWorkersResolvable(IReadOnlyList<LaborWorker> workers)
        {
            var passports = new HashSet<string>(StringComparer.Ordinal);
            var bareNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var worker in workers)
            {
                var passport = LaborText.PassportKey(worker.PassportNo);
                if (passport.Length > 0 && !passports.Add(passport))
                    throw new InvalidOperationException("رقم الجواز " + worker.PassportNo + " مكرر في سجل الشركة. صحّحه قبل المعالجة الجماعية.");

                if (passport.Length == 0 && !bareNames.Add(LaborText.MatchKey(worker.FullName)))
                    throw new InvalidOperationException("الاسم «" + worker.FullName + "» مكرر دون رقم جواز. أضف رقم الجواز قبل المعالجة الجماعية.");
            }
        }

        private static List<LaborImportLine> BuildLines(
            string mode,
            IReadOnlyList<LaborWorker> workers,
            IReadOnlyList<PreparedRow> rows,
            string permitCode)
        {
            var matched = new HashSet<Guid>();
            var lines = new List<LaborImportLine>();
            var sort = 0;
            var year = DateTime.Now.Year;
            var used = new HashSet<int>();
            foreach (var worker in workers)
            {
                if (LaborPermitNumber.TryReadSequence(worker.PermitNo, year, permitCode, out var sequence))
                    used.Add(sequence);
            }

            foreach (var row in rows)
            {
                var worker = ResolveWorker(workers, row.Name, row.PassportNo);
                if (worker != null)
                    matched.Add(worker.Id);

                if (mode == LaborBatchMode.Add)
                {
                    if (worker != null)
                    {
                        lines.Add(Line(++sort, LaborLineAction.Skip, worker, row.Name, worker.PassportNo, worker.Status, worker.Status));
                        continue;
                    }

                    lines.Add(NewWorkerLine(++sort, row, year, permitCode, used));
                    continue;
                }

                if (worker == null)
                {
                    lines.Add(NewWorkerLine(++sort, row, year, permitCode, used));
                    continue;
                }

                if (row.Status == LaborWorkerState.Ended)
                {
                    if (worker.Status != LaborWorkerState.Ended)
                        lines.Add(Line(++sort, LaborLineAction.CloseService, worker, worker.FullName, worker.PassportNo, worker.Status, LaborWorkerState.Ended));
                    continue;
                }

                if (worker.Status != row.Status)
                    lines.Add(Line(++sort, LaborLineAction.UpdateStatus, worker, worker.FullName, worker.PassportNo, worker.Status, row.Status));
            }

            if (mode == LaborBatchMode.Match)
            {
                foreach (var worker in workers.OrderBy(w => w.PermitNo, StringComparer.Ordinal))
                {
                    if (matched.Contains(worker.Id) || worker.Status == LaborWorkerState.Ended)
                        continue;
                    lines.Add(Line(++sort, LaborLineAction.CloseService, worker, worker.FullName, worker.PassportNo, worker.Status, LaborWorkerState.Ended));
                }
            }

            return lines;
        }

        private static LaborImportLine NewWorkerLine(int sort, PreparedRow row, int year, string permitCode, HashSet<int> used)
        {
            var number = LaborPermitNumber.Compose(year, permitCode, Take(used));
            return new LaborImportLine
            {
                Action = LaborLineAction.Add,
                FullName = row.Name,
                Nationality = row.Nationality,
                PassportNo = row.PassportNo,
                Address = row.Address,
                Notes = row.Notes,
                ProposedPermitNo = number,
                ProposedStatus = row.Status,
                SortOrder = sort
            };
        }

        private static LaborImportLine Line(
            int sort,
            string action,
            LaborWorker worker,
            string name,
            string? passport,
            string? currentStatus,
            string? proposedStatus) => new()
        {
            Action = action,
            WorkerId = worker.Id,
            CurrentPermitNo = worker.PermitNo,
            ProposedPermitNo = worker.PermitNo,
            FullName = name,
            PassportNo = passport,
            CurrentStatus = currentStatus,
            ProposedStatus = proposedStatus,
            SortOrder = sort
        };

        private static LaborWorker? ResolveWorker(IReadOnlyList<LaborWorker> workers, string name, string? passport)
        {
            var passportKey = LaborText.PassportKey(passport);
            var nameKey = LaborText.MatchKey(name);
            if (passportKey.Length > 0)
            {
                var byPassport = workers.FirstOrDefault(w => LaborText.PassportKey(w.PassportNo) == passportKey);
                if (byPassport != null)
                    return byPassport;

                var unnamed = workers
                    .Where(w => LaborText.MatchKey(w.FullName) == nameKey && LaborText.PassportKey(w.PassportNo).Length == 0)
                    .ToList();
                if (unnamed.Count > 1)
                    throw new InvalidOperationException("أكثر من عامل باسم «" + name + "» دون رقم جواز.");
                return unnamed.Count == 1 ? unnamed[0] : null;
            }

            var named = workers.Where(w => LaborText.MatchKey(w.FullName) == nameKey).ToList();
            if (named.Count > 1)
                throw new InvalidOperationException("أكثر من عامل باسم «" + name + "». أضف رقم الجواز في الملف لتمييزهم.");
            return named.FirstOrDefault();
        }

        private static async Task<string> NextPermitCodeAsync(DataContext db, CancellationToken ct)
        {
            var used = await db.LaborCompanies
                .Where(c => !c.isDeleted)
                .Select(c => c.PermitCode)
                .ToListAsync(ct);
            var taken = used.ToHashSet(StringComparer.Ordinal);
            for (var i = 1; i <= 99; i++)
            {
                var code = i.ToString("00", CultureInfo.InvariantCulture);
                if (!taken.Contains(code))
                    return code;
            }

            throw new InvalidOperationException("استُنفدت رموز الشركات (01 إلى 99).");
        }

        private static async Task<string> NextPermitNoAsync(DataContext db, LaborCompany company, CancellationToken ct)
        {
            var year = DateTime.Now.Year;
            var numbers = await db.LaborWorkers
                .Where(w => !w.isDeleted)
                .Select(w => w.PermitNo)
                .ToListAsync(ct);
            var used = new HashSet<int>();
            foreach (var number in numbers)
            {
                if (LaborPermitNumber.TryReadSequence(number, year, company.PermitCode, out var sequence))
                    used.Add(sequence);
            }

            return LaborPermitNumber.Compose(year, company.PermitCode, Take(used));
        }

        private static void EnsurePersonIsDistinct(
            string name,
            string? passport,
            IEnumerable<(string FullName, string? PassportNo)> others)
        {
            var passportKey = LaborText.PassportKey(passport);
            var nameKey = LaborText.MatchKey(name);
            foreach (var other in others)
            {
                var otherPassport = LaborText.PassportKey(other.PassportNo);
                if (passportKey.Length > 0 && passportKey == otherPassport)
                    throw new InvalidOperationException("رقم الجواز مسجّل لعامل آخر في الشركة.");
                if (passportKey.Length == 0 && otherPassport.Length == 0 && nameKey == LaborText.MatchKey(other.FullName))
                    throw new InvalidOperationException("يوجد عامل آخر بنفس الاسم دون رقم جواز.");
            }
        }

        private static int Take(ISet<int> pool)
        {
            for (var i = 1; i <= 999; i++)
            {
                if (pool is SortedSet<int> sorted)
                {
                    if (sorted.Count == 0)
                        break;
                    var value = sorted.Min;
                    sorted.Remove(value);
                    return value;
                }

                if (pool.Add(i))
                    return i;
            }

            throw new InvalidOperationException("استُنفد تسلسل تصاريح هذه الشركة لهذه السنة (999).");
        }

        private async Task AuditAsync(string area, string action, string summary, LaborActor actor)
        {
            await _audit.WriteAsync(new AuditLogEntry
            {
                SourceKey = "labor:" + Guid.NewGuid().ToString("N"),
                Area = area,
                Action = action,
                Summary = summary,
                ActorFileNumber = actor.UserName,
                ActorName = string.IsNullOrWhiteSpace(actor.FullName) ? actor.UserName : actor.FullName,
                OccurredAt = DateTime.Now,
                createdBy = actor.UserName
            });
        }

        private static LaborCompanyRow MapCompany(LaborCompany company, int workers) => new()
        {
            Id = company.Id,
            Number = company.Number,
            Name = company.Name,
            Address = company.Address,
            Specialty = company.Specialty,
            Status = company.Status,
            ContractStart = company.ContractStart,
            ContractEnd = company.ContractEnd,
            PermitCode = company.PermitCode,
            WorkerCount = workers
        };

        private static LaborWorkerRow MapWorker(LaborWorker worker, LaborCompany company) => new()
        {
            Id = worker.Id,
            CompanyId = company.Id,
            CompanyName = company.Name,
            CompanyStatus = company.Status,
            PermitNo = worker.PermitNo,
            FullName = worker.FullName,
            Nationality = worker.Nationality,
            PassportNo = worker.PassportNo,
            Status = worker.Status,
            Address = worker.Address,
            Notes = worker.Notes,
            PrintedAt = worker.PrintedAt
        };

        private static void Touch(Base entity, LaborActor actor, bool create)
        {
            var now = DateTime.Now;
            if (create)
            {
                if (entity.Id == Guid.Empty)
                    entity.Id = Guid.NewGuid();
                entity.createdBy = actor.UserName ?? "";
                entity.createdOn = now;
                entity.isDeleted = false;
            }
            else
            {
                entity.updatedBy = actor.UserName ?? "";
                entity.updatedOn = now;
            }
        }

        private static string Required(string? value, string label, int max)
        {
            var text = LaborText.Compact(value);
            if (text.Length == 0)
                throw new InvalidOperationException(label + " مطلوب.");
            if (text.Length > max)
                throw new InvalidOperationException(label + " أطول من المسموح.");
            return text;
        }

        private static string? Truncate(string? value, int max)
        {
            var text = LaborText.Compact(value);
            if (text.Length == 0)
                return null;
            return text.Length <= max ? text : text[..max];
        }

        private static string? Clip(string? value, int max, string label)
        {
            var text = LaborText.Compact(value);
            if (text.Length == 0)
                return null;
            if (text.Length > max)
                throw new InvalidOperationException(label + " أطول من المسموح.");
            return text;
        }

        private static string? EmptyToNull(string value) => value.Length == 0 ? null : value;

        private static string? ReadText(IDataRecord reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal))
                return null;
            return Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
        }

        private sealed class PreparedRow
        {
            public string Name { get; init; } = "";
            public string NameKey { get; init; } = "";
            public string PassportKey { get; init; } = "";
            public string? Nationality { get; init; }
            public string? PassportNo { get; init; }
            public string Status { get; init; } = "";
            public string? Address { get; init; }
            public string? Notes { get; init; }
        }
    }
}
