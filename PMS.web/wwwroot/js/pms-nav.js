window.pmsGoBack = function (fallback) {
    if (window.history.length > 1) {
        window.history.back();
        return;
    }

    window.location.assign(fallback || "/dashboard");
};
