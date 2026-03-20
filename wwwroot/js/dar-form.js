/**
 * dar-form.js — DAR Create & Edit page JS
 * Handles: show/hide conditional fields, validation hints
 * Requires: jQuery 3.7+, Bootstrap 5
 */

$(function () {

    // ── Toggle DocType "Other" field ──────────────────────────────────
    function toggleDocTypeOther() {
        var val = $('input[name="DocType"]:checked').val();
        $('#dtOtherWrap').toggle(val === '8');
    }
    $('input[name="DocType"]').on('change', toggleDocTypeOther);
    toggleDocTypeOther(); // init

    // ── Toggle ForStandard "Others" field ─────────────────────────────
    function toggleFsOther() {
        var val = $('input[name="ForStandard"]:checked').val();
        $('#fsOtherWrap').toggle(val === '4');
    }
    $('input[name="ForStandard"]').on('change', toggleFsOther);
    toggleFsOther();

    // ── Toggle Purpose "Others" field ─────────────────────────────────
    function togglePurposeOther() {
        var val = $('input[name="Purpose"]:checked').val();
        $('#purposeOtherWrap').toggle(val === '7');
    }
    $('input[name="Purpose"]').on('change', togglePurposeOther);
    togglePurposeOther();

    // ── Controlled / Uncontrolled mutual hint (not exclusive) ─────────
    $('#chkCtrl, #chkUnctrl').on('change', function () {
        // Both can be checked; just highlight if none selected
        var anyChecked = $('#chkCtrl').is(':checked') || $('#chkUnctrl').is(':checked');
        $('#chkCtrl, #chkUnctrl').toggleClass('is-invalid', !anyChecked);
    });

    // ── Client-side required fields check before submit ───────────────
    $('#frmDar').on('submit', function (e) {
        var docType  = $('input[name="DocType"]:checked').length;
        var forStd   = $('input[name="ForStandard"]:checked').length;
        var purpose  = $('input[name="Purpose"]:checked').length;
        var errors   = [];

        if (!docType)  errors.push('กรุณาเลือก Type');
        if (!forStd)   errors.push('กรุณาเลือก For (มาตรฐาน)');
        if (!purpose)  errors.push('กรุณาเลือก Purpose');

        if (errors.length > 0) {
            e.preventDefault();
            alert('⚠️ กรุณากรอกข้อมูลให้ครบ:\n' + errors.join('\n'));
        }
    });

    // ── Character counter for Content textarea ────────────────────────
    var $content = $('textarea[name="Content"]');
    if ($content.length) {
        var $counter = $('<small class="text-muted ms-1"></small>');
        $content.after($counter);
        $content.on('input', function () {
            $counter.text($(this).val().length + ' ตัวอักษร');
        }).trigger('input');
    }

});
