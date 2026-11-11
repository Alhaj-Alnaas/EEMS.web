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

/**
 * تنبيهات النظام (قارئة، تصريح). تظهر من أيقونة الجرس ثم تختفي وحدها.
 * رسائل القرار تبقى في pmsMessage.
 */
window.pmsNotice = (function () {
    const DURATION = 4500;
    const MAX = 3;

    function host() {
        let el = document.getElementById('pms-notice-host');
        if (!el) {
            el = document.createElement('div');
            el.id = 'pms-notice-host';
            el.setAttribute('dir', 'rtl');
            document.body.appendChild(el);
        }
        place(el);
        return el;
    }

    function place(el) {
        const btn = document.querySelector('.pms-notify-btn');
        if (!btn) {
            el.style.top = '68px';
            el.style.right = '16px';
            return;
        }
        const box = btn.getBoundingClientRect();
        el.style.top = Math.round(box.bottom + 8) + 'px';
        el.style.right = Math.max(8, Math.round(window.innerWidth - box.right)) + 'px';
    }

    function kindOf(type, title) {
        const t = (type || '').toString().toLowerCase();
        if (t === 'danger' || t === 'warning' || t === 'success' || t === 'info') return t;
        const text = title || '';
        if (text.indexOf('عادت') >= 0 || text.indexOf('اعتماد') >= 0) return 'success';
        if (text.indexOf('توقف') >= 0 || text.indexOf('معط') >= 0 || text.indexOf('رفض') >= 0) return 'danger';
        if (text.indexOf('إرجاع') >= 0 || text.indexOf('إغلاق') >= 0 || text.indexOf('إلغاء') >= 0) return 'warning';
        return 'info';
    }

    function iconOf(kind) {
        if (kind === 'success') return 'bi-wifi';
        if (kind === 'danger') return 'bi-wifi-off';
        if (kind === 'warning') return 'bi-exclamation-circle-fill';
        return 'bi-bell-fill';
    }

    function show(options) {
        const opts = typeof options === 'string' ? { text: options } : (options || {});
        const title = (opts.title == null ? '' : String(opts.title)).trim();
        let body = (opts.body == null ? (opts.text == null ? '' : String(opts.text)) : String(opts.body)).trim();
        if (!title && !body) return;
        if (body === title) body = '';

        const kind = kindOf(opts.type, title);
        const root = host();
        const card = document.createElement('article');
        card.className = 'pms-notice pms-notice-' + kind;
        card.setAttribute('role', 'status');

        const icon = document.createElement('i');
        icon.className = 'bi ' + iconOf(kind) + ' pms-notice-icon';
        icon.setAttribute('aria-hidden', 'true');

        const heading = document.createElement('div');
        heading.className = 'pms-notice-title';
        heading.textContent = title || 'تنبيه';

        const close = document.createElement('button');
        close.type = 'button';
        close.className = 'pms-notice-x';
        close.setAttribute('aria-label', 'إغلاق');
        close.textContent = '×';

        card.appendChild(icon);
        card.appendChild(heading);
        card.appendChild(close);
        if (body) {
            const text = document.createElement('div');
            text.className = 'pms-notice-body';
            text.textContent = body;
            card.appendChild(text);
        }

        root.prepend(card);
        while (root.children.length > MAX) root.removeChild(root.lastElementChild);

        const bell = document.querySelector('.pms-notify-btn');
        if (bell) {
            bell.classList.remove('pms-notify-pulse');
            void bell.offsetWidth;
            bell.classList.add('pms-notify-pulse');
        }

        let timer = null;
        let gone = false;
        const hide = function () {
            if (gone) return;
            gone = true;
            if (timer) clearTimeout(timer);
            card.classList.add('pms-notice-hide');
            setTimeout(function () {
                if (card.parentNode) card.remove();
            }, 220);
        };
        const arm = function (ms) {
            if (timer) clearTimeout(timer);
            timer = setTimeout(hide, ms);
        };
        close.addEventListener('click', function (event) {
            event.stopPropagation();
            hide();
        });
        card.addEventListener('mouseenter', function () {
            if (timer) clearTimeout(timer);
        });
        card.addEventListener('mouseleave', function () { arm(1400); });
        const ms = typeof opts.duration === 'number' ? opts.duration : DURATION;
        if (ms > 0) arm(ms);
        requestAnimationFrame(function () { card.classList.add('pms-notice-show'); });
    }

    window.addEventListener('resize', function () {
        const el = document.getElementById('pms-notice-host');
        if (el) place(el);
    });
    window.addEventListener('scroll', function () {
        const el = document.getElementById('pms-notice-host');
        if (el && el.childElementCount) place(el);
    }, true);

    return {
        show: show,
        clear: function () {
            const el = document.getElementById('pms-notice-host');
            if (el) el.replaceChildren();
        }
    };
})();
