using Core.Entities;
using Core.Interfaces.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using iTextSharp.text;
using iTextSharp.text.pdf;
using iTextSharp.text;
using iTextSharp.text.pdf;
using QRCoder;


namespace EEMS.web.Controllers
{
    public class PermitController : Controller

    {
         private readonly IPermit _permitService;
         private readonly IDepartmentService _departmentService;
         private readonly IProcedureMovment _procedureMovmentService;
         private readonly UserManager<User> _userManager;
        private readonly IWebHostEnvironment _hostingEnvironment;

        public PermitController(IPermit permitService, IDepartmentService departmentService, IProcedureMovment procedureMovmentService, UserManager<User> userManager, IWebHostEnvironment hostingEnvironment)
    {
        _permitService = permitService;
        _departmentService = departmentService;
        _userManager = userManager;
        _procedureMovmentService=procedureMovmentService; 
         _hostingEnvironment = hostingEnvironment;   
    }

    // GET: Permit/Index
    public async Task<IActionResult> PermitIndex()
        {
            var user = await _userManager.FindByNameAsync(HttpContext.Session.GetString("UserName")); 
            var permits = await _permitService.GetAllPermitAsync(user);
            return View(permits);
        }

        // GET: Permit/Details/{id}
        public async Task<IActionResult> Details(Guid id)
        {
            var permit = await _permitService.GetPermitByIdAsync(id);
            if (permit == null) return NotFound();
            return View(permit);
        }

        // GET: Permit/Edit/{id}
        public async Task<IActionResult> Edit(Guid id)
        {
            var permit = await _permitService.GetPermitByIdAsync(id);
            if (permit == null) return NotFound();
            return View(permit);
        }

        [HttpPost]
        public async Task<JsonResult> HandlePermitAction(Guid permitId, string actionType, string reason)
        {
            try
            {
                var userName = HttpContext.Session.GetString("UserName");
                var user = await _userManager.FindByNameAsync(userName);

                var CurrentPermit = await _permitService.GetPermitByIdAsync(permitId);
                if (CurrentPermit == null)
                    return Json(new { success = false, message = "لم يتم العثور على التصريح." });

                // إنشاء الحركة
                var movement = new ProcedureMovment
                {
                    permitId = permitId,
                    permit = CurrentPermit,
                    donedBy = user.FullName,
                    doneAs = user.JobtypeName,
                    shift = "A", // يمكن جلبها لاحقاً من المستخدم أو النظام
                    doneOn = DateTime.Now,
                    procedureType = actionType,
                    remarks = reason
                };

                // حفظ الحركة
                await _procedureMovmentService.InsertProcedureMovment(movement);

                // تحديث حالة التصريح
                switch (actionType)
                {
                    case "approve":

                        if (user.ResponsibilityCode=="45011"){ CurrentPermit.PermintsSectionApproval = true; }
                        else if (user.ResponsibilityCode == "45012") { CurrentPermit.secuSectionApproval = true; }
                        else   { CurrentPermit.reqDepApproval = true; }

                        if (CurrentPermit.reqDepApproval == true && CurrentPermit.secuSectionApproval == true)
                        {
                            CurrentPermit.status = 'A';
                        }
                        
                        break;
                    case "reject":
                        CurrentPermit.status = 'J';
                        break;
                    case "return":
                        CurrentPermit.status = 'R';
                        break;
                    case "close":
                        CurrentPermit.status = 'C';
                        break;
                    case "delete":
                        CurrentPermit.status = 'D';
                        CurrentPermit.isDeleted = true;
                        CurrentPermit.deletedBy = user.FullName;
                        CurrentPermit.deletedOn = DateTime.Now;
                        break;
                }

                await _permitService.UpdatePermitAsync(CurrentPermit);

                string successMessage = actionType switch
                {
                    "approve" => "تم اعتماد التصريح بنجاح ✅",
                    "reject" => "تم رفض التصريح بنجاح ❌",
                    "return" => "تم ترجيع التصريح بنجاح 🔁",
                    "close" => "تم إغلاق التصريح بنجاح 🔒",
                    "delete" => "تم حذف التصريح بنجاح 🗑️",
                    _ => "تم تنفيذ العملية بنجاح"
                };

                return Json(new { success = true, message = successMessage });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "حدث خطأ أثناء تنفيذ العملية: " + ex.Message });
            }
        }

        [HttpGet]

        public async Task<IActionResult> PrintPermitPdf(Guid permitId)
        {
            var permit = await _permitService.GetPermitByIdAsync(permitId);
            if (permit == null)
                return NotFound();

            using var stream = new MemoryStream();
            var document = new Document(PageSize.A4, 40, 40, 80, 50);
            var writer = PdfWriter.GetInstance(document, stream);
            document.Open();

            // 🏗️ إعداد الخط العربي
            string fontPath = Path.Combine(_hostingEnvironment.WebRootPath, "fonts", "Amiri-Regular.ttf");
            if (!System.IO.File.Exists(fontPath))
                fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");

            var baseFont = BaseFont.CreateFont(fontPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
            var normalFont = new Font(baseFont, 12);
            var boldFont = new Font(baseFont, 13, Font.BOLD);
            var sectionTitleFont = new Font(baseFont, 16, Font.BOLD);

            // 🖼️ رسم الإطار العام للصفحة
            PdfContentByte cb = writer.DirectContent;
            cb.SetColorStroke(BaseColor.Black);
            cb.Rectangle(30, 30, document.PageSize.Width - 60, document.PageSize.Height - 60);
            cb.Stroke();

            // 🏢 شعار الشركة في الأعلى يمين الصفحة
            string logoPath = Path.Combine(_hostingEnvironment.WebRootPath, "images", "company-logo.png"); // ضع شعارك هنا
            if (System.IO.File.Exists(logoPath))
            {
                Image logo = Image.GetInstance(logoPath);
                logo.ScaleAbsolute(30, 30);
                logo.SetAbsolutePosition(document.PageSize.Width - 100, document.PageSize.Height - 70);
                document.Add(logo);
            }

            // 🧾 كتابة اسم الشركة أسفل الشعار في ثلاثة أسطر
            float textStartX = document.PageSize.Width - 50;
            float textStartY = document.PageSize.Height - 85;

            string[] companyLines =
            {
        "الشركة الليبية للحديد والصلب",
        "قطاع الشؤون الإدارية والخدمات",
        "إدارة الشؤون الأمنية"
    };

            foreach (var line in companyLines)
            {
                ColumnText.ShowTextAligned(
                    cb,
                    Element.ALIGN_RIGHT,
                    new Phrase(line, boldFont),
                    textStartX,
                    textStartY,
                    0,
                    PdfWriter.RUN_DIRECTION_RTL,
                    0
                );
                textStartY -= 16; // المسافة بين الأسطر
            }

            // 🧾 QR Code في الزاوية اليسرى العليا
            string qrText = $"Permit No: {permit.no}";
            using (var qrGen = new QRCoder.QRCodeGenerator())
            {
                var qrData = qrGen.CreateQrCode(qrText, QRCoder.QRCodeGenerator.ECCLevel.Q);
                var qrCode = new QRCoder.QRCode(qrData);
                using (var qrBitmap = qrCode.GetGraphic(3))
                {
                    using (var ms = new MemoryStream())
                    {
                        qrBitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        Image qrImg = Image.GetInstance(ms.ToArray());
                        qrImg.ScaleAbsolute(40, 40);
                        qrImg.SetAbsolutePosition(30, document.PageSize.Height - 70);
                        document.Add(qrImg);
                    }
                }
            }

            // 🧾 العنوان الرئيسي للنموذج
            ColumnText.ShowTextAligned(
                cb,
                Element.ALIGN_CENTER,
                new Phrase("نموذج تصريح دخول / خروج المواد", sectionTitleFont),
                document.PageSize.Width / 2,
                document.PageSize.Height - 120,
                0,
                PdfWriter.RUN_DIRECTION_RTL,
                0
            );

            document.Add(new Paragraph("\n\n\n"));

            // ✅ عنوان قسم البيانات الأساسية
            //ColumnText.ShowTextAligned(
            //    cb,
            //    Element.ALIGN_CENTER,
            //    new Phrase("البيانات الأساسية", sectionTitleFont),
            //    document.PageSize.Width / 2,
            //    document.PageSize.Height - 160,
            //    // writer.GetVerticalPosition(false) - 10,
            //    0,
            //    PdfWriter.RUN_DIRECTION_RTL,
            //    0
            //);

            //document.Add(new Paragraph("\n\n"));

            // ✅ جدول البيانات الأساسية
            PdfPTable baseTable = new PdfPTable(2)
            {
                RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                WidthPercentage = 100
            };
            baseTable.SetWidths(new float[] { 4.0f, 1.0f });

            void AddRow(string label, string value)
            {
                PdfPCell labelCell = new PdfPCell(new Phrase(label, boldFont))
                {
                    Border = PdfPCell.NO_BORDER,
                    Padding = 3,
                    HorizontalAlignment = Element.ALIGN_LEFT,
                    VerticalAlignment = Element.ALIGN_MIDDLE,
                };

                PdfPCell valueCell = new PdfPCell(new Phrase(value ?? "-", normalFont))
                {
                    Border = PdfPCell.NO_BORDER,
                    Padding = 3,
                    HorizontalAlignment = Element.ALIGN_LEFT,
                    VerticalAlignment = Element.ALIGN_MIDDLE,
                };

                baseTable.AddCell(labelCell);
                baseTable.AddCell(valueCell);
            }

            AddRow("رقم التصريح:", permit.no?.ToString());
            AddRow("نوع التصريح:", permit.type?.ToString());
            AddRow("الجهة:", permit.OrgDescription);
            AddRow("القسم الطالِب:", permit.reqDepartment);
            AddRow("تاريخ الطلب:", permit.date.ToString("yyyy/MM/dd"));
            AddRow("الحالة:", permit.status.ToString());
            AddRow("مقدم الطلب:", permit.requoidedAs);
            AddRow("ملاحظات:", permit.remarks);

            // ✅ إحاطة قسم البيانات الأساسية بإطار خفيف
            PdfPCell baseContainerCell = new PdfPCell(baseTable)
            {
                BorderColor = BaseColor.Gray,
                BorderWidth = 0.5f,
                Padding = 8
            };

            PdfPTable baseContainer = new PdfPTable(1)
            {
                RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                WidthPercentage = 100
            };
            baseContainer.AddCell(baseContainerCell);

            document.Add(baseContainer);
            document.Add(new Paragraph("\n"));

            // 🔹 جدول المواد
            if (permit.EquipmentsAndMatirials?.Any() == true)
            {
                ColumnText.ShowTextAligned(
                    cb,
                    Element.ALIGN_CENTER,
                    new Phrase("جدول المواد", sectionTitleFont),
                    document.PageSize.Width / 2,
                    writer.GetVerticalPosition(false) - 10,
                    0,
                    PdfWriter.RUN_DIRECTION_RTL,
                    0
                );
                document.Add(new Paragraph("\n\n"));

                PdfPTable matTable = new PdfPTable(4)
                {
                    RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                    WidthPercentage = 100
                };
                matTable.SetWidths(new float[] { 3f, 1f, 1f, 3f });

                string[] matHeaders = { "البيان", "الكمية", "الوحدة", "الملاحظات" };
                foreach (var h in matHeaders)
                {
                    matTable.AddCell(new PdfPCell(new Phrase(h, boldFont))
                    {
                        HorizontalAlignment = Element.ALIGN_CENTER,
                        Padding = 6,
                        BorderWidth = 1
                    });
                }

                foreach (var m in permit.EquipmentsAndMatirials)
                {
                    matTable.AddCell(new Phrase(m.description ?? "-", normalFont));
                    matTable.AddCell(new Phrase(m.qty.ToString() ?? "-", normalFont));
                    matTable.AddCell(new Phrase(m.unit ?? "-", normalFont));
                    matTable.AddCell(new Phrase(m.remarks ?? "-", normalFont));
                }

                document.Add(matTable);
                document.Add(new Paragraph("\n"));
            }

            // 🔹 جدول السيارات
            if (permit.Cars?.Any() == true)
            {
                ColumnText.ShowTextAligned(
                    cb,
                    Element.ALIGN_CENTER,
                    new Phrase("جدول السيارات", sectionTitleFont),
                    document.PageSize.Width / 2,
                    writer.GetVerticalPosition(false) - 10,
                    0,
                    PdfWriter.RUN_DIRECTION_RTL,
                    0
                );
                document.Add(new Paragraph("\n\n"));

                PdfPTable carTable = new PdfPTable(3)
                {
                    RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                    WidthPercentage = 100
                };
                carTable.SetWidths(new float[] { 2f, 2f, 2f });

                string[] carHeaders = { "رقم اللوحة", "اسم السائق", "رقم الرخصة" };
                foreach (var h in carHeaders)
                {
                    carTable.AddCell(new PdfPCell(new Phrase(h, boldFont))
                    {
                        HorizontalAlignment = Element.ALIGN_CENTER,
                        Padding = 6,
                        BorderWidth = 1
                    });
                }

                foreach (var v in permit.Cars)
                {
                    carTable.AddCell(new Phrase(v.carNo ?? "-", normalFont));
                    carTable.AddCell(new Phrase(v.name ?? "-", normalFont));
                    carTable.AddCell(new Phrase(v.licenseNo ?? "-", normalFont));
                }

                document.Add(carTable);
                document.Add(new Paragraph("\n"));
            }

            // 🔹 سجل الحركات
            if (permit.Procedures?.Any() == true)
            {
                ColumnText.ShowTextAligned(
                    cb,
                    Element.ALIGN_CENTER,
                    new Phrase("سجل الحركات", sectionTitleFont),
                    document.PageSize.Width / 2,
                    writer.GetVerticalPosition(false) - 10,
                    0,
                    PdfWriter.RUN_DIRECTION_RTL,
                    0
                );
                document.Add(new Paragraph("\n\n"));

                PdfPTable procTable = new PdfPTable(4)
                {
                    RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                    WidthPercentage = 100
                };
                procTable.SetWidths(new float[] { 2f, 2f, 2f, 3f });

                string[] procHeaders = { "نوع العملية", "تمت بواسطة", "الوظيفة", "التاريخ والوقت" };
                foreach (var h in procHeaders)
                {
                    procTable.AddCell(new PdfPCell(new Phrase(h, boldFont))
                    {
                        HorizontalAlignment = Element.ALIGN_CENTER,
                        Padding = 6,
                        BorderWidth = 1
                    });
                }

                foreach (var p in permit.Procedures.OrderBy(p => p.doneOn))
                {
                    procTable.AddCell(new Phrase(p.procedureType ?? "-", normalFont));
                    procTable.AddCell(new Phrase(p.donedBy ?? "-", normalFont));
                    procTable.AddCell(new Phrase(p.doneAs ?? "-", normalFont));
                    procTable.AddCell(new Phrase(p.doneOn?.ToString("yyyy/MM/dd HH:mm") ?? "-", normalFont));
                }

                document.Add(procTable);
            }

            // 🕓 التذييل
            document.Add(new Paragraph("\n"));
            var footer = new Paragraph($"Printed on: {DateTime.Now:yyyy/MM/dd HH:mm}", normalFont)
            {
                Alignment = Element.ALIGN_CENTER
            };
            document.Add(footer);

            document.Close();
            writer.Close();

            return File(stream.ToArray(), "application/pdf", $"Permit_{permit.no}.pdf");
        }


    }
}
