/**
 * dar-detail.js — DAR Detail page JS
 * Handles: Approve / Reject / MR Agree / MR Not Agree / DCO Register
 * via Bootstrap modal → jQuery AJAX POST
 */

$(function () {

    var currentAction = null;
    var currentId     = null;
    var $modal        = new bootstrap.Modal($('#modalAction')[0]);

    // ── Open modal on action button click ─────────────────────────────
    $(document).on('click', '[data-action]', function () {
        currentAction = $(this).data('action');
        currentId     = $(this).data('id');

        // Reset modal state
        $('#actionRemarks').val('');
        $('#dcoDateWrap').hide();
        $('#dcoDate').val('');

        // Customize modal per action
        var titles = {
            'approve'      : 'Approve DAR',
            'reject'       : 'Reject DAR',
            'mr-agree'     : 'MR — Agree',
            'mr-disagree'  : 'MR — Not Agree',
            'dco-register' : 'DCO Register Document'
        };
        $('#modalTitle').text(titles[currentAction] || 'ดำเนินการ');

        var btnClass = (currentAction === 'approve' || currentAction === 'mr-agree' || currentAction === 'dco-register')
                        ? 'btn-success' : 'btn-danger';
        $('#btnConfirmAction').removeClass('btn-success btn-danger btn-primary').addClass(btnClass);

        if (currentAction === 'dco-register') {
            $('#dcoDateWrap').show();
            $('#dcoDate').val(new Date().toISOString().split('T')[0]);
        }

        $modal.show();
    });

    // ── Confirm action ────────────────────────────────────────────────
    $('#btnConfirmAction').on('click', function () {
        if (!currentAction || !currentId) return;

        var remarks = $('#actionRemarks').val().trim();
        var url, data;

        switch (currentAction) {
            case 'approve':
                url  = '/Dar/Approve';
                data = { id: currentId, remarks: remarks };
                break;
            case 'reject':
                url  = '/Dar/Reject';
                data = { id: currentId, remarks: remarks };
                break;
            case 'mr-agree':
                url  = '/Dar/MRAgree';
                data = { id: currentId, agree: true, remarks: remarks };
                break;
            case 'mr-disagree':
                url  = '/Dar/MRAgree';
                data = { id: currentId, agree: false, remarks: remarks };
                break;
            case 'dco-register':
                var dcoDate = $('#dcoDate').val();
                if (!dcoDate) { alert('กรุณาระบุ Doc Registered Date'); return; }
                url  = '/Dar/DCORegister';
                data = { id: currentId, registeredDate: dcoDate, remarks: remarks };
                break;
            default: return;
        }

        // Add CSRF token
        data.__RequestVerificationToken = $('input[name="__RequestVerificationToken"]').val()
            || $('meta[name="csrf-token"]').attr('content') || '';

        $('#btnConfirmAction').prop('disabled', true)
            .html('<span class="spinner-border spinner-border-sm"></span>');

        $.post(url, data)
            .done(function () { window.location.reload(); })
            .fail(function (xhr) {
                alert('เกิดข้อผิดพลาด: ' + (xhr.responseText || xhr.status));
                $('#btnConfirmAction').prop('disabled', false).text('ยืนยัน');
            });
    });

    // ── Add AntiForgery token to all AJAX POSTs ───────────────────────
    $.ajaxSetup({
        beforeSend: function (xhr, settings) {
            if (settings.type === 'POST') {
                var token = $('input[name="__RequestVerificationToken"]').first().val();
                if (token) xhr.setRequestHeader('RequestVerificationToken', token);
            }
        }
    });

});
