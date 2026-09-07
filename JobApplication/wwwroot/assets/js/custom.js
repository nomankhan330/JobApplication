function showMessage(Message, type, reload = false) {

    toastr.options = {
        positionClass: 'toast-top-right',
        toastClass: 'custom-toastr',
        closeMethod: 'fadeout',
        timeOut: 2000
    };

    if (type === "info")
        toastr.info(Message);
    else if (type === "success")
        toastr.success(Message);
    else if (type === "error")
        toastr.error(Message);

    // Optional Reload
    if (reload === true) {
        setTimeout(function () {
            window.location.reload();
        }, 500);
    }
}

function handleSessionExpired() {
    if (window.$ && $.fn && $.fn.dataTable) {
        $.fn.dataTable.ext.errMode = 'none';
    }

    Swal.fire({
        title: 'Session Expired!',
        text: 'Your session has expired. Please login again to continue.',
        icon: 'warning',
        confirmButtonText: 'OK',
        allowOutsideClick: false,
        allowEscapeKey: false
    }).then((result) => {
        if (result.isConfirmed) {
            // Immediate redirection without leaving browser history
            window.location.replace('/Home/Login');
        }
    });
}

function confirmDelete(callback) {

    Swal.fire({
        title: "Are you sure?",
        text: "This record will be deleted!",
        icon: "warning",
        showCancelButton: true,
        confirmButtonColor: "#d33",
        cancelButtonColor: "#6c757d",
        confirmButtonText: "Yes, delete it!",
        cancelButtonText: "Cancel"
    }).then((result) => {
        if (result.isConfirmed) {
            callback(); // Run your custom delete code
        }
    });

}

function formatDate(dateString) {
    if (!dateString) return "";
    return dateString.split('T')[0];
}

function formatDate2(value) {

    if (!value) return '';

    let date = new Date(value);

    return date.toLocaleDateString('en-GB', {
        day: '2-digit',
        month: 'short',
        year: 'numeric'
    });
}

function formatDateFilter(date) {
    let year = date.getFullYear();
    let month = String(date.getMonth() + 1).padStart(2, '0');
    let day = String(date.getDate()).padStart(2, '0');

    return `${year}-${month}-${day}`;
}


function formatReportDate(dateValue) {
    if (!dateValue) {
        return "All";
    }

    const dateParts = dateValue.split("-");

    if (dateParts.length === 3) {
        return `${dateParts[2]}-${dateParts[1]}-${dateParts[0]}`;
    }

    return dateValue;
}

function formatCurrency(value) {

    return parseFloat(value || 0)
        .toLocaleString('en-US', {

            minimumFractionDigits: 2,
            maximumFractionDigits: 2

        });
}

function formatAmount(data) {
    let val = parseFloat(data);

    if (isNaN(val) || val === 0) {
        return "—";
    }

    return val.toLocaleString('en-US', {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
    });
}

function formatAmount2(value) {

    let amount = parseFloat(value || 0);

    return amount.toLocaleString(undefined, {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
    });
}


function isNullOrEmpty(value) {
    return value === null || value === undefined || value === '';
}

function redirectAction(value) {
    setTimeout(function () {
        window.location.href = value;
    }, 3000);
}

function applyGlobalInputRules() {

    // CNIC Mask
    $("[data-mask='cnic']").mask("00000-0000000-0");

    // Phone Mask
    $("[data-mask='phone']").mask("0000-0000000");

    // Only Alpha Numeric
    $("[data-type='alphanumeric']").on("input", function () {
        this.value = this.value.replace(/[^a-zA-Z0-9]/g, "");
    });

    $("[data-type='numeric']").on("input", function () {
        this.value = this.value.replace(/[^0-9]/g, "");
    });

    $("[data-type='phone']").on("input", function () {
        this.value = this.value.replace(/[^0-9+]/g, "");
    });

    $("[data-type='uppercase']").on("input", function () {
        this.value = this.value.toUpperCase();
    });

    // Email validation + allowed characters
    $("[data-type='email']").on("input", function () {
        let val = $(this).val();

        // allowed chars only
        val = val.replace(/[^a-zA-Z0-9@._-]/g, "");
        $(this).val(val);

        // email validation
        const emailPattern = /^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[A-Za-z]{2,}$/;

        if (emailPattern.test(val)) {
            $(this).css("border-color", "#28a745"); // green
        } else {
            $(this).css("border-color", "#dc3545"); // red
        }
    });

}
