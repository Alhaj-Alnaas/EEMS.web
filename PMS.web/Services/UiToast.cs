using Microsoft.JSInterop;

namespace PMS.web.Notifications
{
    /// <summary>Thin wrapper over window.pmsToast for Blazor pages.</summary>
    public interface IUiToast
    {
        Task SuccessAsync(string message, int? durationMs = null);
        Task ErrorAsync(string message, int? durationMs = null);
        Task WarningAsync(string message, int? durationMs = null);
        Task InfoAsync(string message, int? durationMs = null);
        Task ShowAsync(string message, string type, int? durationMs = null);
        Task ClearAsync();
    }

    public sealed class UiToast : IUiToast
    {
        private readonly IJSRuntime _js;

        public UiToast(IJSRuntime js) => _js = js;

        public Task SuccessAsync(string message, int? durationMs = null)
            => ShowAsync(message, "success", durationMs);

        public Task ErrorAsync(string message, int? durationMs = null)
            => ShowAsync(message, "danger", durationMs);

        public Task WarningAsync(string message, int? durationMs = null)
            => ShowAsync(message, "warning", durationMs);

        public Task InfoAsync(string message, int? durationMs = null)
            => ShowAsync(message, "info", durationMs);

        public async Task ShowAsync(string message, string type, int? durationMs = null)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            try
            {
                if (durationMs is int ms)
                    await _js.InvokeVoidAsync("pmsToast.show", message.Trim(), type, ms);
                else
                    await _js.InvokeVoidAsync("pmsToast.show", message.Trim(), type);
            }
            catch (JSDisconnectedException) { }
            catch (JSException) { }
            catch (InvalidOperationException) { }
        }

        public async Task ClearAsync()
        {
            try { await _js.InvokeVoidAsync("pmsToast.clear"); }
            catch { /* ignore */ }
        }
    }
}
