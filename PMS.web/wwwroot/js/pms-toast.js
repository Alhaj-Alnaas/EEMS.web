/**
 * توافق مع الاستدعاءات السابقة. التنفيذ الفعلي في pms-message.js.
 */
window.pmsToast = {
    show: function (text, type, durationMs) {
        if (!window.pmsMessage) return;
        const options = { text: text, type: type };
        if (typeof durationMs === 'number') options.duration = durationMs;
        return window.pmsMessage.notify(options);
    },
    clear: function () {
        if (window.pmsMessage) window.pmsMessage.clear();
    }
};
