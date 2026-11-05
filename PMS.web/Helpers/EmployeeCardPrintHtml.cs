using System.Net;

namespace PMS.web.Helpers
{
    /// <summary>Builds printable HTML for employee ID cards (landscape badge layout).</summary>
    public static class EmployeeCardPrintHtml
    {
        public static string Build(
            string fileNumber,
            string fullName,
            string? nationality,
            string? address,
            string? jobTitle,
            string? organization,
            string? photoSrc,
            string? logoSrc = null,
            string? bloodType = null)
        {
            var photoInner = string.IsNullOrWhiteSpace(photoSrc)
                ? "<div class='photo empty'>بدون صورة</div>"
                : $"<img class='photo' src='{EscapeAttr(photoSrc)}' alt='' />";

            var effectiveLogo = string.IsNullOrWhiteSpace(logoSrc) ? LiscoLogoData.DataUrl : logoSrc;
            var logoInner = $"<img class='logo' src='{EscapeAttr(effectiveLogo)}' alt='الشركة الليبية للحديد والصلب' />";

            var bloodHtml = string.IsNullOrWhiteSpace(bloodType)
                ? ""
                : $"<div class='blood'>{WebUtility.HtmlEncode(bloodType.Trim())}</div>";

            return $@"<!DOCTYPE html>
<html lang='ar' dir='rtl'>
<head>
<meta charset='utf-8' />
<title>كرت موظف — {WebUtility.HtmlEncode(fileNumber)}</title>
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
}}
.card {{
  width: 100%;
  height: 100%;
  border: 1.4px solid #222;
  outline: 0.6px solid #222;
  outline-offset: -2.2px;
  padding: 2.5mm 2.8mm;
  display: flex;
  flex-direction: row;
  direction: ltr;
  gap: 2.8mm;
  position: relative;
  overflow: hidden;
  background: #fff;
}}
.logo {{
  position: absolute;
  top: 1.8mm;
  right: 2.2mm;
  width: 18mm;
  height: auto;
  max-height: 10mm;
  object-fit: contain;
  z-index: 3;
}}
.photo-col {{
  width: 22mm;
  flex: 0 0 22mm;
  display: flex;
  flex-direction: column;
  align-items: stretch;
  justify-content: flex-start;
  margin-top: 1mm;
  z-index: 1;
}}
img.photo, .photo.empty {{
  width: 100%;
  height: 28mm;
  object-fit: cover;
  object-position: center top;
  border: 1px solid #333;
  background: #f3f3f3;
  display: block;
}}
.photo.empty {{
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 8px;
  color: #777;
}}
.blood {{
  margin-top: 1.2mm;
  text-align: center;
  font-weight: 800;
  font-size: 11px;
  color: #b71c1c;
}}
.info {{
  flex: 1;
  min-width: 0;
  direction: rtl;
  text-align: right;
  display: flex;
  flex-direction: column;
  justify-content: center;
  padding-top: 9mm;
  padding-right: 0.5mm;
  font-size: 10.5px;
  line-height: 1.4;
  z-index: 1;
}}
.row {{
  display: flex;
  flex-direction: row;
  flex-wrap: nowrap;
  align-items: baseline;
  gap: 1.5mm;
  margin-bottom: 1.2mm;
  white-space: nowrap;
  overflow: hidden;
}}
.label {{
  flex: 0 0 auto;
  color: #222;
  font-weight: 700;
  white-space: nowrap;
  font-size: 10.5px;
}}
.value {{
  flex: 1 1 auto;
  min-width: 0;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  font-size: 10.5px;
}}
.file-no {{
  color: #c2185b;
  font-weight: 800;
  font-size: 12px;
  letter-spacing: 0.2px;
}}
@media print {{
  html, body {{ width: 86mm; height: 54mm; }}
  .sheet {{ page-break-inside: avoid; }}
}}
</style>
</head>
<body>
<div class='sheet'>
  <div class='card'>
    {logoInner}
    <div class='photo-col'>{photoInner}{bloodHtml}</div>
    <div class='info'>
      <div class='row'><span class='label'>ملف:</span><span class='value file-no'>{WebUtility.HtmlEncode(fileNumber)}</span></div>
      <div class='row'><span class='label'>الاسم:</span><span class='value'>{WebUtility.HtmlEncode(fullName)}</span></div>
      <div class='row'><span class='label'>الجنسية:</span><span class='value'>{WebUtility.HtmlEncode(NullDash(nationality))}</span></div>
      <div class='row'><span class='label'>العنوان:</span><span class='value'>{WebUtility.HtmlEncode(NullDash(address))}</span></div>
      <div class='row'><span class='label'>الوظيفة:</span><span class='value'>{WebUtility.HtmlEncode(NullDash(jobTitle))}</span></div>
      <div class='row'><span class='label'>الجهة:</span><span class='value'>{WebUtility.HtmlEncode(NullDash(organization))}</span></div>
    </div>
  </div>
</div>
</body>
</html>";
        }

        private static string NullDash(string? value)
            => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

        private static string EscapeAttr(string value)
            => WebUtility.HtmlEncode(value);
    }
}
