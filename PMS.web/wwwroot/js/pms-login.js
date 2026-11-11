window.pmsLogin = (function () {
    const gateBlockId = 'loginGateBlock';
    const gateSelectId = 'loginSelectedGate';
    const infoId = 'loginGateInfo';
    const userId = 'loginUsername';

    async function checkUsername() {
        const input = document.getElementById(userId);
        const block = document.getElementById(gateBlockId);
        const select = document.getElementById(gateSelectId);
        const info = document.getElementById(infoId);
        if (!input || !block || !select) return;

        const username = (input.value || '').trim();
        if (!username) {
            block.classList.add('d-none');
            select.innerHTML = '<option value="">-- اختر البوابة --</option>';
            if (info) info.classList.add('d-none');
            return;
        }

        try {
            const res = await fetch('/api/auth/gate-required?username=' + encodeURIComponent(username), {
                headers: { 'Accept': 'application/json' },
                cache: 'no-store'
            });
            if (!res.ok) return;
            const data = await res.json();
            if (!data.required) {
                block.classList.add('d-none');
                select.innerHTML = '<option value="">-- اختر البوابة --</option>';
                if (info) info.classList.add('d-none');
                return;
            }

            let html = '<option value="">-- اختر البوابة --</option>';
            (data.gates || []).forEach(function (g) {
                html += '<option value="' + g.id + '">' + (g.text || g.id) + '</option>';
            });
            select.innerHTML = html;
            block.classList.remove('d-none');
            if (info) info.classList.remove('d-none');
        } catch (_) {
            /* ignore network errors on probe */
        }
    }

    let passwordVisible = false;
    let remember = false;
    const forgotText = 'نأمل التواصل مع مسؤول النظام في قسم النظم والبرمجة لتصفير كلمة المرور';

    function applyPassword() {
        const input = document.getElementById('loginPassword');
        const icon = document.querySelector('#loginEye i');
        if (input) input.type = passwordVisible ? 'text' : 'password';
        if (icon) icon.className = passwordVisible ? 'bi bi-eye-slash' : 'bi bi-eye';
    }

    function applyRemember() {
        const box = document.getElementById('loginRemember');
        if (box) box.checked = remember;
    }

    function showForgot() {
        if (window.pmsMessage) {
            window.pmsMessage.notify({ text: forgotText, type: 'info', duration: 0 });
            return;
        }
        window.alert(forgotText);
    }

    function focusFileNumber() {
        const input = document.getElementById(userId);
        if (!input || !document.querySelector('.pms-login-card')) return;
        try { input.focus(); } catch (_) { }
    }

    function bind() {
        const input = document.getElementById(userId);
        if (input && input.dataset.pmsBound !== '1') {
            input.dataset.pmsBound = '1';
            input.addEventListener('blur', checkUsername);
            input.addEventListener('input', function () {
                const cleaned = (input.value || '').replace(/\D/g, '');
                if (cleaned !== input.value) input.value = cleaned;
            });
            if ((input.value || '').trim()) checkUsername();
        }
        applyPassword();
        applyRemember();
        placeLogo();
        focusFileNumber();
        const pass = document.getElementById('loginPassword');
        if (pass && pass.dataset.pmsSelect !== '1') {
            pass.dataset.pmsSelect = '1';
            pass.addEventListener('focus', selectPassword);
        }
    }

    function placeLogo() {
        if (!document.querySelector('.pms-login-card') || document.querySelector('.pms-login-logo')) return;
        const img = document.createElement('img');
        img.className = 'pms-login-logo';
        img.src = '/images/lisco-logo.png';
        img.alt = 'شعار الشركة الليبية للحديد والصلب';
        document.body.appendChild(img);
    }

    async function advance(from) {
        if (!from) return;
        if (from.id === userId) await checkUsername();

        const pass = document.getElementById('loginPassword');
        const gateBlock = document.getElementById(gateBlockId);
        const gate = document.getElementById(gateSelectId);
        const gateOpen = gateBlock && !gateBlock.classList.contains('d-none');

        if (from.id === userId && gateOpen && gate) {
            gate.focus();
            return;
        }
        if (pass) {
            pass.focus();
            selectPassword();
        }
    }

    function selectPassword() {
        const pass = document.getElementById('loginPassword');
        if (!pass || !pass.value) return;
        const mark = function () {
            try {
                if (document.activeElement !== pass) pass.focus();
                pass.select();
                pass.setSelectionRange(0, pass.value.length);
            } catch (_) { }
        };
        mark();
        setTimeout(mark, 0);
    }

    function onLoginEnter(e) {
        if (e.key !== 'Enter' || e.shiftKey || e.isComposing) return;
        const el = e.target;
        if (!el || !el.closest || !el.closest('.pms-login-card')) return;
        if (el.id !== userId && el.id !== gateSelectId) return;
        e.preventDefault();
        e.stopPropagation();
        advance(el);
    }

    document.addEventListener('keydown', onLoginEnter, true);
    document.addEventListener('keypress', onLoginEnter, true);

    document.addEventListener('click', function (e) {
        const eye = e.target.closest && e.target.closest('#loginEye');
        if (eye) {
            e.preventDefault();
            e.stopPropagation();
            passwordVisible = !passwordVisible;
            applyPassword();
            return;
        }
        const forgot = e.target.closest && e.target.closest('#loginForgot');
        if (forgot) {
            e.preventDefault();
            e.stopPropagation();
            showForgot();
        }
    }, true);

    document.addEventListener('change', function (e) {
        if (e.target && e.target.id === 'loginRemember')
            remember = !!e.target.checked;
    }, true);

    if (document.readyState === 'loading')
        document.addEventListener('DOMContentLoaded', bind);
    else
        bind();

    return {
        checkUsername: checkUsername,
        bind: bind,
        apply: bind,
        rememberChecked: function () {
            const box = document.getElementById('loginRemember');
            return box ? !!box.checked : remember;
        }
    };
})();
