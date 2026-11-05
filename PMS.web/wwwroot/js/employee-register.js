window.pmsEmpReg = (function () {
    let dotNetRef = null;

    function notify(text, ok) {
        if (window.pmsToast && typeof window.pmsToast.show === 'function') {
            window.pmsToast.show(text, ok ? 'success' : 'danger');
            return;
        }
        // احتياطي نادر
        window.alert(text);
    }

    function clearAlert() {
        if (window.pmsToast && typeof window.pmsToast.clear === 'function') {
            window.pmsToast.clear();
        }
    }

    function setVal(id, value) {
        const el = document.getElementById(id);
        if (el) el.value = value == null ? '' : String(value);
    }

    function getVal(id) {
        const el = document.getElementById(id);
        return el ? (el.value || '').trim() : '';
    }

    function setBusy(busy) {
        const btn = document.getElementById('regShowBtn');
        // Blazor owns spinner/disabled via VerifyAndShowAsync — don't fight the DOM
        if (!btn || btn.getAttribute('data-blazor-managed') === '1') return;
        btn.disabled = !!busy;
        btn.innerHTML = busy
            ? '<span class="spinner-border spinner-border-sm"></span>'
            : '<i class="bi bi-search"></i> عرض';
    }

    function attach(dotNet) {
        dotNetRef = dotNet;
    }

    function detach() {
        dotNetRef = null;
    }

    async function show() {
        clearAlert();
        const empNo = getVal('regEmpFileNo');
        if (!empNo) {
            notify('أدخل الرقم الوظيفي أولاً.', false);
            return;
        }

        setBusy(true);
        try {
            const url = '/api/hr/employee/' + encodeURIComponent(empNo);
            const response = await fetch(url, {
                method: 'GET',
                headers: { 'Accept': 'application/json' },
                cache: 'no-store'
            });

            if (!response.ok) {
                notify('تعذر الاتصال بخدمة عرض الموظف (HTTP ' + response.status + ').', false);
                return;
            }

            const data = await response.json();
            if (data.error) {
                setVal('regEmpName', '');
                setVal('regJobTypeName', '');
                setVal('regDepartment', '');
                setVal('regJobStatus', '');
                setVal('regEmpPhone', '');
                setVal('regEmpEmail', '');
                setVal('regAddress', '');
                setVal('regNationality', '');
                setVal('regBloodType', '');
                notify(data.error, false);
                if (dotNetRef) {
                    // بدون رسالة إضافية من Blazor — التنبيه عُرض مرة واحدة أعلاه
                    await dotNetRef.invokeMethodAsync('OnHrFailed', data.error || '');
                }
                return;
            }

            setVal('regEmpFileNo', data.empFileNo || empNo);
            setVal('regEmpName', data.empName || '');
            setVal('regJobTypeName', data.jobtypeName || '');
            setVal('regDepartment', data.perRespCodeNoName || '');
            setVal('regJobStatus', data.jobStatus || '');
            setVal('regEmpPhone', data.empPhone || '');
            setVal('regEmpEmail', data.empEmail || '');

            const address = data.empAddress || data.address || '';
            const nationality = data.nationality || data.empNationality || '';
            const bloodType = data.bloodType || data.bloodGroup || '';
            if (address) setVal('regAddress', address);
            if (nationality) setVal('regNationality', nationality);
            if (bloodType) setVal('regBloodType', bloodType);

            if (dotNetRef) {
                await dotNetRef.invokeMethodAsync('OnHrLoaded', JSON.stringify(data));
            }
            // تنبيه نجاح مختصر مرة واحدة فقط (بعد مزامنة Blazor)
            const name = data.empName || empNo;
            notify('تم عرض بيانات الموظف: ' + name, true);
        } catch (err) {
            const text = 'فشل عرض بيانات الموظف: ' + (err && err.message ? err.message : err);
            notify(text, false);
            if (dotNetRef) {
                try { await dotNetRef.invokeMethodAsync('OnHrFailed', ''); } catch (_) { /* ignore */ }
            }
        } finally {
            setBusy(false);
        }
    }

    function onFileKey(e) {
        if (e.key === 'Enter') {
            e.preventDefault();
            // Prefer Blazor VerifyAndShowAsync (credential gate) via button click
            const btn = document.getElementById('regShowBtn');
            if (btn && !btn.disabled) {
                btn.click();
                return;
            }
            show();
        }
    }

    return { attach, detach, show, onFileKey, getVal, setVal, notify };
})();
