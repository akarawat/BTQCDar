/**
 * dar-form.js — DAR Create & Edit page
 * Handles: conditional field visibility, client-side validation
 */

$(function () {

    // Toggle DocType "Other" field
    function toggleDocTypeOther() {
        $('#dtOtherWrap').toggle($('input[name="DocType"]:checked').val() === '8');
    }
    $('input[name="DocType"]').on('change', toggleDocTypeOther);
    toggleDocTypeOther();

    // Toggle ForStandard "Others" field
    function toggleFsOther() {
        $('#fsOtherWrap').toggle($('input[name="ForStandard"]:checked').val() === '4');
    }
    $('input[name="ForStandard"]').on('change', toggleFsOther);
    toggleFsOther();

    // Toggle Purpose "Others" field
    function togglePurposeOther() {
        $('#purposeOtherWrap').toggle($('input[name="Purpose"]:checked').val() === '7');
    }
    $('input[name="Purpose"]').on('change', togglePurposeOther);
    togglePurposeOther();

    // Client-side validation before submit
    $('#frmDar').on('submit', function (e) {
        var errors = [];
        if (!$('input[name="DocType"]:checked').length)    errors.push('Please select a Type.');
        if (!$('input[name="ForStandard"]:checked').length) errors.push('Please select a Standard (For).');
        if (!$('input[name="Purpose"]:checked').length)    errors.push('Please select a Purpose.');

        if (errors.length > 0) {
            e.preventDefault();
            alert('Please complete the required fields:\n\n' + errors.join('\n'));
        }
    });

    // Character counter for Content textarea
    var $content = $('textarea[name="Content"]');
    if ($content.length) {
        var $counter = $('<small class="text-muted ms-1"></small>');
        $content.after($counter);
        $content.on('input', function () {
            $counter.text($(this).val().length + ' characters');
        }).trigger('input');
    }

});
