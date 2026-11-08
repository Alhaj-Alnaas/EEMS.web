/**
 * رسائل النظام الموحدة. تظهر في وسط الشاشة.
 * الإشعار يختفي بعد مدة أو بزر الإغلاق.
 * رسالة القرار تبقى حتى نعم / لا أو الإغلاق.
 */
window.pmsMessage = (function () {
    const DURATION = 4000;
    let lastKey = '';
    let lastAt = 0;

    function kindOf(type) {
        const t = (type || 'info').toString().toLowerCase();
        if (t === 'ok' || t === 'success') return 'success';
        if (t === 'error' || t === 'danger' || t === 'fail') return 'danger';
        if (t === 'warn' || t === 'warning') return 'warning';
        if (t === 'confirm' || t === 'question') return 'confirm';
        return 'info';
    }

    function iconOf(kind) {
        if (kind === 'success') return 'bi-check-circle-fill';
        if (kind === 'danger') return 'bi-x-circle-fill';
        if (kind === 'warning') return 'bi-exclamation-circle-fill';
        if (kind === 'confirm') return 'bi-question-circle-fill';
        return 'bi-info-circle-fill';
    }

    function titleOf(kind) {
        if (kind === 'success') return 'تم';
        if (kind === 'danger') return 'خطأ';
        if (kind === 'warning') return 'تنبيه';
        if (kind === 'confirm') return 'تأكيد';
        return 'ملاحظة';
    }

    function read(options) {
        if (typeof options === 'string') return { text: options };
        return options || {};
    }

    function shell(text, kind, title) {
        const el = document.createElement('div');
        el.className = 'pms-msg';
        el.setAttribute('role', 'alertdialog');

        const close = document.createElement('button');
        close.type = 'button';
        close.className = 'pms-msg-x';
        close.setAttribute('aria-label', 'إغلاق');
        close.textContent = '×';

        const iconWrap = document.createElement('div');
        iconWrap.className = 'pms-msg-icon';
        const icon = document.createElement('i');
        icon.className = 'bi ' + iconOf(kind);
        icon.setAttribute('aria-hidden', 'true');
        iconWrap.appendChild(icon);

        const heading = document.createElement('div');
        heading.className = 'pms-msg-title';
        heading.textContent = title || titleOf(kind);

        const body = document.createElement('div');
        body.className = 'pms-msg-body';
        body.textContent = text;

        el.appendChild(close);
        el.appendChild(iconWrap);
        el.appendChild(heading);
        el.appendChild(body);
        return { el: el, close: close };
    }

    function button(label, yes) {
        const btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'pms-msg-btn' + (yes ? ' pms-msg-btn-yes' : '');
        btn.textContent = label;
        return btn;
    }

    function stage() {
        const backdrop = document.createElement('div');
        backdrop.className = 'pms-msg-backdrop';
        return backdrop;
    }

    function notify(options) {
        const opts = read(options);
        const text = (opts.text == null ? '' : String(opts.text)).trim();
        if (!text) return Promise.resolve();

        const kind = kindOf(opts.type);
        const now = Date.now();
        const key = kind + '|' + text;
        if (key === lastKey && (now - lastAt) < 2500) return Promise.resolve();
        lastKey = key;
        lastAt = now;

        const previous = document.getElementById('pms-msg-notice');
        if (previous) previous.remove();

        const backdrop = stage();
        backdrop.id = 'pms-msg-notice';
        const built = shell(text, kind, opts.title);
        backdrop.appendChild(built.el);
        document.body.appendChild(backdrop);

        return new Promise(function (resolve) {
            let timer = null;
            let settled = false;
            const finish = function () {
                if (settled) return;
                settled = true;
                if (timer) clearTimeout(timer);
                built.el.classList.add('pms-msg-hide');
                setTimeout(function () {
                    if (backdrop.parentNode) backdrop.remove();
                    resolve();
                }, 160);
            };
            built.close.addEventListener('click', finish);
            backdrop.addEventListener('click', function (event) {
                if (event.target === backdrop) finish();
            });
            const ms = typeof opts.duration === 'number' ? opts.duration : DURATION;
            if (ms > 0) timer = setTimeout(finish, ms);
            requestAnimationFrame(function () { built.el.classList.add('pms-msg-show'); });
        });
    }

    function openDialog(opts, withInput) {
        const text = (opts.text == null ? '' : String(opts.text)).trim();
        const kind = opts.type ? kindOf(opts.type) : 'confirm';
        const heading = opts.title || (withInput ? 'بيان مطلوب' : 'تأكيد');
        const backdrop = stage();
        const built = shell(text, kind, heading);

        let input = null;
        let error = null;
        if (withInput) {
            input = document.createElement('input');
            input.type = 'text';
            input.className = 'pms-msg-input';
            input.placeholder = opts.placeholder || '';
            error = document.createElement('div');
            error.className = 'pms-msg-error';
            error.hidden = true;
            built.el.appendChild(input);
            built.el.appendChild(error);
        }

        const actions = document.createElement('div');
        actions.className = 'pms-msg-actions';
        const yes = button(opts.yes || (withInput ? 'موافق' : 'نعم'), true);
        actions.appendChild(yes);
        let no = null;
        if (opts.no !== null) {
            no = button(opts.no || (withInput ? 'إلغاء' : 'لا'), false);
            actions.appendChild(no);
        }
        built.el.appendChild(actions);
        backdrop.appendChild(built.el);
        document.body.appendChild(backdrop);
        if (input) input.focus();
        else yes.focus();

        return new Promise(function (resolve) {
            const onKey = function (event) {
                if (event.key === 'Escape') cancel();
            };
            const close = function (value) {
                document.removeEventListener('keydown', onKey);
                backdrop.remove();
                resolve(value);
            };
            document.addEventListener('keydown', onKey);
            yes.addEventListener('click', function () {
                if (withInput) {
                    const value = input.value.trim();
                    if (opts.required !== false && !value) {
                        error.hidden = false;
                        error.textContent = opts.requiredText || 'يجب إدخال السبب';
                        input.focus();
                        return;
                    }
                    close(value);
                    return;
                }
                close(true);
            });
            const cancel = function () { close(withInput ? null : false); };
            if (no) no.addEventListener('click', cancel);
            built.close.addEventListener('click', cancel);
        });
    }

    function confirm(options) {
        return openDialog(read(options), false);
    }

    function prompt(options) {
        return openDialog(read(options), true);
    }

    function clear() {
        const notice = document.getElementById('pms-msg-notice');
        if (notice) notice.remove();
        lastKey = '';
        lastAt = 0;
    }

    function submitConfirm(event, text) {
        const form = event.target;
        if (form && form.dataset.pmsConfirmed === '1') return true;
        event.preventDefault();
        confirm({ text: text, type: 'confirm' }).then(function (ok) {
            if (!ok || !form) return;
            form.dataset.pmsConfirmed = '1';
            form.submit();
        });
        return false;
    }

    function installLegacy() {
        const fire = function (arg1, arg2, arg3) {
            const opts = (arg1 && typeof arg1 === 'object')
                ? arg1
                : { title: arg1, text: arg2, icon: arg3 };
            const type = opts.icon || 'info';
            if (opts.input) {
                return prompt({
                    title: opts.title || 'بيان مطلوب',
                    text: opts.text || opts.inputPlaceholder || '',
                    type: type,
                    placeholder: opts.inputPlaceholder,
                    yes: opts.confirmButtonText,
                    no: opts.cancelButtonText,
                    required: true
                }).then(function (value) {
                    return { isConfirmed: value != null, value: value || '' };
                });
            }
            if (opts.showCancelButton) {
                return confirm({
                    title: opts.title || 'تأكيد',
                    text: opts.text || opts.title || '',
                    type: 'confirm',
                    yes: opts.confirmButtonText || 'نعم',
                    no: opts.cancelButtonText || 'لا'
                }).then(function (ok) {
                    return { isConfirmed: !!ok };
                });
            }
            const text = [opts.title, opts.text].filter(Boolean).join('\n');
            return notify({ text: text, type: type }).then(function () {
                return { isConfirmed: true };
            });
        };
        window.Swal = window.Swal || {};
        window.Swal.fire = fire;
    }

    return {
        notify: notify,
        confirm: confirm,
        prompt: prompt,
        clear: clear,
        submitConfirm: submitConfirm,
        installLegacy: installLegacy
    };
})();
