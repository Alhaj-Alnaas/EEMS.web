/**
 * PMS global toast notifications — single host, auto-dismiss, user can close.
 * Dedupes identical messages shown within a short window.
 */
window.pmsToast = (function () {
    const DEDUPE_MS = 2500;
    let host = null;
    let lastKey = '';
    let lastAt = 0;

    function ensureHost() {
        if (host && document.body.contains(host)) return host;
        host = document.getElementById('pms-toast-host');
        if (!host) {
            host = document.createElement('div');
            host.id = 'pms-toast-host';
            host.className = 'pms-toast-host';
            host.setAttribute('aria-live', 'polite');
            host.setAttribute('aria-relevant', 'additions');
            document.body.appendChild(host);
        }
        return host;
    }

    function normalizeType(type) {
        const t = (type || 'info').toString().toLowerCase();
        if (t === 'ok' || t === 'success') return 'success';
        if (t === 'error' || t === 'danger' || t === 'fail') return 'danger';
        if (t === 'warn' || t === 'warning') return 'warning';
        return 'info';
    }

    function defaultDuration(type) {
        if (type === 'danger') return 4500;
        if (type === 'warning') return 4000;
        if (type === 'success') return 2800;
        return 3200;
    }

    /**
     * @param {string} text
     * @param {string} [type] success|danger|warning|info
     * @param {number} [durationMs] 0 = no auto-hide
     */
    function show(text, type, durationMs) {
        const msg = (text == null ? '' : String(text)).trim();
        if (!msg) return;

        const kind = normalizeType(type);
        const now = Date.now();
        const key = kind + '|' + msg;
        if (key === lastKey && (now - lastAt) < DEDUPE_MS) {
            return; // نفس الرسالة ظهرت للتو
        }
        lastKey = key;
        lastAt = now;

        const root = ensureHost();
        // رسالة واحدة ظاهرة في كل مرة (تجنب التراكم المزعج)
        root.innerHTML = '';

        const el = document.createElement('div');
        el.className = 'pms-toast pms-toast-' + kind;
        el.setAttribute('role', 'alert');

        const icon = document.createElement('i');
        icon.className = 'bi ' + (
            kind === 'success' ? 'bi-check-circle-fill' :
            kind === 'danger' ? 'bi-exclamation-triangle-fill' :
            kind === 'warning' ? 'bi-exclamation-circle-fill' :
            'bi-info-circle-fill'
        );
        icon.setAttribute('aria-hidden', 'true');

        const body = document.createElement('div');
        body.className = 'pms-toast-body';
        body.textContent = msg;

        const close = document.createElement('button');
        close.type = 'button';
        close.className = 'pms-toast-close';
        close.setAttribute('aria-label', 'إغلاق');
        close.innerHTML = '&times;';

        let timer = null;
        const dismiss = function () {
            if (timer) { clearTimeout(timer); timer = null; }
            el.classList.add('pms-toast-hide');
            setTimeout(function () {
                if (el.parentNode) el.parentNode.removeChild(el);
            }, 220);
        };

        close.addEventListener('click', dismiss);
        el.appendChild(icon);
        el.appendChild(body);
        el.appendChild(close);
        root.appendChild(el);

        requestAnimationFrame(function () {
            el.classList.add('pms-toast-show');
        });

        const ms = typeof durationMs === 'number' ? durationMs : defaultDuration(kind);
        if (ms > 0) {
            timer = setTimeout(dismiss, ms);
        }
    }

    function clear() {
        const root = document.getElementById('pms-toast-host');
        if (root) root.innerHTML = '';
        lastKey = '';
        lastAt = 0;
    }

    return { show: show, clear: clear };
})();
