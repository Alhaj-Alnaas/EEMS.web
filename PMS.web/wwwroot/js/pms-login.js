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

    function bind() {
        const input = document.getElementById(userId);
        if (!input || input.dataset.pmsBound === '1') return;
        input.dataset.pmsBound = '1';
        input.addEventListener('blur', checkUsername);
        input.addEventListener('keydown', function (e) {
            if (e.key === 'Enter') {
                e.preventDefault();
                checkUsername();
            }
        });
        if ((input.value || '').trim()) checkUsername();
    }

    if (document.readyState === 'loading')
        document.addEventListener('DOMContentLoaded', bind);
    else
        bind();

    return { checkUsername, bind };
})();
