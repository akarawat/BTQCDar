/**
 * dar-detail.js — DAR Detail page workflow actions
 *
 * Actions: review / approve / reject / mr-agree / mr-disagree / dco-register
 * All actions → AJAX JSON POST → reload on success / show error inline
 */

$(function () {

    // Lazy-init Bootstrap modal (only exists when canReview||canApprove||canMR||canDCO)
    var $modalEl = $('#modalAction');
    var $modal = $modalEl.length ? new bootstrap.Modal($modalEl[0]) : null;

    // ── Open modal on action button click ─────────────────────────────
    $(document).on('click', '[data-action]', function () {
        if (!$modal) return;

        var action = $(this).data('action');
        var id = $(this).data('id');
        $modalEl.data('action', action).data('id', id);

        // Reset modal state
        $('#actionRemarks').val('');
        $('#dcoDateWrap').hide();
        $('#dcoDate').val('');
        $('#modalActionError').remove();

        // Modal title + confirm button colour
        var titles = {
            'sign-review': 'Sign & Forward to Approver',
            'sign-approve': 'Sign & Approve',
            'review': 'Review & Forward to Approver',
            'approve': 'Approve DAR',
            'reject': 'Reject DAR',
            'mr-agree': 'MR — Agree',
            'mr-disagree': 'MR — Not Agree',
            'dco-register': 'DCO Register Document'
        };
        $('#modalTitle').text(titles[action] || 'Confirm Action');

        var isPositive = ['sign-review', 'sign-approve', 'review', 'approve', 'mr-agree', 'dco-register'].indexOf(action) > -1;
        $('#btnConfirmAction')
            .removeClass('btn-success btn-danger btn-primary')
            .addClass(isPositive ? 'btn-success' : 'btn-danger')
            .prop('disabled', false)
            .text('Confirm');

        // DCO date picker
        if (action === 'dco-register') {
            $('#dcoDateWrap').show();
            $('#dcoDate').val(new Date().toISOString().split('T')[0]);
        }

        // Remarks hint per action
        var hints = {
            'sign-review': 'Remarks for digital signature (optional)',
            'sign-approve': 'Remarks for digital signature (optional)',
            'review': 'Review comments (optional)',
            'reject': 'Please provide a reason for rejection.',
            'approve': 'Approval remarks (optional)'
        };
        $('#actionRemarks').attr('placeholder', hints[action] || 'Enter remarks or reason...');

        $modal.show();
    });

    // ── Confirm button → AJAX POST ────────────────────────────────────
    $('#btnConfirmAction').on('click', function () {
        var action = $modalEl.data('action');
        var id = $modalEl.data('id');
        if (!action || !id) return;

        var remarks = $('#actionRemarks').val().trim();

        // Reject requires a reason
        if (action === 'reject' && !remarks) {
            showModalError('Please provide a reason for rejection.');
            return;
        }

        var url, postData;

        switch (action) {
            case 'sign-review':
                url = '/Dar/SignReview';
                postData = { id: id, remarks: remarks };
                break;
            case 'sign-approve':
                url = '/Dar/SignApprove';
                postData = { id: id, remarks: remarks };
                break;
            case 'review':
                url = '/Dar/Review';
                postData = { id: id, remarks: remarks };
                break;
            case 'approve':
                url = '/Dar/Approve';
                postData = { id: id, remarks: remarks };
                break;
            case 'reject':
                url = '/Dar/Reject';
                postData = { id: id, remarks: remarks };
                break;
            case 'mr-agree':
                url = '/Dar/MRAgree';
                postData = { id: id, agree: true, remarks: remarks };
                break;
            case 'mr-disagree':
                url = '/Dar/MRAgree';
                postData = { id: id, agree: false, remarks: remarks };
                break;
            case 'dco-register':
                var dcoDate = $('#dcoDate').val();
                if (!dcoDate) { showModalError('Please enter the Doc Registered Date.'); return; }
                url = '/Dar/DCORegister';
                postData = { id: id, registeredDate: dcoDate, remarks: remarks };
                break;
            default: return;
        }

        postData.__RequestVerificationToken =
            $('input[name="__RequestVerificationToken"]').val() || '';

        // Loading state
        var $btn = $('#btnConfirmAction');
        $btn.prop('disabled', true)
            .html('<span class="spinner-border spinner-border-sm me-1"></span>Processing...');

        $.post(url, postData)
            .done(function (res) {
                // Both JSON and redirect responses
                if (res && res.success === false) {
                    showModalError(res.message || 'An error occurred.');
                    $btn.prop('disabled', false).text('Confirm');
                    return;
                }
                // Success — close modal and reload
                $modal.hide();
                showPageSuccess(res.message || 'Action completed successfully.');
                setTimeout(function () { window.location.reload(); }, 1000);
            })
            .fail(function (xhr) {
                var msg = 'Server error (' + xhr.status + '). Please try again.';
                try {
                    var json = JSON.parse(xhr.responseText);
                    if (json.message) msg = json.message;
                } catch (e) { }
                showModalError(msg);
                $btn.prop('disabled', false).text('Confirm');
            });
    });

    // ── Show error inside modal ───────────────────────────────────────
    function showModalError(msg) {
        $('#modalActionError').remove();
        var $err = $('<div id="modalActionError" class="alert alert-danger alert-sm py-2 mt-2 mb-0 small">'
            + '<i class="bi bi-exclamation-circle me-1"></i>'
            + $('<span>').text(msg).html() + '</div>');
        $('.modal-body').append($err);
    }

    // ── Show success banner on page ───────────────────────────────────
    function showPageSuccess(msg) {
        var $banner = $('<div class="alert alert-success alert-dismissible fade show position-fixed '
            + 'top-0 start-50 translate-middle-x mt-3 shadow" style="z-index:9999;min-width:320px">'
            + '<i class="bi bi-check-circle me-1"></i>'
            + $('<span>').text(msg).html()
            + '<button type="button" class="btn-close" data-bs-dismiss="alert"></button></div>');
        $('body').append($banner);
        setTimeout(function () { $banner.alert('close'); }, 3000);
    }

    // CSRF for all AJAX POSTs
    $.ajaxSetup({
        beforeSend: function (xhr, settings) {
            if (settings.type === 'POST') {
                var token = $('input[name="__RequestVerificationToken"]').first().val();
                if (token) xhr.setRequestHeader('RequestVerificationToken', token);
            }
        }
    });

});
