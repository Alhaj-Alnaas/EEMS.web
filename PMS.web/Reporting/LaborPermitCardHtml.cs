using System.Net;
using Core.Common;
using PMS.web.Helpers;
using QRCoder;

namespace PMS.web.Reporting
{
    public sealed class LaborPermitCard
    {
        public string PermitNo { get; init; } = "";
        public string FullName { get; init; } = "";
        public string CompanyName { get; init; } = "";
    }

    /// <summary>CR80 labor badge, same pouch size as the employee card, with a gate QR.</summary>
    public static class LaborPermitCardHtml
    {
        public static string Build(IReadOnlyList<LaborPermitCard> cards)
        {
            var sheets = string.Concat(cards.Select(Sheet));
            return $@"<!DOCTYPE html>
<html lang='ar' dir='rtl'>
<head>
<meta charset='utf-8' />
<title>تصريح عمالة</title>
<style>
@page {{ size: 86mm 54mm; margin: 0; }}
* {{ box-sizing: border-box; }}
html, body {{
  margin: 0; padding: 0;
  font-family: Tahoma, 'Segoe UI', Arial, sans-serif;
  background: #fff;
  color: #111;
}}
.sheet {{
  width: 86mm;
  height: 54mm;
  padding: 2mm;
  page-break-after: always;
}}
.sheet:last-child {{ page-break-after: auto; }}
.card {{
  width: 100%;
  height: 100%;
  border: 1.4px solid #222;
  outline: 0.6px solid #222;
  outline-offset: -2.2px;
  padding: 2.2mm 2.4mm 2mm;
  display: flex;
  flex-direction: row;
  direction: ltr;
  gap: 2.2mm;
  position: relative;
  overflow: hidden;
  background: #fff;
}}
.logo {{
  position: absolute;
  top: 1.6mm;
  right: 2mm;
  width: 16mm;
  height: auto;
  max-height: 8mm;
  object-fit: contain;
}}
.qr {{
  width: 22mm;
  flex: 0 0 22mm;
  align-self: center;
  image-rendering: pixelated;
}}
.info {{
  flex: 1;
  min-width: 0;
  direction: rtl;
  text-align: right;
  display: flex;
  flex-direction: column;
  justify-content: flex-end;
  padding-top: 8mm;
  gap: 0.7mm;
}}
.org {{
  position: absolute;
  top: 1.5mm;
  right: 19mm;
  left: 25mm;
  text-align: right;
  font-size: 7.5px;
  font-weight: 700;
  line-height: 1.25;
}}
.title {{
  font-size: 11px;
  font-weight: 800;
}}
.row {{
  display: flex;
  gap: 1.2mm;
  align-items: baseline;
  white-space: nowrap;
  overflow: hidden;
  font-size: 9.5px;
  line-height: 1.25;
}}
.label {{ font-weight: 700; flex: none; }}
.value {{
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
}}
.permit {{
  color: #9a1148;
  font-weight: 800;
  font-size: 12px;
  letter-spacing: 0.4px;
}}
.year {{
  font-size: 8px;
  font-weight: 700;
  color: #333;
}}
@media print {{
  html, body {{ width: 86mm; height: 54mm; }}
  .sheet {{ page-break-inside: avoid; }}
}}
</style>
</head>
<body>
{sheets}
</body>
</html>";
        }

        private static string Sheet(LaborPermitCard card)
        {
            var permit = card.PermitNo.Trim();
            var qr = QrDataUrl(LaborPermitNumber.QrText(permit));
            var year = permit.Length >= 2 && int.TryParse(permit.AsSpan(0, 2), out var yy)
                ? (2000 + yy).ToString()
                : DateTime.Now.Year.ToString();
            return $@"<div class='sheet'>
  <div class='card'>
    <img class='logo' src='{LiscoLogoData.DataUrl}' alt='' />
    <div class='org'>الشركة الليبية للحديد والصلب<br>إدارة الشؤون الأمنية</div>
    <img class='qr' src='{qr}' alt='' />
    <div class='info'>
      <div class='title'>تصريح عمالة</div>
      <div class='row'><span class='label'>الاسم:</span><span class='value'>{Html(card.FullName)}</span></div>
      <div class='row'><span class='label'>الشركة:</span><span class='value'>{Html(card.CompanyName)}</span></div>
      <div class='row'><span class='label'>الرقم:</span><span class='value permit'>{Html(permit)}</span></div>
      <div class='year'>ساري لسنة {year}</div>
    </div>
  </div>
</div>";
        }

        private static string QrDataUrl(string text)
        {
            using var generator = new QRCodeGenerator();
            var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
            var png = new PngByteQRCode(data);
            var bytes = png.GetGraphic(8);
            return "data:image/png;base64," + Convert.ToBase64String(bytes);
        }

        private static string Html(string? value) => WebUtility.HtmlEncode(value ?? "");
    }
}
