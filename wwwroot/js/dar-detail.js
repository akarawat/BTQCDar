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
        $('#dcoRemarksWrap').hide();
        $('#dcoRemarks').val('');
        $('#dcoDate').val('');
        $('#modalActionError').remove();

        // Modal title + confirm button colour
        var titles = {
            'sign-review': 'Sign & Forward to Approver',
            'sign-approve': 'Sign & Approve',
            'review': 'Review & Forward to Approver',
            'approve': 'Approve DAR',
            'reject': 'Reject DAR',
            'sign-mr-agree': 'Sign & Agree (QMR)',
            'sign-dco-register': 'Sign & Complete (DCC)',
            'mr-agree': 'QMR — Agree',
            'mr-disagree': 'QMR — Not Agree',
            'dco-register': 'DCC — Mark as Completed'
        };
        $('#modalTitle').text(titles[action] || 'Confirm Action');

        var isPositive = ['sign-review', 'sign-approve', 'sign-mr-agree', 'sign-dco-register', 'review', 'approve', 'mr-agree', 'dco-register'].indexOf(action) > -1;
        $('#btnConfirmAction')
            .removeClass('btn-success btn-danger btn-primary')
            .addClass(isPositive ? 'btn-success' : 'btn-danger')
            .prop('disabled', false)
            .text('Confirm');

        // DCO date picker
        if (action === 'dco-register' || action === 'sign-dco-register') {
            $('#dcoDateWrap').show();
            $('#dcoRemarksWrap').show();
            $('#dcoDate').val(new Date().toISOString().split('T')[0]);
        }

        // Remarks hint per action
        var hints = {
            'sign-review': 'Remarks for digital signature (optional)',
            'sign-approve': 'Remarks for digital signature (optional)',
            'review': 'Review comments (optional)',
            'reject': 'Please provide a reason for rejection.',
            'approve': 'Approval remarks (optional)',
            'sign-mr-agree': 'Remarks for QMR digital signature (optional)',
            'sign-dco-register': 'Remarks for DCC digital signature (optional)',
            'mr-agree': 'QMR agreement remarks (optional)',
            'mr-disagree': 'Please provide a reason for not agreeing.',
            'dco-register': 'Document registration remarks (optional)'
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

        // Reject / not-agree requires a reason
        if ((action === 'reject' || action === 'mr-disagree') && !remarks) {
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
            case 'sign-mr-agree':
                url = '/Dar/SignMRAgree';
                postData = { id: id, remarks: remarks };
                break;
            case 'sign-dco-register':
                var dcoDateSign = $('#dcoDate').val();
                if (!dcoDateSign) { showModalError('Please enter the Doc Registered Date.'); return; }
                url = '/Dar/SignDCORegister';
                postData = {
                    id: id, registeredDate: dcoDateSign,
                    dcoRemarks: $('#dcoRemarks').val().trim()
                };
                break;
            case 'dco-register':
                var dcoDate = $('#dcoDate').val();
                if (!dcoDate) { showModalError('Please enter the Doc Registered Date.'); return; }
                url = '/Dar/DCORegister';
                postData = {
                    id: id, registeredDate: dcoDate,
                    dcoRemarks: $('#dcoRemarks').val().trim(),
                    remarks: remarks
                };
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

    // ── Audit Log button → load from API and show modal ─────────────────
    $('#btnAuditLog').on('click', function () {
        var darNo = $(this).data('darno');
        var $body = $('#auditModalBody');
        var $modal = new bootstrap.Modal($('#auditModal')[0]);

        $('#auditDarNo').text('— ' + darNo);
        $body.html('<div class="text-center py-4 text-muted">' +
            '<div class="spinner-border spinner-border-sm me-2"></div>Loading...</div>');
        $modal.show();

        $.getJSON('/Dar/AuditLog?darNo=' + encodeURIComponent(darNo))
            .done(function (res) {
                if (!res.success || !res.data || res.data.records.length === 0) {
                    $body.html('<div class="text-center py-4 text-muted">' +
                        '<i class="bi bi-inbox me-2"></i>No audit records found.</div>');
                    return;
                }

                var records = res.data.records;
                var html = '<div class="table-responsive">' +
                    '<table class="table table-sm table-hover small mb-0">' +
                    '<thead class="table-dark"><tr>' +
                    '<th>#</th><th>Purpose</th><th>Signed By</th>' +
                    '<th>Signed At</th><th>Algorithm</th><th>Status</th>' +
                    '</tr></thead><tbody>';

                records.forEach(function (r, i) {
                    var signedAt = r.signedAt
                        ? new Date(r.signedAt).toLocaleString('th-TH', { hour12: false })
                        : '—';
                    var user = r.signerFullName || r.signedByUser || '—';
                    var status = r.isRevoked
                        ? '<span class="badge bg-danger">Revoked</span>'
                        : '<span class="badge bg-success">Valid</span>';
                    var purpose = r.purpose || '—';

                    html += '<tr>' +
                        '<td class="text-muted">' + (i + 1) + '</td>' +
                        '<td>' + $('<span>').text(purpose).html() + '</td>' +
                        '<td>' + $('<span>').text(user).html() + '</td>' +
                        '<td class="text-nowrap">' + signedAt + '</td>' +
                        '<td><code class="small">' + (r.signatureType || '—') + '</code></td>' +
                        '<td>' + status + '</td>' +
                        '</tr>' +
                        // Expanded row: cert + thumbprint + hash
                        '<tr class="table-light">' +
                        '<td></td>' +
                        '<td colspan="5" class="py-1">' +
                        '<span class="text-muted me-2">Cert:</span>' +
                        $('<span>').text(r.signedByCert || '—').html() +
                        ' &nbsp;|&nbsp; <span class="text-muted me-1">Thumbprint:</span>' +
                        '<code class="small text-break">' + (r.certThumbprint || '—') + '</code>' +
                        (r.remarks ? ' &nbsp;|&nbsp; <span class="text-muted me-1">Remarks:</span>' +
                            $('<span>').text(r.remarks).html() : '') +
                        '</td></tr>';
                });

                html += '</tbody></table></div>';
                html += '<div class="px-3 py-2 border-top bg-light small text-muted">' +
                    'Total: <strong>' + res.data.total + '</strong> record(s)</div>';
                $body.html(html);
            })
            .fail(function () {
                $body.html('<div class="text-center py-4 text-danger">' +
                    '<i class="bi bi-exclamation-circle me-2"></i>' +
                    'Failed to load audit log. Please try again.</div>');
            });
    });

    // ── Certificate badge click → show cert detail modal ────────────────
    $(document).on('click', '.cert-badge', function () {
        var $b = $(this);
        $('#cert-role').text($b.data('role') || '—');
        $('#cert-name').text($b.data('name') || '—');
        $('#cert-samacc').text($b.data('samacc') || '—');
        $('#cert-signedat').text($b.data('signedat') || '—');
        $('#cert-thumbprint').text($b.data('thumbprint') || '—');

        var $certModalEl = $('#certModal');
        if ($certModalEl.length) {
            new bootstrap.Modal($certModalEl[0]).show();
        }
    });

    // CSRF for all AJAX POSTs
    $.ajaxSetup({
        beforeSend: function (xhr, settings) {
            if (settings.type === 'POST') {
                var token = $('input[name="__RequestVerificationToken"]').first().val();
                if (token) xhr.setRequestHeader('RequestVerificationToken', token);
            }
        }
    });

    // ── Related Documents Identification and Verification ──────────────
    var $relTbl = $('#tblRelatedDocs');
    if ($relTbl.length) {
        var relDarId  = parseInt($relTbl.data('dar-id'));
        var relCanEdit = $relTbl.data('can-edit') === true || $relTbl.data('can-edit') === 'true';

        loadRelatedDocs();

        function loadRelatedDocs() {
            $.getJSON('/Dar/GetRelatedDocs', { darId: relDarId }, function (data) {
                renderRelatedDocs(data || []);
            }).fail(function () {
                $('#tblRelatedDocsBody').html('<tr><td colspan="6" class="text-center text-danger py-3">'
                    + 'Failed to load related documents.</td></tr>');
            });
        }

        function renderRelatedDocs(rows) {
            var $body = $('#tblRelatedDocsBody').empty();
            var colspan = relCanEdit ? 6 : 5;

            if (!rows.length) {
                $body.html('<tr><td colspan="' + colspan + '" class="text-center text-muted py-3">'
                    + 'No related documents.</td></tr>');
                return;
            }

            rows.forEach(function (r, i) {
                $body.append(buildRelatedDocRow(r, i + 1));
            });
        }

        function esc(s) { return $('<span>').text(s || '').html(); }

        function buildRelatedDocRow(r, displayItem) {
            var $tr = $('<tr>').data('reldocid', r.relDocId).data('itemno', r.itemNo || displayItem);

            if (relCanEdit) {
                $tr.append($('<td>').text(r.itemNo || displayItem));
                $tr.append($('<td>').html('<input type="text" class="form-control form-control-sm rd-docno" value="'
                    + esc(r.documentNo) + '" maxlength="200">'));
                $tr.append($('<td>').html('<input type="text" class="form-control form-control-sm rd-docname" value="'
                    + esc(r.documentName) + '" maxlength="512">'));
                $tr.append($('<td class="text-center">').html('<input type="checkbox" class="form-check-input rd-revise"'
                    + (r.isRevise ? ' checked' : '') + '>'));
                $tr.append($('<td class="text-center">').html('<input type="checkbox" class="form-check-input rd-keepasis"'
                    + (r.isKeepAsIs ? ' checked' : '') + '>'));
                $tr.append($('<td class="text-center">').html(
                    '<button type="button" class="btn btn-sm btn-outline-success py-0 me-1 rd-save" title="Save">'
                    + '<i class="bi bi-check-lg"></i></button>'
                    + '<button type="button" class="btn btn-sm btn-outline-danger py-0 rd-delete" title="Delete">'
                    + '<i class="bi bi-trash"></i></button>'));
            } else {
                $tr.append($('<td>').text(r.itemNo || displayItem));
                $tr.append($('<td>').text(r.documentNo));
                $tr.append($('<td>').text(r.documentName));
                $tr.append($('<td class="text-center">').html(r.isRevise
                    ? '<i class="bi bi-check-lg text-success"></i>' : '<span class="text-muted">—</span>'));
                $tr.append($('<td class="text-center">').html(r.isKeepAsIs
                    ? '<i class="bi bi-check-lg text-success"></i>' : '<span class="text-muted">—</span>'));
            }
            return $tr;
        }

        if (relCanEdit) {
            // Add a new blank editable row (RelDocId=0 until saved)
            $('#btnAddRelatedDoc').on('click', function () {
                $('#tblRelatedDocsBody tr td.text-center.text-muted').closest('tr').remove();
                var nextItem = $('#tblRelatedDocsBody tr').length + 1;
                var $row = buildRelatedDocRow({ relDocId: 0, itemNo: nextItem,
                    documentNo: '', documentName: '', isRevise: false, isKeepAsIs: false }, nextItem);
                $('#tblRelatedDocsBody').append($row);
                $row.find('.rd-docno').trigger('focus');
            });

            // Save row (insert or update)
            $(document).on('click', '.rd-save', function () {
                var $row = $(this).closest('tr');
                var docNo = $row.find('.rd-docno').val().trim();
                var docName = $row.find('.rd-docname').val().trim();

                if (!docNo && !docName) {
                    showPageSuccess('Please enter Document No or Document Name.');
                    return;
                }

                var $btn = $(this).prop('disabled', true);
                $.post('/Dar/SaveRelatedDoc', {
                    relDocId: $row.data('reldocid') || 0,
                    darId: relDarId,
                    itemNo: $row.data('itemno'),
                    documentNo: docNo,
                    documentName: docName,
                    isRevise: $row.find('.rd-revise').prop('checked'),
                    isKeepAsIs: $row.find('.rd-keepasis').prop('checked')
                })
                .done(function (res) {
                    if (res.success) {
                        $row.data('reldocid', res.relDocId);
                        showPageSuccess('Related document saved.');
                    } else {
                        showPageSuccess('Error: ' + (res.message || 'Unknown error'));
                    }
                })
                .fail(function () { showPageSuccess('Server error. Please try again.'); })
                .always(function () { $btn.prop('disabled', false); });
            });

            // Delete row
            $(document).on('click', '.rd-delete', function () {
                var $row = $(this).closest('tr');
                var relDocId = $row.data('reldocid');

                if (!relDocId) { $row.remove(); return; } // unsaved row — just remove client-side
                if (!confirm('Remove this related document?')) return;

                $.post('/Dar/DeleteRelatedDoc', { relDocId: relDocId })
                    .done(function (res) {
                        if (res.success) {
                            $row.remove();
                            showPageSuccess('Related document removed.');
                        } else {
                            showPageSuccess('Error: ' + (res.message || 'Unknown error'));
                        }
                    })
                    .fail(function () { showPageSuccess('Server error. Please try again.'); });
            });
        }
    }

});
