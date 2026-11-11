using Microsoft.AspNetCore.Mvc;
using PMS.web.Reporting;

namespace PMS.web.Controllers
{
    [Route("reports/print")]
    public class ReportPrintController : Controller
    {
        private readonly IReportFileCache _files;

        public ReportPrintController(IReportFileCache files) => _files = files;

        [HttpGet("{id}")]
        public IActionResult Open(string id)
        {
            if (!_files.TryGet(id, out var bytes))
                return NotFound();

            return File(bytes, "application/pdf");
        }
    }
}
