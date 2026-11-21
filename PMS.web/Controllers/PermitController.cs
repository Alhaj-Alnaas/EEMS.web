using Core.Interfaces.Services;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.AspNetCore.Mvc;
using QRCoder;

namespace PMS.web.Controllers
{
    /// <summary>PDF print for a material permit. The screen itself is Blazor.</summary>
    public class PermitController : Controller
    {
        private readonly IPermit _permitService;
        private readonly IDepartmentService _departmentService;
        private readonly IWebHostEnvironment _hostingEnvironment;

        public PermitController(
            IPermit permitService,
            IDepartmentService departmentService,
            IWebHostEnvironment hostingEnvironment)
        {
            _permitService = permitService;
            _departmentService = departmentService;
            _hostingEnvironment = hostingEnvironment;
        }

        [HttpGet("/permits/{permitId:guid}/pdf")]
        public async Task<IActionResult> PrintPermitPdf(Guid permitId)
        {
            var permit = await _permitService.GetPermitByIdAsync(permitId);
            if (permit == null)
                return NotFound();

            using var stream = new MemoryStream();
            var document = new Document(PageSize.A4, 40, 40, 80, 50);
            var writer = PdfWriter.GetInstance(document, stream);
            document.Open();

            // font setting
            string fontPath = Path.Combine(_hostingEnvironment.WebRootPath, "fonts", "Amiri-Regular.ttf");
            if (!System.IO.File.Exists(fontPath))
                fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");

            var baseFont = BaseFont.CreateFont(fontPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
            var normalFont = new Font(baseFont, 12);
            var boldFont = new Font(baseFont, 13, Font.BOLD);
            var sectionTitleFont = new Font(baseFont, 16, Font.BOLD);

           
            PdfContentByte cb = writer.DirectContent;
            cb.SetColorStroke(BaseColor.Black);
            cb.Rectangle(30, 30, document.PageSize.Width - 60, document.PageSize.Height - 60);
            cb.Stroke();

          
            string logoPath = Path.Combine(_hostingEnvironment.WebRootPath, "images", "company-logo.png"); 
            if (System.IO.File.Exists(logoPath))
            {
                Image logo = Image.GetInstance(logoPath);
                logo.ScaleAbsolute(30, 30);
                logo.SetAbsolutePosition(document.PageSize.Width - 100, document.PageSize.Height - 70);
                document.Add(logo);
            }

           
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

            //  QR Code 
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

            // form hader
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

            // permint main data
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
            var department = await _departmentService.GetSingleDepartmentByResponsibilityAsync(permit.reqDepartment);
            AddRow("رقم التصريح :", permit.no?.ToString());
            var classification = permit.classification?.Trim() ?? "";
            var isMaterials = classification.Equals("Materials", StringComparison.OrdinalIgnoreCase)
                || classification is "مواد" or "تصريح مواد";
            if (isMaterials)
                AddRow("نوع التصريح :", _permitService.MappingAsync(permit.type?.ToString()));
            AddRow("تصنيف التصريح :", _permitService.MappingAsync(permit.classification?.ToString()));
            AddRow("الجهة المُعدة للتصريح:", department.DepartmentName);
            AddRow("الجهة المصرح لها:", permit.OrgDescription);
            AddRow("تاريخ الطلب :", permit.date.ToString("yyyy/MM/dd"));
            AddRow("حالة التصريح :", _permitService.MappingAsync(permit.status.ToString()));
            AddRow("ملاحظات:", permit.remarks);

            // main data
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
            document.Add(new Paragraph("\n\n"));

            // matirails
            if (permit.EquipmentsAndMatirials?.Any() == true)
            {
                ColumnText.ShowTextAligned(
                    cb,
                    Element.ALIGN_CENTER,
                    new Phrase("قائمة المواد / المعدات", sectionTitleFont),
                    document.PageSize.Width / 2,
                    writer.GetVerticalPosition(false) - 10,
                    0,
                    PdfWriter.RUN_DIRECTION_RTL,
                    0
                );
                document.Add(new Paragraph("\n"));

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
                document.Add(new Paragraph("\n\n"));
            }

            // Cars
            if (permit.Cars?.Any() == true)
            {
                ColumnText.ShowTextAligned(
                    cb,
                    Element.ALIGN_CENTER,
                    new Phrase("المركبات الناقلة", sectionTitleFont),
                    document.PageSize.Width / 2,
                    writer.GetVerticalPosition(false) - 10,
                    0,
                    PdfWriter.RUN_DIRECTION_RTL,
                    0
                );
                document.Add(new Paragraph("\n"));

                PdfPTable carTable = new PdfPTable(5)
                {
                    RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                    WidthPercentage = 100
                };
                carTable.SetWidths(new float[] { 2f, 1f, 2f, 1f, 1f });

                string[] carHeaders = { "نوع المركبة", "رقم اللوحة", "اسم السائق", "جنسيته", "رقم الرخصة" };
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
                    carTable.AddCell(new Phrase(v.carType ?? "-", normalFont));
                    carTable.AddCell(new Phrase(v.carNo ?? "-", normalFont));
                    carTable.AddCell(new Phrase(v.name ?? "-", normalFont));
                    carTable.AddCell(new Phrase(v.nattiunality ?? "-", normalFont));
                    carTable.AddCell(new Phrase(v.licenseNo ?? "-", normalFont));
                }

                document.Add(carTable);
                document.Add(new Paragraph("\n\n\n\n"));
            }

            // Procedures
            if (permit.Procedures?.Any() == true)
            {
                ColumnText.ShowTextAligned(
                    cb,
                    Element.ALIGN_CENTER,
                    new Phrase("الإجراءات المتخذة", sectionTitleFont),
                    document.PageSize.Width / 2,
                    writer.GetVerticalPosition(false) - 10,
                    0,
                    PdfWriter.RUN_DIRECTION_RTL,
                    0
                );
                document.Add(new Paragraph("\n"));

                PdfPTable procTable = new PdfPTable(5)
                {
                    RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                    WidthPercentage = 100
                };
                procTable.SetWidths(new float[] { 1.5f, 2f, 2f, 2f, 1.5f });

                string[] procHeaders = { "الإجراء المُتخذ", "تمت بواسطة", "الصفة", "تاريخ الإجراء", "التوقيع" };
                foreach (var h in procHeaders)
                {
                    procTable.AddCell(new PdfPCell(new Phrase(h, boldFont))
                    {
                        HorizontalAlignment = Element.ALIGN_CENTER,
                        Padding = 8,
                        BorderWidth = 1
                    });
                }

                foreach (var p in permit.Procedures.OrderBy(p => p.doneOn))
                {
                    procTable.AddCell(new Phrase(_permitService.MappingAsync(p.procedureType ?? "-"), normalFont));
                    procTable.AddCell(new Phrase(p.donedBy ?? "-", normalFont));
                    procTable.AddCell(new Phrase(p.doneAs ?? "-", normalFont));
                    procTable.AddCell(new Phrase(p.doneOn?.ToString("yyyy/MM/dd HH:mm") ?? "-", normalFont));
                    procTable.AddCell(new Phrase( "......................", normalFont));
                }

                document.Add(procTable);
            }

            // fortter
            document.Add(new Paragraph("\n\n\n"));
            var footer = new Paragraph($"Printed on: {DateTime.Now:yyyy/MM/dd HH:mm}", normalFont)
            {
                Alignment = Element.ALIGN_CENTER
            };
            document.Add(footer);

            document.Close();
            writer.Close();

            return File(stream.ToArray(), "application/pdf");
        }

    }
}
