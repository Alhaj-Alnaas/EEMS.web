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

    function explainCameraError(err) {
        const name = (err && err.name) || '';
        const msg = (err && err.message) || String(err || '');
        if (!window.isSecureContext || /only secure origins|https/i.test(msg))
            return 'الكاميرا تعمل فقط عبر https أو عبر localhost. افتح الصفحة بأحدهما ثم أعد المحاولة.';
        if (name === 'NotAllowedError' || /not allowed|permission|denied/i.test(msg))
            return 'رفض المتصفح الكاميرا. اسمح بها من أيقونة القفل بجانب العنوان، وتأكد أن ويندوز يسمح للمتصفح بالكاميرا، ثم أعد المحاولة.';
        if (name === 'NotFoundError' || /notfound/i.test(msg))
            return 'لم يُعثر على كاميرا متصلة بالجهاز.';
        if (name === 'NotReadableError' || /notreadable|trackstart/i.test(msg))
            return 'الكاميرا مستخدمة في برنامج آخر. أغلقه ثم أعد المحاولة.';
        return msg || 'تعذر تشغيل الكاميرا.';
    }

    function hideStill(preview) {
        if (!preview || !preview.parentElement) return;
        const img = preview.parentElement.querySelector('img');
        if (img) img.hidden = true;
    }

    function showStill(previewId) {
        const preview = document.getElementById(previewId);
        markLive(previewId, false);
        if (!preview) return;
        preview.classList.remove('is-live');
        if (!preview.parentElement) return;
        const img = preview.parentElement.querySelector('img');
        if (img) img.hidden = false;
    }

    function markLive(previewId, on) {
        const preview = document.getElementById(previewId);
        const panel = preview && preview.closest('.card-req-photo-panel, .emp-reg-photo-panel');
        if (panel) panel.classList.toggle('is-live', !!on);
    }

    async function start(previewElementId) {
        releaseStream();
        const token = ++session;
        const preview = document.getElementById(previewElementId);
        if (!preview) throw new Error('تعذر تجهيز مكان معاينة الكاميرا');

        if (!window.isSecureContext)
            throw new Error('الكاميرا تعمل فقط عبر https أو عبر localhost. افتح الصفحة بأحدهما ثم أعد المحاولة.');

        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
            throw new Error('المتصفح لا يدعم الكاميرا. استخدم Chrome أو Edge عبر https أو localhost.');
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
        const live = document.getElementById(previewElementId);
        if (!live) { releaseStream(); throw new Error('تعذر تجهيز مكان معاينة الكاميرا'); }
        live.classList.add('is-live');
        hideStill(live);
        markLive(previewElementId, true);
        live.muted = true;
        live.srcObject = stream;
        const playing = live.play();
        if (playing && playing.catch) playing.catch(function () { });
        return true;
    }

    function attach(previewElementId) {
        const live = document.getElementById(previewElementId);
        if (!live || !stream) return false;
        live.classList.add('is-live');
        hideStill(live);
        live.muted = true;
        if (live.srcObject !== stream)
            live.srcObject = stream;
        const playing = live.play();
        if (playing && playing.catch) playing.catch(function () { });
        return true;
    }

    function bindStart(buttonId, previewId, dotNetRef) {
        const button = document.getElementById(buttonId);
        if (!button || button.dataset.camBound === '1' || !dotNetRef) return;
        button.dataset.camBound = '1';
        button.addEventListener('click', function () {
            const preview = document.getElementById(previewId);
            if (preview) {
                preview.classList.add('is-live');
                hideStill(preview);
            }
            start(previewId).then(function (ok) {
                if (!ok) {
                    showStill(previewId);
                    return;
                }
                return dotNetRef.invokeMethodAsync('OnCameraReady');
            }).catch(function (err) {
                showStill(previewId);
                const message = explainCameraError(err);
                return dotNetRef.invokeMethodAsync('OnCameraFailed', message).catch(function () { });
            });
        });
    }

    function bindCapture(buttonId, previewId, dotNetRef) {
        const button = document.getElementById(buttonId);
        if (!button || button.dataset.capBound === '1' || !dotNetRef) return;
        button.dataset.capBound = '1';
        button.addEventListener('click', function () {
            if (button.disabled) return;
            let dataUrl;
            try {
                dataUrl = capture(previewId, 0.85);
            } catch (err) {
                dotNetRef.invokeMethodAsync('OnPhotoCaptureFailed', explainCameraError(err)).catch(function () { });
                return;
            }
            button.disabled = true;
            dotNetRef.invokeMethodAsync('OnPhotoCaptured', dataUrl).catch(function () {
                button.disabled = false;
            });
        });
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
        document.querySelectorAll('.card-req-photo-panel.is-live, .emp-reg-photo-panel.is-live').forEach(function (panel) {
            panel.classList.remove('is-live');
        });
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

    return { start, capture, stop, printHtml, bindStart, bindCapture, attach };
})();
