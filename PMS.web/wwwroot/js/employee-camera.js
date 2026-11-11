window.employeeCamera = (function () {
    let stream = null;
    let session = 0;

    function releaseStream() {
        if (!stream) return;
        stream.getTracks().forEach(function (track) { track.stop(); });
        stream = null;
    }

    function cameraRank(label) {
        const s = (label || '').toLowerCase();
        if (!s) return 2;
        if (/infrared|\bir camera\b|depth|windows hello|virtual/.test(s)) return 0;
        if (/integrated|built-in|built in|facetime|truevision|easy camera|hp hd|internal/.test(s)) return 1;
        if (/usb|webcam|external|logitech|lifecam|a4tech|genius|uvc|c920|c270/.test(s)) return 4;
        return 3;
    }

    async function openCamera(deviceId) {
        const video = deviceId
            ? { deviceId: { exact: deviceId }, width: { ideal: 640 }, height: { ideal: 480 } }
            : { width: { ideal: 640 }, height: { ideal: 480 } };
        return navigator.mediaDevices.getUserMedia({ video: video, audio: false });
    }

    function withTimeout(promise, ms, message) {
        return new Promise(function (resolve, reject) {
            const timer = setTimeout(function () { reject(new Error(message)); }, ms);
            promise.then(function (value) {
                clearTimeout(timer);
                resolve(value);
            }, function (error) {
                clearTimeout(timer);
                reject(error);
            });
        });
    }

    async function start(previewElementId) {
        releaseStream();
        const token = ++session;
        const preview = document.getElementById(previewElementId);
        if (!preview) throw new Error('تعذر تجهيز مكان معاينة الكاميرا');

        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
            throw new Error('المتصفح لا يدعم الكاميرا');
        }

        const timeoutMessage = 'لم تُستجب الكاميرا. اسمح للمتصفح باستخدامها ثم أعد المحاولة.';
        stream = await withTimeout(openCamera(null), 12000, timeoutMessage);
        if (token !== session) { releaseStream(); return false; }
        try {
            const devices = await navigator.mediaDevices.enumerateDevices();
            const cams = devices.filter(function (d) { return d.kind === 'videoinput' && d.deviceId; });
            let best = null;
            cams.forEach(function (cam) {
                if (!best || cameraRank(cam.label) >= cameraRank(best.label)) best = cam;
            });
            const current = stream.getVideoTracks()[0] && stream.getVideoTracks()[0].getSettings().deviceId;
            if (best && best.deviceId && best.deviceId !== current && cameraRank(best.label) > 1) {
                const next = await withTimeout(openCamera(best.deviceId), 8000, timeoutMessage);
                if (token !== session) {
                    next.getTracks().forEach(function (track) { track.stop(); });
                    releaseStream();
                    return false;
                }
                stream.getTracks().forEach(function (track) { track.stop(); });
                stream = next;
            }
        } catch (_) { /* الإبقاء على الكاميرا التي فُتحت أولاً */ }

        if (token !== session) { releaseStream(); return false; }
        preview.muted = true;
        preview.srcObject = stream;
        const playing = preview.play();
        if (playing && playing.catch) playing.catch(function () { });
        return true;
    }

    function capture(previewElementId, quality) {
        const preview = document.getElementById(previewElementId);
        if (!preview || !preview.videoWidth) throw new Error('الكاميرا غير جاهزة لالتقاط الصورة');

        const canvas = document.createElement('canvas');
        canvas.width = preview.videoWidth;
        canvas.height = preview.videoHeight;
        const ctx = canvas.getContext('2d');
        ctx.drawImage(preview, 0, 0);
        const q = typeof quality === 'number' ? quality : 0.85;
        const dataUrl = canvas.toDataURL('image/jpeg', q);
        return dataUrl;
    }

    function stop() {
        session++;
        releaseStream();
    }

    function printHtml(html) {
        const w = window.open('', '_blank', 'width=920,height=640');
        if (!w) throw new Error('تم حظر نافذة الطباعة — اسمح بالنوافذ المنبثقة');
        w.document.open();
        w.document.write(html);
        w.document.close();
        w.focus();

        function doPrint() {
            try { w.print(); } catch (_) { /* ignore */ }
        }

        const imgs = Array.from(w.document.images || []);
        if (imgs.length === 0) {
            setTimeout(doPrint, 250);
            return;
        }

        let pending = imgs.length;
        let done = false;
        const finish = function () {
            if (done) return;
            done = true;
            setTimeout(doPrint, 150);
        };

        imgs.forEach(function (img) {
            if (img.complete) {
                pending -= 1;
                if (pending <= 0) finish();
                return;
            }
            img.onload = img.onerror = function () {
                pending -= 1;
                if (pending <= 0) finish();
            };
        });

        // مهلة أمان حتى لو تعذّر تحميل صورة
        setTimeout(finish, 4000);
    }

    return { start, capture, stop, printHtml };
})();
