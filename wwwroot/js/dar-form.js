/**
 * dar-form.js — DAR Create & Edit page
 * Features:
 *   - Conditional field show/hide
 *   - Red highlight on required radio groups when left empty
 *   - Drag-and-drop / browse file upload with preview
 */

$(function () {

    // ══════════════════════════════════════════════════════════════════
    // 1. Conditional field toggles
    // ══════════════════════════════════════════════════════════════════

    function toggleDocTypeOther() {
        var val = $('input[name="DocType"]:checked').val();
        $('#dtOtherWrap').toggle(val === '8');
        if (val) clearRadioError('grpDocType', 'dtInvalidMsg');
    }
    $('input[name="DocType"]').on('change', toggleDocTypeOther);
    toggleDocTypeOther();

    function toggleFsOther() {
        var val = $('input[name="ForStandard"]:checked').val();
        $('#fsOtherWrap').toggle(val === '4');
        if (val) clearRadioError('grpForStandard', 'fsInvalidMsg');
    }
    $('input[name="ForStandard"]').on('change', toggleFsOther);
    toggleFsOther();

    function togglePurposeOther() {
        var val = $('input[name="Purpose"]:checked').val();
        $('#purposeOtherWrap').toggle(val === '7');
        if (val) clearRadioError('grpPurpose', 'purposeInvalidMsg');
    }
    $('input[name="Purpose"]').on('change', togglePurposeOther);
    togglePurposeOther();

    // ══════════════════════════════════════════════════════════════════
    // 2. Required field red-highlight helpers
    // ══════════════════════════════════════════════════════════════════

    function markRadioError(groupId, msgId) {
        $('#' + groupId).addClass('radio-group-invalid');
        $('#' + msgId).show();
    }

    function clearRadioError(groupId, msgId) {
        $('#' + groupId).removeClass('radio-group-invalid');
        $('#' + msgId).hide();
    }

    // ══════════════════════════════════════════════════════════════════
    // 3. Form submit validation
    // ══════════════════════════════════════════════════════════════════

    $('#frmDar').on('submit', function (e) {
        var valid = true;

        // Radio groups
        if (!$('input[name="DocType"]:checked').length) {
            markRadioError('grpDocType', 'dtInvalidMsg');
            valid = false;
        }
        if (!$('input[name="ForStandard"]:checked').length) {
            markRadioError('grpForStandard', 'fsInvalidMsg');
            valid = false;
        }
        if (!$('input[name="Purpose"]:checked').length) {
            markRadioError('grpPurpose', 'purposeInvalidMsg');
            valid = false;
        }

        // Text / textarea required fields — Bootstrap's .is-invalid
        $('.req-field').each(function () {
            if (!$(this).val().trim()) {
                $(this).addClass('is-invalid');
                valid = false;
            } else {
                $(this).removeClass('is-invalid');
            }
        });

        if (!valid) {
            e.preventDefault();
            // Scroll to first error
            var $first = $('.radio-group-invalid, .is-invalid').first();
            if ($first.length) {
                $('html, body').animate({ scrollTop: $first.offset().top - 100 }, 300);
            }
        }
    });

    // Clear .is-invalid on input
    $(document).on('input change', '.req-field', function () {
        if ($(this).val().trim()) $(this).removeClass('is-invalid');
    });

    // Character counter for Content textarea
    var $content = $('textarea[name="Content"]');
    if ($content.length) {
        var $counter = $('<small class="text-muted ms-1 float-end"></small>');
        $content.closest('.col-12').find('label').first().append($counter);
        $content.on('input', function () {
            $counter.text($(this).val().length + ' chars');
        }).trigger('input');
    }

    // ══════════════════════════════════════════════════════════════════
    // 4. File Upload — Drag & Drop + Browse
    // ══════════════════════════════════════════════════════════════════

    var MAX_MB  = 20;
    var $area   = $('#uploadArea');
    var $input  = $('#attachmentFile');
    var $preview = $('#filePreview');
    var $hdnHas  = $('#hdnHasAttachment');

    var ICON_MAP = {
        pdf  : 'bi-file-earmark-pdf text-danger',
        doc  : 'bi-file-earmark-word text-primary',
        docx : 'bi-file-earmark-word text-primary',
        xls  : 'bi-file-earmark-excel text-success',
        xlsx : 'bi-file-earmark-excel text-success',
        png  : 'bi-file-earmark-image text-info',
        jpg  : 'bi-file-earmark-image text-info',
        jpeg : 'bi-file-earmark-image text-info',
        zip  : 'bi-file-earmark-zip text-warning',
    };

    function formatBytes(bytes) {
        if (bytes < 1024)       return bytes + ' B';
        if (bytes < 1048576)    return (bytes / 1024).toFixed(1) + ' KB';
        return (bytes / 1048576).toFixed(1) + ' MB';
    }

    function showPreview(file) {
        var ext = file.name.split('.').pop().toLowerCase();
        var iconClass = ICON_MAP[ext] || 'bi-file-earmark text-secondary';

        $('#fileIcon').attr('class', 'bi ' + iconClass + ' fs-2');
        $('#fileName').text(file.name);
        $('#fileSize').text(formatBytes(file.size));

        $preview.show();
        $area.addClass('has-file');
        $hdnHas.val('true');
    }

    function clearFile() {
        $input.val('');
        $preview.hide();
        $area.removeClass('has-file');
        $hdnHas.val('false');
    }

    function handleFile(file) {
        if (!file) return;
        if (file.size > MAX_MB * 1024 * 1024) {
            alert('File size exceeds ' + MAX_MB + ' MB limit. Please choose a smaller file.');
            clearFile();
            return;
        }
        showPreview(file);
    }

    // File input change
    $input.on('change', function () {
        handleFile(this.files[0]);
    });

    // Remove button
    $('#btnRemoveFile').on('click', function () {
        clearFile();
    });

    // Click on upload area triggers file browse
    $area.on('click', function (e) {
        if (!$(e.target).is('input, label, button')) {
            $input.trigger('click');
        }
    });

    // Drag & Drop
    $area.on('dragover dragenter', function (e) {
        e.preventDefault();
        e.stopPropagation();
        $area.addClass('drag-over');
    });

    $area.on('dragleave dragend', function (e) {
        e.preventDefault();
        $area.removeClass('drag-over');
    });

    $area.on('drop', function (e) {
        e.preventDefault();
        e.stopPropagation();
        $area.removeClass('drag-over');

        var files = e.originalEvent.dataTransfer.files;
        if (files.length > 0) {
            // Transfer to the real file input
            var dt = new DataTransfer();
            dt.items.add(files[0]);
            $input[0].files = dt.files;
            handleFile(files[0]);
        }
    });

});
