window.employeeCamera = (function () {
    let stream = null;

    async function start(videoElementId) {
        stop();
        const video = document.getElementById(videoElementId);
        if (!video) throw new Error('عنصر الفيديو غير موجود');

        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
            throw new Error('المتصفح لا يدعم الكاميرا');
        }

        stream = await navigator.mediaDevices.getUserMedia({
            video: { facingMode: 'user', width: { ideal: 640 }, height: { ideal: 480 } },
            audio: false
        });
        video.srcObject = stream;
        await video.play();
        return true;
    }

    function capture(videoElementId, quality) {
        const video = document.getElementById(videoElementId);
        if (!video || !video.videoWidth) throw new Error('الكاميرا غير جاهزة');

        const canvas = document.createElement('canvas');
        canvas.width = video.videoWidth;
        canvas.height = video.videoHeight;
        const ctx = canvas.getContext('2d');
        ctx.drawImage(video, 0, 0);
        const q = typeof quality === 'number' ? quality : 0.85;
        const dataUrl = canvas.toDataURL('image/jpeg', q);
        return dataUrl;
    }

    function stop() {
        if (stream) {
            stream.getTracks().forEach(t => t.stop());
            stream = null;
        }
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
