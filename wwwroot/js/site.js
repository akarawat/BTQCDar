/* =============================================================
   BTQCDar — site.js
   jQuery-powered helpers (loaded via _Layout.cshtml)
   No build step needed — edit & save, browser picks it up.
   ============================================================= */

$(function () {

    /* ── Auto-dismiss alerts after 5 s ─────────────────────── */
    setTimeout(function () {
        $('.alert-auto-dismiss').fadeOut(400, function () { $(this).remove(); });
    }, 5000);

    /* ── Confirm dialogs on dangerous buttons ───────────────── */
    $(document).on('click', '[data-confirm]', function (e) {
        var msg = $(this).data('confirm') || 'ยืนยันการดำเนินการนี้?';
        if (!confirm(msg)) {
            e.preventDefault();
            return false;
        }
    });

    /* ── Toggle "Other" text inputs ─────────────────────────── */
    $(document).on('change', '.radio-toggle', function () {
        var target = $(this).data('target');
        var show   = $(this).val() === $(this).data('trigger');
        $(target).toggleClass('d-none', !show);
        if (!show) $(target).find('input,textarea').val('');
    });

    /* ── DAR form: Purpose radio → show/hide Other field ────── */
    $('input[name="Purpose"]').on('change', function () {
        var isOther = $(this).val() === '7'; // DarPurpose.Others = 7
        $('#purposeOtherGroup').toggleClass('d-none', !isOther);
    });

    /* ── DAR form: DocType radio → show/hide Other field ────── */
    $('input[name="DocType"]').on('change', function () {
        var isOther = $(this).val() === '8'; // DarDocType.Other = 8
        $('#docTypeOtherGroup').toggleClass('d-none', !isOther);
    });

    /* ── DAR form: ForStandard radio → show/hide Other field ── */
    $('input[name="ForStandard"]').on('change', function () {
        var isOther = $(this).val() === '4'; // DarForStandard.Others = 4
        $('#forStandardOtherGroup').toggleClass('d-none', !isOther);
    });

    /* ── Dynamic Distribution rows (add / remove) ───────────── */
    var distRowIdx = 0;

    $(document).on('click', '#btnAddDistRow', function () {
        distRowIdx++;
        var row = $('<tr>')
            .attr('id', 'distRow_' + distRowIdx)
            .html(
                '<td><input class="form-control form-control-sm" name="DistDept_' + distRowIdx + '" placeholder="Dept/Sect" /></td>' +
                '<td></td><td></td><td></td><td></td>' +
                '<td><button type="button" class="btn btn-sm btn-outline-danger btn-remove-dist" data-row="' + distRowIdx + '">' +
                '<i class="bi bi-trash"></i></button></td>'
            );
        $('#distTableBody').append(row);
    });

    $(document).on('click', '.btn-remove-dist', function () {
        var rowId = $(this).data('row');
        $('#distRow_' + rowId).remove();
    });

    /* ── Dynamic Related Document rows ──────────────────────── */
    var relDocIdx = 0;

    $(document).on('click', '#btnAddRelDoc', function () {
        relDocIdx++;
        var row = $('<tr>')
            .attr('id', 'relDoc_' + relDocIdx)
            .html(
                '<td class="text-center">' + relDocIdx + '</td>' +
                '<td><input class="form-control form-control-sm" name="RelDocNo_' + relDocIdx + '" /></td>' +
                '<td><input class="form-control form-control-sm" name="RelDocName_' + relDocIdx + '" /></td>' +
                '<td class="text-center"><input type="checkbox" name="RelDocRevise_' + relDocIdx + '" value="true" /></td>' +
                '<td class="text-center"><input type="checkbox" name="RelDocKeep_' + relDocIdx + '" value="true" /></td>' +
                '<td><button type="button" class="btn btn-sm btn-outline-danger btn-remove-reldoc" data-row="' + relDocIdx + '">' +
                '<i class="bi bi-trash"></i></button></td>'
            );
        $('#relDocTableBody').append(row);
    });

    $(document).on('click', '.btn-remove-reldoc', function () {
        var rowId = $(this).data('row');
        $('#relDoc_' + rowId).remove();
        // Re-number items
        $('#relDocTableBody tr').each(function (idx) {
            $(this).find('td:first').text(idx + 1);
        });
    });

    /* ── Approve / Reject modal submit ──────────────────────── */
    $('#btnSubmitApprove').on('click', function () {
        $('#formApprove').submit();
    });

    $('#btnSubmitReject').on('click', function () {
        $('#formReject').submit();
    });

    $('#btnSubmitMRAgree').on('click', function () {
        $('#formMRAgree').submit();
    });

    $('#btnSubmitDCO').on('click', function () {
        $('#formDCO').submit();
    });

    /* ── Tooltip init (Bootstrap 5) ─────────────────────────── */
    var tooltipEls = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
    tooltipEls.forEach(function (el) {
        new bootstrap.Tooltip(el);
    });

});
