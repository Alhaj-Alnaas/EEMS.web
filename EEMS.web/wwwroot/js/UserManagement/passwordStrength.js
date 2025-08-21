function initPasswordStrength() {
    const passwordInput = document.getElementById("Password");
    const strengthBar = document.getElementById("passwordStrengthBar");
    const strengthText = document.getElementById("passwordStrengthText");

    if (!passwordInput || !strengthBar || !strengthText) return;

    passwordInput.addEventListener("input", function () {
        var password = this.value;
        var strength = 0;

        if (password.length >= 6) strength++;
        if (/[A-Z]/.test(password)) strength++;
        if (/[a-z]/.test(password)) strength++;
        if (/[0-9]/.test(password)) strength++;
        if (/[\W_]/.test(password)) strength++;

        switch (strength) {
            case 0:
                strengthBar.style.width = "0%";
                strengthBar.className = "progress-bar bg-danger";
                strengthText.innerText = "";
                break;
            case 1:
                strengthBar.style.width = "20%";
                strengthBar.className = "progress-bar bg-danger";
                strengthText.innerText = "ضعيفة جداً";
                break;
            case 2:
                strengthBar.style.width = "40%";
                strengthBar.className = "progress-bar bg-warning";
                strengthText.innerText = "ضعيفة";
                break;
            case 3:
                strengthBar.style.width = "60%";
                strengthBar.className = "progress-bar bg-info";
                strengthText.innerText = "متوسطة";
                break;
            case 4:
                strengthBar.style.width = "80%";
                strengthBar.className = "progress-bar bg-primary";
                strengthText.innerText = "جيدة";
                break;
            case 5:
                strengthBar.style.width = "100%";
                strengthBar.className = "progress-bar bg-success";
                strengthText.innerText = "قوية";
                break;
        }
    });
}
