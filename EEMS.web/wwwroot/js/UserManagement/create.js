document.addEventListener("DOMContentLoaded", function () {
    // ربط زر Enter مع البحث عن موظف
    const empInput = document.getElementById("EmpFileNo");
    if (empInput) {
        empInput.addEventListener("keypress", function (event) {
            if (event.key === "Enter") {
                event.preventDefault();
                fetchEmployeeData();
            }
        });
    }

    // عرض رسالة نجاح بعد إضافة مستخدم
    const successMessage = document.getElementById("SuccessMessageValue")?.value;
    if (successMessage) {
        showAlert(successMessage, "success");

        setTimeout(() => {
            document.querySelector("form").reset();
            if (empInput) empInput.readOnly = false;
        }, 2000);
    }

    // تشغيل فحص قوة كلمة المرور
    initPasswordStrength();
});
