window.pmsDownload = {
    save: function (name, type, base64) {
        const url = blobUrl(type, base64);
        const link = document.createElement("a");
        link.href = url;
        link.download = name;
        document.body.appendChild(link);
        link.click();
        link.remove();
        setTimeout(function () { URL.revokeObjectURL(url); }, 1500);
    },
    openUrl: function (url) {
        window.open(url, "_blank");
    }
};

function blobUrl(type, base64) {
    const raw = atob(base64);
    const bytes = new Uint8Array(raw.length);
    for (let i = 0; i < raw.length; i++)
        bytes[i] = raw.charCodeAt(i);
    return URL.createObjectURL(new Blob([bytes], { type: type }));
}
