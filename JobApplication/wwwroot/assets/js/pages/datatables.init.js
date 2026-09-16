    var a=$("#datatable-buttons").DataTable({lengthChange:!1,language:{paginate:{previous:"<i class='mdi mdi-chevron-left'>",next:"<i class='mdi mdi-chevron-right'>"}},drawCallback:function(){$(".dataTables_paginate > .pagination").addClass("pagination-rounded")},buttons:["copy","excel","pdf","colvis"]});

    // Guard against missing Buttons extension which causes `a.buttons is not a function`
    try {
        if (a && typeof a.buttons === 'function') {
            a.buttons().container().appendTo("#datatable-buttons_wrapper .col-md-6:eq(0)");
        }
    } catch (err) {
        console.warn('DataTables Buttons extension not available:', err);
    }

    $(".dataTables_length select").addClass("form-select form-select-sm"),$("#selection-datatable").DataTable({select:{style:"multi"},language:{paginate:{previous:"<i class='mdi mdi-chevron-left'>",next:"<i class='mdi mdi-chevron-right'>"}},drawCallback:function(){$(".dataTables_paginate > .pagination").addClass("pagination-rounded")}}),$("#key-datatable").DataTable({keys:!0,language:{paginate:{previous:"<i class='mdi mdi-chevron-left'>",next:"<i class='mdi mdi-chevron-right'>"}},drawCallback:function(){$(".dataTables_paginate > .pagination").addClass("pagination-rounded")}});
    try {
        if (a && typeof a.buttons === 'function') {
            a.buttons().container().appendTo("#datatable-buttons_wrapper .col-md-6:eq(0)");
        }
    } catch (err) {
        console.warn('DataTables Buttons extension not available on second append:', err);
    }

    $(".dataTables_length select").addClass("form-select form-select-sm"),$("#alternative-page-datatable").DataTable({pagingType:"full_numbers",drawCallback:function(){$(".dataTables_paginate > .pagination").addClass("pagination-rounded"),$(".dataTables_length select").addClass("form-select form-select-sm")}})
