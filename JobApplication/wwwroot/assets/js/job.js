var today = new Date().toISOString().split('T')[0];
$('#JobDate').val(today);

if (editId > 0) {
    GetJobImportById(editId);
}

loadShipmentMode();
loadCustomer();
loadCostCenter();
loadPol();
loadPod();
loadJobType();
loadContainerType();
loadShipmentStatus();
loadDeliveryCity();
loadBLType();
loadBLStatus();

$('#ContainerNo').select2({
    tags: true,
    tokenSeparators: [',', ' '],
    placeholder: "Enter container numbers",
    width: '100%'
});

$('#JobTypeId').on('change', function () {

    if ($(this).find("option:selected").text() === 'Other') {
        $('#OtherJobTypeDiv').removeClass('d-none');
        $('#OtherJobType').attr('required', true);
    } else {
        $('#OtherJobTypeDiv').addClass('d-none');
        $('#OtherJobType').removeAttr('required').val('');
    }

});

$("#btnOpenPortLoadingModal").click(function () {

    $("#portLoadingModal").modal({
        backdrop: 'static',
        keyboard: false
    });

    $("#portLoadingModal").modal("show");

});

$("#btnOpenPortDischargeModal").click(function () {

    $("#portDischargeModal").modal({
        backdrop: 'static',
        keyboard: false
    });

    $("#portDischargeModal").modal("show");

});

$("#btnOpenDeliveryCityModal").click(function () {

    $("#deliveryCityModal").modal({
        backdrop: 'static',
        keyboard: false
    });

    $("#deliveryCityModal").modal("show");

});


let targetSelect = '';

// Save port Of loading
$('#btnSavePort').on('click', function () {

    let portName = $('#PolPortName').val().trim();

    if (portName !== '') {

        $.ajax({
            url: '/Job/SavePol',
            type: 'POST',
            data: { portName: portName },
            success: function (response) {

                console.log("pol response", response);

                let $dropdown = $('#PolId');

                // check if already exists
                let exists = $dropdown.find(`option[value='${response.id}']`).length > 0;

                if (!exists) {
                    $dropdown.append(
                        $('<option>', {
                            value: response.id,
                            text: response.name
                        })
                    );
                }

                // always select existing or newly added
                $dropdown.val(response.id);

                // reset + close
                $('#PolPortName').val('');
                $('#portLoadingModal').modal('hide');
            }
        });

    } else {
        alert('Please enter port name.');
    }
});

$('#btnSavePortPod').on('click', function () {

    let portName = $('#PodPortName').val().trim();

    if (portName !== '') {

        $.ajax({
            url: '/Job/SavePod',
            type: 'POST',
            data: { portName: portName },
            success: function (response) {

                console.log("pod response", response);

                let $dropdown = $('#PodId');

                // check existing option
                let exists = $dropdown.find(`option[value='${response.id}']`).length > 0;

                if (!exists) {
                    $dropdown.append(
                        $('<option>', {
                            value: response.id,
                            text: response.name
                        })
                    );
                }

                // select value
                $dropdown.val(response.id);

                // reset + close modal
                $('#PodPortName').val('');
                $('#portDischargeModal').modal('hide');
            }
        });

    } else {
        alert('Please enter POD name.');
    }
});

$('#btnSaveDeliveryCity').on('click', function () {

    let deliveryCityName = $('#DeliveryCityName').val().trim();

    if (deliveryCityName !== '') {

        $.ajax({
            url: '/Job/SaveDeliveryCity',
            type: 'POST',
            data: { deliveryCityName: deliveryCityName },
            success: function (response) {

                console.log("delivery city response", response);

                let $dropdown = $('#DeliveryCityId');

                // Check if option already exists
                let exists = $dropdown.find(`option[value='${response.id}']`).length > 0;

                if (!exists) {
                    $dropdown.append(
                        $('<option>', {
                            value: response.id,
                            text: response.name
                        })
                    );
                }

                // Select newly added value
                $dropdown.val(response.id);

                // Reset and close modal
                $('#DeliveryCityName').val('');
                $('#deliveryCityModal').modal('hide');
            },
            error: function () {
                alert('Error saving Delivery City');
            }
        });

    } else {
        alert('Please enter Delivery City name.');
    }
});

// Payment Type Change Event (works for dynamically added rows)
$(document).on('change', '.payment_type', function () {

    let paymentTypeId = $(this).val();
    let $currentRow = $(this).closest('.invoice_row');
    let $paymentDetailsDropdown = $currentRow.find('.payment_headers');

    loadPaymentDetails(paymentTypeId, $paymentDetailsDropdown);
});

// Add new row
$('#add_invoice_row').click(function () {
    $("#invoice_wrapper").append(getInvoiceRowHtml());
});

// Remove row
$(document).on('click', '.remove_invoice_row', function () {

    if ($('.invoice_row').length > 1) {
        $(this).closest('.invoice_row').remove();
    } else {
        alert('At least one invoice row is required.');
    }

});

$("#btnSubmit").on("click", function (e) {
    e.preventDefault();

    if (!validateForm()) return;

    var containerNos = $('#ContainerNo').val();
    var concatenated = "";

    for (var i = 0; i < containerNos.length; i++) {
        concatenated += containerNos[i];

        if (i !== containerNos.length - 1) {
            concatenated += ",";
        }
    }

    //console.log(concatenated);

    var formData = new FormData($("#jobForm")[0]);
    formData.delete("ContainerNo");
    formData.append("ContainerNo", concatenated);


    $('.invoice_row').each(function () {

        let $row = $(this);

        let values = [
            $row.find('.payment_type').val(),
            $row.find('.payment_headers').val(),
            $row.find('input[name="invoice_no[]"]').val(),
            $row.find('input[name="invoice_date[]"]').val(),
            $row.find('input[name="amount[]"]').val(),
            $row.find('input[name="payment_reference[]"]').val(),
            $row.find('input[name="paid_by[]"]').val()
        ];

        let isEmpty = values.every(v => !v || v.toString().trim() === "");

        if (isEmpty) {
            return; // skip row
        } else {
            formData.append('payment_type', values[0]);
            formData.append('payment_headers', values[1]);
            formData.append('invoice_no', values[2]);
            formData.append('invoice_date', values[3]);
            formData.append('amount', values[4]);
            formData.append('payment_reference', values[5]);
            formData.append('paid_by', values[6]);
        }

    });

    var $btn = $(this);

    for (let [key, value] of formData.entries()) {
        console.log(key, value);
    }
    //return;

    let shipmentType = $("#ShipmentType").val();

    $.ajax({
        url: "/Job/SaveJobImportMaster",
        type: "POST",
        data: formData,
        contentType: false,
        processData: false,
        beforeSend: function () {
            $btn.prop("disabled", true).text("Processing...");
        },
        success: function (data) {
            console.log("data", data);

            if (data.errorCode == 200) {

                showMessage("Data saved successfully!", "success");
                $btn.text("Submitted");

                if (shipmentType == 1) {
                    redirectAction("/Job/Import")
                } else {
                    redirectAction("/Job/Export")
                }
                
            } else {
                showMessage(data.message, "error");
                $btn.prop("disabled", false).text("Submit");
            }
        },
        error: function (error) {
            showMessage("An error occurred", "error");
        }
    });

});

$("#btnSubmitInvoice").on("click", function (e) {
    
    e.preventDefault();
    if (!validateForm()) return;

    var formData = new FormData($("#jobForm")[0]);

    $('.invoice_row').each(function () {

        let $row = $(this);

        let values = [
            $row.find('.payment_type').val(),
            $row.find('.payment_headers').val(),
            $row.find('input[name="invoice_no[]"]').val(),
            $row.find('input[name="invoice_date[]"]').val(),
            $row.find('input[name="amount[]"]').val(),
            $row.find('input[name="payment_reference[]"]').val(),
            $row.find('input[name="paid_by[]"]').val()
        ];

        let isEmpty = values.every(v => !v || v.toString().trim() === "");

        if (isEmpty) {
            return; // skip row
        } else {
            formData.append('payment_type', values[0]);
            formData.append('payment_headers', values[1]);
            formData.append('invoice_no', values[2]);
            formData.append('invoice_date', values[3]);
            formData.append('amount', values[4]);
            formData.append('payment_reference', values[5]);
            formData.append('paid_by', values[6]);
        }

    });

    var $btn = $(this);
    let shipmentType = $("#ShipmentType").val();

    // for (let [key, value] of formData.entries()) {
    //     console.log(key, value);
    // }
    // return;

    $.ajax({
        url: "/Job/SaveJobImportMasterInvoice",
        type: "POST",
        data: formData,
        contentType: false,
        processData: false,
        beforeSend: function () {
            $btn.prop("disabled", true).text("Processing...");
        },
        success: function (data) {
            console.log("data", data);

            if (data.errorCode == 200) {

                showMessage("Data saved successfully!", "success");
                $btn.text("Submitted");

                if (shipmentType == 1) {
                    redirectAction("/Job/Import")
                } else {
                    redirectAction("/Job/Export")
                }

            } else {
                showMessage(data.message, "error");
                $btn.prop("disabled", false).text("Submit");
            }
        },
        error: function (error) {
            showMessage("An error occurred", "error");
        }
    });

});


function checkPage() {
    var isDirty = false;

    // Jab bhi input change ho
    $('#jobForm').on('input change', 'input, textarea, select', function () {
        isDirty = true;
    });

    // Form submit hone par dirty reset
    $('#btnSubmit').on('click', function () {
        isDirty = false;
    });

    // Page leave warning
    $(window).on('beforeunload', function () {
        if (isDirty) {
            return "Aapka data save nahi hua!";
        }
    });
}

function getInvoiceRowHtml() {
    return `
                <div class="invoice_row border rounded p-3 mb-3">
                    <div class="row">

                        <div class="col-md-3">
                            <label class="form-label">Payment Type</label>
                            <select class="form-select payment_type" name="payment_type[]">
                                <option value="">Select</option>
                                <option value="1">Official</option>
                                <option value="2">Unofficial</option>
                            </select>
                        </div>

                        <div class="col-md-3">
                            <label class="form-label">Headers</label>
                            <select class="form-select payment_headers" name="payment_headers[]">
                                <option value="">Select Headers</option>
                            </select>
                        </div>

                        <div class="col-md-2">
                            <label class="form-label">SP Invoice No</label>
                                <input type="text" class="form-control invoice_no" name="invoice_no[]">
                        </div>

                        <div class="col-md-2">
                            <label class="form-label">SP Invoice Date</label>
                            <input type="date" class="form-control" name="invoice_date[]">
                        </div>

                        <div class="col-md-2">
                            <label class="form-label">Amount</label>
                            <input type="number" step="0.01" class="form-control" name="amount[]">
                        </div>

                        <div class="col-md-3 mt-3">
                            <label class="form-label">Payment Reference</label>
                            <input type="text" class="form-control" name="payment_reference[]" placeholder="SADAD / Bank / Cash">
                        </div>

                        <div class="col-md-3 mt-3">
                            <label class="form-label">Paid By (Name)</label>
                            <input type="text" class="form-control" name="paid_by[]">
                        </div>

                        <div class="col-md-3 mt-5">
                            <button type="button" class="btn btn-danger btn-sm remove_invoice_row">
                                × Remove
                            </button>
                        </div>

                    </div>
                </div>`;
}

function loadCustomer_bk(selectedId = null) {
    $.ajax({
        url: '/Dropdown/GetCustomer',
        type: 'GET',
        dataType: 'json',
        success: function (response) {

            let $dropdown = $('#CustomerId');

            $dropdown.empty();
            $dropdown.append('<option value=""></option>');

            $.each(response.data, function (i, item) {
                $dropdown.append(
                    `<option value="${item.Id}">
                                ${item.CustomerName} (${item.CustomerCode})
                            </option>`
                );
            });

            // Destroy previous Select2 instance (important)
            if ($dropdown.hasClass("select2-hidden-accessible")) {
                $dropdown.select2('destroy');
            }

            // Initialize Select2
            $dropdown.select2({
                placeholder: "Select Customer",
                allowClear: true,
                width: '100%'
            });

            // Set selected value
            if (selectedId) {
                $dropdown.val(selectedId).trigger('change');
            }
        },
        error: function (xhr, status, error) {
            console.error("Error loading customers:", error);
        }
    });
}

function loadCustomer(customerId = null) {

    if ($('#CustomerId').hasClass("select2-hidden-accessible")) {
        $('#CustomerId').select2('destroy');
    }

    $('#CustomerId').select2({

        placeholder: 'Select Customer',
        allowClear: true,
        width: '100%',

        ajax: {
            url: '/Dropdown/GetCustomerSearch',
            type: 'GET',
            dataType: 'json',
            delay: 250,

            data: function (params) {
                return {
                    search: params.term || "",
                    page: params.page || 1
                };
            },

            processResults: function (data, params) {

                params.page = params.page || 1;

                return {
                    results: data.data,
                    pagination: {
                        more: data.hasMore
                    }
                };
            },

            cache: true
        }
    });

    // Select customer by ID
    if (customerId) {

        $.ajax({
            url: '/Customer/GetCustomerById',
            type: 'GET',
            dataType: 'json',
            data: {
                id: customerId
            },

            success: function (data) {

                if (data.errorCode === 200 && data.Customer) {

                    var customer = data.Customer;

                    var option = new Option(
                        customer.CustomerName + ' (' + customer.CustomerCode + ')',
                        customer.Id,
                        true,
                        true
                    );

                    $('#CustomerId')
                        .append(option)
                        .trigger('change');
                }
            },
            
            error: function (xhr) {
                console.log('Customer load error:', xhr);
            }
        });
    }
}


function loadCostCenter(selectedId = null) {
    $.ajax({
        url: '/Dropdown/GetCostCenterAssigned',
        type: 'GET',
        dataType: 'json',
        success: function (response) {

            let $dropdown = $('#CostCenterId');

            $dropdown.empty();
            $dropdown.append('<option value=""></option>');

            $.each(response.data, function (i, item) {
                $dropdown.append(
                    `<option value="${item.Id}">
                                ${item.CostCenterName} (${item.CostCenterCode})
                            </option>`
                );
            });

            // Destroy previous Select2 (important for reload cases)
            if ($dropdown.hasClass("select2-hidden-accessible")) {
                $dropdown.select2('destroy');
            }

            // Initialize Select2
            $dropdown.select2({
                placeholder: "Select Cost Center",
                allowClear: true,
                width: '100%'
            });

            // Set selected value
            if (selectedId) {
                $dropdown.val(selectedId).trigger('change');
            }
        },
        error: function (xhr, status, error) {
            console.error("Error loading cost centers:", error);
        }
    });
}

function loadShipmentMode(selectedId = null) {
    $.ajax({
        url: '/Dropdown/GetShipmentMode',
        type: 'GET',
        dataType: 'json',
        success: function (response) {

            let $dropdown = $('#ShipmentModeId');
            $dropdown.empty();
            $dropdown.append('<option value="" disabled selected>Select Shipment Mode</option>');

            $.each(response.data, function (i, item) {
                $dropdown.append(`<option value="${item.Id}">${item.Name}</option>`);
            });

            if (selectedId !== null && selectedId !== "" && selectedId !== undefined) {
                $dropdown.val(selectedId);
            }
        },
        error: function (xhr, status, error) {
            console.error("Error loading shipment modes:", error);
        }
    });
}

function loadJobType(selectedId = null) {
    $.ajax({
        url: '/Dropdown/GetJobType',
        type: 'GET',
        dataType: 'json',
        success: function (response) {

            let $dropdown = $('#JobTypeId');
            $dropdown.empty();
            $dropdown.append('<option value="" disabled selected>Select Job Type</option>');

            $.each(response.data, function (i, item) {
                $dropdown.append(`<option value="${item.Id}">${item.Name}</option>`);
            });

            if (selectedId !== null && selectedId !== "" && selectedId !== undefined) {
                $dropdown.val(selectedId);
            }
        },
        error: function (xhr, status, error) {
            console.error("Error loading job types:", error);
        }
    });

}

function loadContainerType(selectedId = null) {
    $.ajax({
        url: '/Dropdown/GetContainerType',
        type: 'GET',
        dataType: 'json',
        success: function (response) {

            let $dropdown = $('#ContainerTypeId');
            $dropdown.empty();
            $dropdown.append('<option value="" disabled selected>Select Container Type</option>');

            $.each(response.data, function (i, item) {
                $dropdown.append(`<option value="${item.Id}">${item.Name}</option>`);
            });

            if (selectedId !== null && selectedId !== "" && selectedId !== undefined) {
                $dropdown.val(selectedId);
            }
        },
        error: function (xhr, status, error) {
            console.error("Error loading container types:", error);
        }
    });
}

function loadBLType(selectedId = null) {
    $.ajax({
        url: '/Dropdown/GetBLType',
        type: 'GET',
        dataType: 'json',
        success: function (response) {

            let $dropdown = $('#BlTypeId');
            $dropdown.empty();
            $dropdown.append('<option value="" disabled selected>Select BL Type</option>');

            $.each(response.data, function (i, item) {
                $dropdown.append(`<option value="${item.Id}">${item.Name}</option>`);
            });

            if (selectedId !== null && selectedId !== "" && selectedId !== undefined) {
                $dropdown.val(selectedId);
            }
        },
        error: function (xhr, status, error) {
            console.error("Error loading BL types:", error);
        }
    });
}

function loadPol(selectedId = null) {
    $.ajax({
        url: '/Dropdown/GetPol',
        type: 'GET',
        dataType: 'json',
        success: function (response) {

            let $dropdown = $('#PolId');
            $dropdown.empty();
            $dropdown.append('<option value="" disabled selected>Select POL</option>');

            $.each(response.data, function (i, item) {
                $dropdown.append(`<option value="${item.Id}">${item.Name}</option>`);
            });

            if (selectedId !== null && selectedId !== "" && selectedId !== undefined) {
                $dropdown.val(selectedId);
            }
        },
        error: function (xhr, status, error) {
            console.error("Error loading POL:", error);
        }
    });
}

function loadPod(selectedId = null) {
    $.ajax({
        url: '/Dropdown/GetPod',
        type: 'GET',
        dataType: 'json',
        success: function (response) {

            let $dropdown = $('#PodId');
            $dropdown.empty();
            $dropdown.append('<option value="" disabled selected>Select POD</option>');

            $.each(response.data, function (i, item) {
                $dropdown.append(`<option value="${item.Id}">${item.Name}</option>`);
            });

            if (selectedId !== null && selectedId !== "" && selectedId !== undefined) {
                $dropdown.val(selectedId);
            }
        },
        error: function (xhr, status, error) {
            console.error("Error loading POD:", error);
        }
    });
}

function loadDeliveryCity(selectedId = null) {
    $.ajax({
        url: '/Dropdown/GetDeliveryCity',
        type: 'GET',
        dataType: 'json',
        success: function (response) {

            let $dropdown = $('#DeliveryCityId');
            $dropdown.empty();
            $dropdown.append('<option value="" disabled selected>Select Delivery City</option>');

            $.each(response.data, function (i, item) {
                $dropdown.append(`<option value="${item.Id}">${item.Name}</option>`);
            });

            if (selectedId !== null && selectedId !== "" && selectedId !== undefined) {
                $dropdown.val(selectedId);
            }
        },
        error: function (xhr, status, error) {
            console.error("Error loading Delivery City:", error);
        }
    });
}

function loadShipmentStatus(selectedId = null) {
    $.ajax({
        url: '/Dropdown/GetShipmentStatus',
        type: 'GET',
        dataType: 'json',
        success: function (response) {

            let $dropdown = $('#ShipmentStatusId');
            $dropdown.empty();
            $dropdown.append('<option value="" disabled selected>Select Shipment Status</option>');

            $.each(response.data, function (i, item) {
                $dropdown.append(`<option value="${item.Id}">${item.Name}</option>`);
            });

            if (selectedId !== null && selectedId !== "" && selectedId !== undefined) {
                $dropdown.val(selectedId);
            }
        },
        error: function (xhr, status, error) {
            console.error("Error loading shipment status:", error);
        }
    });

}

function loadBLStatus(selectedId = null) {
    $.ajax({
        url: '/Dropdown/GetBLStatus',
        type: 'GET',
        dataType: 'json',
        success: function (response) {

            let $dropdown = $('#BlStatusId');
            $dropdown.empty();
            $dropdown.append('<option value="" disabled selected>Select BL Status</option>');

            $.each(response.data, function (i, item) {
                $dropdown.append(`<option value="${item.Id}">${item.Name}</option>`);
            });

            if (selectedId !== null && selectedId !== "" && selectedId !== undefined) {
                $dropdown.val(selectedId);
            }
        },
        error: function (xhr, status, error) {
            console.error("Error loading BL types:", error);
        }
    });
}

function setControlsReadOnly(containerSelector, isReadOnly = true) {
    // Inputs & Textareas
    $(containerSelector)
        .find('input, textarea')
        .not('.allow-action, [type="hidden"]')
        .prop('readonly', isReadOnly);

    // 🔥 Only disable buttons having class "btn-disabled"
    $(containerSelector)
        .find('button.btn-disabled')
        .not('.allow-action')
        .prop('disabled', isReadOnly);

    // Selects
    $(containerSelector)
        .find('select')
        .not('.allow-action')
        .each(function () {
            const $select = $(this);
            const name = $select.attr('name');

            if (isReadOnly) {
                // Create/update hidden field
                let $hidden = $select.data('readonly-hidden');

                if (!$hidden || !$hidden.length) {
                    $hidden = $('<input>', {
                        type: 'hidden',
                        name: name,
                        class: 'readonly-hidden'
                    });

                    $select.after($hidden);
                    $select.data('readonly-hidden', $hidden);

                    // Prevent duplicate submission
                    $select.removeAttr('name');
                    $select.data('original-name', name);
                }

                $hidden.val($select.val());
                $select.prop('disabled', true);

            } else {
                // Restore original name
                const originalName = $select.data('original-name');
                if (originalName) {
                    $select.attr('name', originalName);
                }

                // Remove hidden field
                const $hidden = $select.data('readonly-hidden');
                if ($hidden && $hidden.length) {
                    $hidden.remove();
                }

                $select.prop('disabled', false);
            }
        });

    // Refresh Select2
    $(containerSelector)
        .find('select')
        .trigger('change.select2');
}

function GetJobImportById() {

    let id = editId;//$(this).data("id");

    // Reset Form
    $("#jobForm")[0].reset();
    $("#invoice_wrapper").empty();
    $("#jobForm .is-invalid").removeClass("is-invalid");

    // 🔥 SHOW LOADER + DISABLE SCREEN
    $("#globalLoader").show();
    //$("#jobForm :input, #jobForm button").prop("disabled", true);

    $.ajax({
        type: "POST",
        url: "/Job/GetAllJobById",
        data: { id: id },
        success: function (resp) {

            console.log("Edit Response:", resp);

            if (!resp || resp.errorCode !== 200 || !resp.data) {
                showMessage && showMessage("Record not found", "error");
                return;
            }

            let master = resp.data.JobImportMaster?.[0];   // single record
            let payments = resp.data.JobImportPayment || []; // list

            //console.log("Master:", master);
            //console.log("Payments:", payments);


            let data = master;
            //console.log("ContainerNo", data.ContainerNo)
            //alert(data.JobNumber)

            // Basic Information
            $("#Id").val(data.Id);
            $("#JobNumber").val(data.JobNumber);
            $("#JobNumberShow").val(data.JobNumber);
            $("#JobDate").val(formatDate(data.JobDate));
            $("#OtherJobType").val(data.OtherJobType);
            $("#Shipper").val(data.Shipper);
            $("#Consignee").val(data.Consignee);
            $("#Carrier").val(data.Carrier);
            $("#BookingDate").val(formatDate(data.BookingDate));
            $("#Etd").val(formatDate(data.Etd));
            $("#Eta").val(formatDate(data.Eta));
            $("#BayanNo").val(data.BayanNo);
            $("#BookingNo").val(data.BookingNo);
            $("#BlNo").val(data.BlNo);
            $("#ContainerQuantity").val(data.ContainerQuantity);
            $("#BayanPrintDate").val(formatDate(data.BayanPrintDate));
            $("#ShipmentClearedDate").val(formatDate(data.ShipmentClearedDate));
            $("#DeliveryDate").val(formatDate(data.DeliveryDate));
            $("#SiReceivedDate").val(formatDate(data.SiReceivedDate));
            $("#ManifestReceivedDate").val(formatDate(data.ManifestReceivedDate));
            $("#SailDate").val(formatDate(data.SailDate));
            $("#ArrivalDate").val(formatDate(data.ArrivalDate));
            $("#OurInvoiceNo").val(data.OurInvoiceNo);
            $("#CreditNoteNo").val(data.CreditNoteNo);
            $("#AddInvoiceNo").val(data.AddInvoiceNo);
            $("#Remarks").val(data.Remarks);


            loadCostCenter(data.CostCenterId);
            loadShipmentMode(data.ShipmentModeId);
            loadJobType(data.JobTypeId);
            loadCustomer(data.CustomerId);
            loadContainerType(data.ContainerTypeId);
            loadDeliveryCity(data.DeliveryCityId);
            loadShipmentStatus(data.ShipmentStatusId);
            loadPol(data.PolId);
            loadPod(data.PodId);
            loadBLStatus(data.BlStatusId);
            loadBLType(data.BlTypeId);

            if (data.ContainerNo) {
                let containerNumbers = data.ContainerNo.split(',');

                let $container = $('#ContainerNo');

                $container.empty();

                containerNumbers.forEach(function (item) {
                    item = item.trim();

                    if (item) {
                        let option = new Option(item, item, true, true);
                        $container.append(option);
                    }
                });

                $container.trigger('change');
            }
            //return;

            // Invoice Rows
            if (payments && payments.length > 0) {
                $.each(payments, function (index, item) {

                    let rowHtml = getInvoiceRowHtml();
                    $("#invoice_wrapper").append(rowHtml);

                    let $row = $("#invoice_wrapper .invoice_row").last();

                    $row.find(".payment_type").val(item.PaymentType);


                    let $paymentDetailsDropdown = $row.find('.payment_headers');
                    loadPaymentDetails(item.PaymentType, $paymentDetailsDropdown, item.PaymentHeaderId);

                    $row.find('input[name="invoice_no[]"]').val(item.InvoiceNo);
                    $row.find('input[name="invoice_date[]"]').val(formatDate(item.InvoiceDate));
                    $row.find('input[name="amount[]"]').val(item.Amount);
                    $row.find('input[name="payment_reference[]"]').val(item.PaymentReference);
                    $row.find('input[name="paid_by[]"]').val(item.PaidBy);
                });
            } else {
                $("#invoice_wrapper").append(getInvoiceRowHtml());
            }

            if (userType == 4) {
                setControlsReadOnly('.readonly-mode', true)
            }

        },
        error: function () {
            showMessage && showMessage("An error occurred while fetching job details.", "error");
        }, 
        complete: function () {

            // 🔥 HIDE LOADER + ENABLE SCREEN
            $("#globalLoader").hide();
            //$("#jobForm :input, #jobForm button").prop("disabled", false);
        }
    });
}

function loadPaymentDetails(paymentTypeId, $dropdown, selectedId = null) {

    $dropdown.empty();
    $dropdown.append('<option value="">Select Payment Details</option>');

    if (!paymentTypeId) {
        return;
    }

    $.ajax({
        url: '/Dropdown/GetPaymentHeadersByType',
        type: 'GET',
        data: { paymentTypeId: paymentTypeId },
        dataType: 'json',
        success: function (response) {

            if (response.errorCode === 200 && response.data) {
                $.each(response.data, function (i, item) {
                    $dropdown.append(
                        `<option value="${item.Id}">${item.CombinedHeader}</option>`
                    );
                });

                if (selectedId !== null && selectedId !== "" && selectedId !== undefined) {
                    $dropdown.val(selectedId);
                }
            }
        },
        error: function (xhr, status, error) {
            console.error("Error loading payment details:", error);
        }
    });
}

function validateForm() {
    let valid = true;
    let firstInvalid = null;
    let invalidFields = [];

    // =========================
    // Required Fields Validation
    // =========================
    $("#jobForm [required]:not(:disabled):not([readonly])").each(function () {

        let $this = $(this);
        let value = $this.val();

        if ($this.hasClass("select2-hidden-accessible")) {

            if (!value || (Array.isArray(value) && value.length === 0)) {

                console.log("Invalid Select2 Field:", {
                    name: $this.attr("name"),
                    id: $this.attr("id"),
                    value: value
                });

                invalidFields.push({
                    name: $this.attr("name"),
                    id: $this.attr("id"),
                    value: value,
                    type: "Select2"
                });

                $this.next(".select2-container").addClass("is-invalid");
                valid = false;

                if (!firstInvalid) {
                    firstInvalid = $this.next(".select2-container");
                }

            } else {
                $this.next(".select2-container").removeClass("is-invalid");
            }

        } else {

            if (!value) {

                console.log("Invalid Required Field:", {
                    name: $this.attr("name"),
                    id: $this.attr("id"),
                    value: value,
                    type: $this.prop("tagName")
                });

                invalidFields.push({
                    name: $this.attr("name"),
                    id: $this.attr("id"),
                    value: value,
                    type: $this.prop("tagName")
                });

                $this.addClass("is-invalid");
                valid = false;

                if (!firstInvalid) {
                    firstInvalid = $this;
                }

            } else {
                $this.removeClass("is-invalid");
            }
        }
    });

    // =========================
    // Strict Invoice Row Validation
    // =========================

    $(".invoice_row").each(function (rowIndex) {

        let $row = $(this);
        let rowFields = $row.find("select, input").not(":disabled");

        let hasAnyValue = false;

        // 🔹 Check if ANY field in row has value
        rowFields.each(function () {
            let value = $(this).val();

            if (value && value.toString().trim() !== "") {
                hasAnyValue = true;
                return false; // break loop
            }
        });

        // 🔹 Skip completely empty row
        if (!hasAnyValue) {
            return true; // continue next row
        }

        // 🔥 Validate ONLY this row (because it has some data)
        rowFields.each(function () {

            let $field = $(this);
            let value = $field.val();

            if (!value || value.toString().trim() === "") {

                console.log("Invalid Invoice Field:", {
                    row: rowIndex + 1,
                    name: $field.attr("name"),
                    id: $field.attr("id"),
                    value: value
                });

                invalidFields.push({
                    row: rowIndex + 1,
                    name: $field.attr("name"),
                    id: $field.attr("id"),
                    value: value,
                    type: "Invoice Row"
                });

                $field.addClass("is-invalid");
                valid = false;

                if (!firstInvalid) {
                    firstInvalid = $field;
                }

            } else {
                $field.removeClass("is-invalid");
            }
        });

    });


    // =========================
    // Show Invalid Fields in Console
    // =========================
    if (!valid) {
        console.warn("Validation Failed");
        console.table(invalidFields);
    }

    // =========================
    // Scroll to First Invalid Field
    // =========================
    if (firstInvalid) {
        $('html, body').animate({
            scrollTop: firstInvalid.offset().top - 100
        }, 500);

        if (!firstInvalid.hasClass("select2-container")) {
            firstInvalid.focus();
        }
    }

    return valid;
}


//testing data
function fillTestJobForm() {

    // 🔹 Basic Info
    $("#ShipmentType").val(); // Import/Export
    $("#CostCenterId").val(1).trigger('change');
    
    $("#ShipmentModeId").val(1).trigger('change');
    $("#JobTypeId").val(1).trigger('change');
    $("#OtherJobType").val("Test Type");

    // 🔹 Customer & Parties
    $("#CustomerId").val(2).trigger('change');
    $("#Shipper").val("Test Shipper Pvt Ltd");
    $("#Consignee").val("Test Consignee Pvt Ltd");
    $("#Carrier").val("Maersk Shipping Line");

    // 🔹 Dates
    $("#BookingDate").val("2026-07-01");
    $("#Etd").val("2026-07-03");
    $("#Eta").val("2026-07-10");
    $("#BayanPrintDate").val("2026-07-02");
    $("#ShipmentClearedDate").val("2026-07-11");
    $("#DeliveryDate").val("2026-07-12");
    $("#ArrivalDate").val("2026-07-09");

    $("#SiReceivedDate").val("2026-07-08");
    $("#ManifestReceivedDate").val("2026-08-09");
    $("#SailDate").val("2026-09-09");

    // 🔹 Ports
    $("#PolId").val(12).trigger('change');
    $("#PodId").val(8).trigger('change');

    // 🔹 Shipment Details
    $("#BayanNo").val("BAY-" + Math.floor(1000 + Math.random() * 9000));
    $("#BlNo").val("BL-" + Math.floor(1000 + Math.random() * 9000));

    $("#ContainerQuantity").val("2");
    fillRandomContainers();

    $("#ContainerTypeId").val(1).trigger('change');

    // 🔹 Delivery
    $("#DeliveryCityId").val(9).trigger('change');

    // 🔹 Invoice / Docs
    $("#BlTypeId").val(1).trigger('change');
    $("#ShipmentStatusId").val(1).trigger('change');

    $("#OurInvoiceNo").val("INV-1001");
    $("#CreditNoteNo").val("CN-2001");
    $("#AddInvoiceNo").val("AINV-3001");

    // 🔹 Remarks
    $("#Remarks").val("This is auto-generated test data for development testing.");


    //Export test data
    $("#BookingNo").val("BOOK-" + Math.floor(1000 + Math.random() * 9000));

    $("#BlStatusId").val(1).trigger('change');

    console.log("✅ Form auto-filled successfully!");
}

function fillRandomContainers() {

    let count = 3;
    let containerNumbers = [];

    for (let i = 1; i <= count; i++) {
        containerNumbers.push("CONT-" + Math.floor(1000 + Math.random() * 9000));
    }

    let $container = $('#ContainerNo');

    $container.empty();

    containerNumbers.forEach(function (item) {

        let option = new Option(item, item, true, true);
        $container.append(option);

    });

    $container.trigger('change');

    console.log("🔥 Random containers filled:", containerNumbers);
}