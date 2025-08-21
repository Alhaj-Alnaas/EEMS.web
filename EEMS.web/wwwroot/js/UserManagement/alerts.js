function showAlert(message, type) {
    const alertBox = document.getElementById("alertBox");
    const alertMessage = document.getElementById("alertMessage");

    if (!alertBox || !alertMessage) return;

    alertMessage.textContent = message;
    alertBox.className = `alert alert-${type} alert-dismissible fade show`;
    alertBox.classList.remove("d-none");

    setTimeout(() => {
        alertBox.classList.add("d-none");
    }, 5000);
}
