// wwwroot/js/login.js
$(document).ready(function () {
    const loginForm = $("#loginForm");
    const usernameInput = $("#usernameInput");
    const gatesDropdown = $("#gatesDropdown");
    const passwordInput = $("#passwordInput");
    const loginButton = $("#loginButton");
    const loginAlert = $("#loginAlert");
    const gateContainer = $("#gateContainer");
    const passwordContainer = $("#passwordContainer");
    const loadingSpinner = $(".loading-spinner");

    // منع الفورم من الإرسال قبل ظهور كلمة المرور
    loginForm.on("submit", function (e) {
        e.preventDefault();

        if (!passwordContainer.is(":visible")) {
            usernameInput.focus();
            return false;
        }

        // إظهار مؤشر التحميل
        loadingSpinner.show();
        loginButton.prop("disabled", true);

        $.ajax({
            url: "/Login/Login",
            type: "POST",
            data: loginForm.serialize(),
            dataType: "json",
            success: function (res) {
                loadingSpinner.hide();
                loginButton.prop("disabled", false);

                if (res.success) {
                    // توجيه المستخدم إلى الصفحة الرئيسية
                    window.location.href = res.redirectUrl;
                } else {
                    showAlert(res.message, "danger");
                }
            },
            error: function (xhr, status, error) {
                loadingSpinner.hide();
                loginButton.prop("disabled", false);

                let errorMsg = "حدث خطأ في الاتصال بالخادم";
                if (xhr.responseJSON && xhr.responseJSON.message) {
                    errorMsg = xhr.responseJSON.message;
                }

                showAlert(errorMsg, "danger");
                console.error("Error:", error);
            }
        });
    });

    // عند الضغط على Enter في حقل رقم الملف
    usernameInput.on("keyup", function (e) {
        if (e.key === "Enter") {
            e.preventDefault();
            checkUsername();
        }
    });

    // عند تغيير قائمة البوابات
    gatesDropdown.on("change", function () {
        if ($(this).val()) {
            passwordInput.focus();
        }
    });

    // دالة للتحقق من اسم المستخدم
    function checkUsername() {
        const username = usernameInput.val().trim();

        if (!username) {
            showAlert("الرجاء إدخال رقم الملف", "danger");
            return;
        }

        // إظهار مؤشر التحميل
        loadingSpinner.show();
        loginButton.prop("disabled", true);

        $.ajax({
            url: "/Login/CheckUser",
            type: "POST",
            data: { username: username },
            dataType: "json",
            success: function (res) {
                // إخفاء مؤشر التحميل
                loadingSpinner.hide();
                loginButton.prop("disabled", false);

                if (!res.success) {
                    showAlert(res.message || "حدث خطأ أثناء التحقق من المستخدم", "danger");
                    return;
                }

                // عرض رسالة ترحيب تحتوي على اسم المستخدم
                showAlert(`مرحباً ${res.userName}، يرجى إدخال كلمة المرور`, "success");

                // إظهار حقل كلمة المرور
                passwordContainer.removeClass("hidden");

                // التعامل مع البوابات إذا كان المستخدم من الأمن
                if (res.isSafty && res.gates && res.gates.length > 0) {
                    // إفراغ القائمة الحالية
                    gatesDropdown.empty();

                    // إضافة الخيار الافتراضي
                    gatesDropdown.append('<option value="" disabled selected>-- اختر البوابة --</option>');

                    // إضافة البوابات
                    res.gates.forEach(function (gate) {
                        // استخدام الخصائص الصحيحة
                        const value = gate.value || gate.Id || "";
                        const text = gate.text || gate.no || "بوابة غير معروفة";

                        gatesDropdown.append(`<option value="${value}">${text}</option>`);
                    });

                    // إظهار حقل البوابات
                    gateContainer.removeClass("hidden");

                    // التركيز على قائمة البوابات
                    setTimeout(function () {
                        gatesDropdown.focus();
                    }, 100);
                } else {
                    // إخفاء حقل البوابات إذا لم يكن المستخدم من الأمن
                    gateContainer.addClass("hidden");

                    // التركيز على حقل كلمة المرور
                    passwordInput.focus();
                }
            },
            error: function (xhr, status, error) {
                // إخفاء مؤشر التحميل
                loadingSpinner.hide();
                loginButton.prop("disabled", false);

                let errorMsg = "حدث خطأ في الاتصال بالخادم";
                if (xhr.responseJSON && xhr.responseJSON.message) {
                    errorMsg = xhr.responseJSON.message;
                }

                showAlert(errorMsg, "danger");
                console.error("Error:", error);
            }
        });
    }

    // دالة لعرض الرسائل
    function showAlert(message, type) {
        loginAlert.removeClass("alert-danger alert-success hidden").addClass(`alert-${type}`);

        if (type === "danger") {
            loginAlert.html(`<i class="fas fa-exclamation-circle me-2"></i>${message}`);
        } else if (type === "success") {
            loginAlert.html(`<i class="fas fa-check-circle me-2"></i>${message}`);
        }

        // إظهار الرسالة
        loginAlert.removeClass("hidden");

        // إخفاء الرسالة بعد 5 ثواني
        setTimeout(function () {
            loginAlert.addClass("hidden");
        }, 5000);
    }
});