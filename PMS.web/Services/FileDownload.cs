using Microsoft.JSInterop;

namespace PMS.web.Reporting
{
    public static class FileDownload
    {
        public static Task SaveAsync(IJSRuntime js, string fileName, byte[] bytes) =>
            js.InvokeVoidAsync(
                "pmsDownload.save",
                fileName,
                "application/vnd.ms-excel",
                Convert.ToBase64String(bytes)).AsTask();

        public static Task OpenPdfAsync(IJSRuntime js, string url) =>
            js.InvokeVoidAsync("pmsDownload.openUrl", url).AsTask();
    }
}
