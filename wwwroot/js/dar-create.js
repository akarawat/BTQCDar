/**
 * dar-create.js — DAR Create page
 * Handles:
 *   1. DocType radio → permission check + load Reviewer/Approver dropdowns
 *   2. Reviewer dropdown → populate hidden fields
 *   3. Approver dropdown → auto-select if single, populate hidden fields
 *   4. Form validation (required fields + radio groups)
 *   5. File upload drag & drop
 */

$(function () {

    // ══════════════════════════════════════════════════════════════════
    // 1. DocType change → check permission → load dropdowns
    // ══════════════════════════════════════════════════════════════════

    $('.doc-type-radio').on('change', function () {
        var docType = $(this).val();
        clearRadioError('grpDocType', 'dtInvalidMsg');

        // Permission check
        $.getJSON('/Dar/CheckCreatorPermission?docType=' + docType, function (res) {
            if (!res.isAllowed) {
                $('#notAuthorizedBanner').show();
                $('#cardReviewerApprover').hide();
                $('#btnSubmit').prop('disabled', true);
                return;
            }
            $('#notAuthorizedBanner').hide();
            $('#btnSubmit').prop('disabled', false);
            loadReviewers(docType);
            loadApprover(docType);
            $('#cardReviewerApprover').show();
        }).fail(function () {
            // If API fails, still show dropdowns (non-fatal)
            $('#notAuthorizedBanner').hide();
            $('#btnSubmit').prop('disabled', false);
            loadReviewers(docType);
            loadApprover(docType);
            $('#cardReviewerApprover').show();
        });
    });

    // ── Load Reviewers ──────────────────────────────────────────────
    function loadReviewers(docType) {
        var $ddl = $('#ddlReviewer');
        $('#reviewerLoading').show();
        $ddl.prop('disabled', true).empty().append('<option value="">Loading...</option>');

        $.getJSON('/Dar/GetReviewers?docType=' + docType, function (data) {
            $ddl.empty().append('<option value="">-- Select Reviewer --</option>');

            // Group by department
            var depts = {};
            data.forEach(function (u) {
                if (!depts[u.department]) depts[u.department] = [];
                depts[u.department].push(u);
            });

            $.each(depts, function (dept, users) {
                var $grp = $('<optgroup>').attr('label', dept);
                users.forEach(function (u) {
                    $grp.append(
                        $('<option>').val(u.samAcc)
                            .text((u.fullName || u.samAcc) + ' (' + u.roleName + ')')
                            .data('user', u)
                    );
                });
                $ddl.append($grp);
            });

            $ddl.prop('disabled', false);
            // Auto-select if only one
            if (data.length === 1) {
                $ddl.val(data[0].samAcc).trigger('change');
            }
        }).fail(function () {
            $ddl.empty().append('<option value="">-- No reviewers found --</option>');
            $ddl.prop('disabled', false);
        }).always(function () {
            $('#reviewerLoading').hide();
        });
    }

    // ── Load Approver (fixed role, auto-select) ─────────────────────
    function loadApprover(docType) {
        var $ddl = $('#ddlApprover');
        $('#approverLoading').show();
        $ddl.prop('disabled', true).empty().append('<option value="">Loading...</option>');
        $('#approverNote').text('');

        $.getJSON('/Dar/GetApprover?docType=' + docType, function (data) {
            $ddl.empty().append('<option value="">-- Select Approver --</option>');
            data.forEach(function (u) {
                $ddl.append(
                    $('<option>').val(u.samAcc)
                        .text((u.fullName || u.samAcc) + ' (' + u.roleName + ')')
                        .data('user', u)
                );
            });

            // Auto-select if only one person (typical for MD/Manager role)
            if (data.length === 1) {
                $ddl.val(data[0].samAcc).trigger('change');
                $ddl.prop('disabled', true); // lock — fixed role, no choice
                $('#approverNote').text('Auto-selected: fixed role for this document type');
            } else if (data.length > 1) {
                $ddl.prop('disabled', false);
                $('#approverNote').text('Multiple approvers available — please select one');
            } else {
                $ddl.empty().append('<option value="">-- No approver configured --</option>');
                $('#approverNote').text('Warning: no approver assigned for this document type');
            }
        }).fail(function () {
            $ddl.empty().append('<option value="">-- Error loading --</option>');
            $ddl.prop('disabled', false);
        }).always(function () {
            $('#approverLoading').hide();
        });
    }

    // ══════════════════════════════════════════════════════════════════
    // 2. Reviewer change → populate hidden fields
    // ══════════════════════════════════════════════════════════════════
    $('#ddlReviewer').on('change', function () {
        var $opt = $(this).find('option:selected');
        var u    = $opt.data('user') || {};
        $('#hdnReviewerName').val(u.fullName  || '');
        $('#hdnReviewerEmail').val(u.email    || '');
        if ($(this).val()) clearFieldError('reviewerInvalidMsg');
    });

    // ══════════════════════════════════════════════════════════════════
    // 3. Approver change → populate hidden fields
    // ══════════════════════════════════════════════════════════════════
    $('#ddlApprover').on('change', function () {
        var $opt = $(this).find('option:selected');
        var u    = $opt.data('user') || {};
        $('#hdnApproverName').val(u.fullName  || '');
        $('#hdnApproverEmail').val(u.email    || '');
    });

    // ══════════════════════════════════════════════════════════════════
    // 4. ForStandard / Purpose toggles
    // ══════════════════════════════════════════════════════════════════
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
    // 5. Form submit validation
    // ══════════════════════════════════════════════════════════════════
    $('#frmDar').on('submit', function (e) {
        var valid = true;

        if (!$('.doc-type-radio:checked').length) {
            markRadioError('grpDocType', 'dtInvalidMsg'); valid = false;
        }
        if (!$('input[name="ForStandard"]:checked').length) {
            markRadioError('grpForStandard', 'fsInvalidMsg'); valid = false;
        }
        if (!$('input[name="Purpose"]:checked').length) {
            markRadioError('grpPurpose', 'purposeInvalidMsg'); valid = false;
        }

        // Reviewer required
        if ($('#cardReviewerApprover').is(':visible') && !$('#ddlReviewer').val()) {
            $('#reviewerInvalidMsg').show(); valid = false;
        }

        // Text/textarea required fields
        $('.req-field').each(function () {
            if (!$(this).val().trim()) {
                $(this).addClass('is-invalid'); valid = false;
            } else {
                $(this).removeClass('is-invalid');
            }
        });

        if (!valid) {
            e.preventDefault();
            var $first = $('.radio-group-invalid, .is-invalid, #reviewerInvalidMsg:visible').first();
            if ($first.length)
                $('html,body').animate({ scrollTop: $first.offset().top - 100 }, 300);
        }
    });

    $(document).on('input change', '.req-field', function () {
        if ($(this).val().trim()) $(this).removeClass('is-invalid');
    });

    // ══════════════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════════════
    function markRadioError(groupId, msgId) {
        $('#' + groupId).addClass('radio-group-invalid');
        $('#' + msgId).show();
    }
    function clearRadioError(groupId, msgId) {
        $('#' + groupId).removeClass('radio-group-invalid');
        $('#' + msgId).hide();
    }
    function clearFieldError(msgId) {
        $('#' + msgId).hide();
    }

    // ══════════════════════════════════════════════════════════════════
    // 6. File Upload drag & drop
    // ══════════════════════════════════════════════════════════════════
    var MAX_MB   = 20;
    var ICON_MAP = { pdf:'bi-file-earmark-pdf text-danger', doc:'bi-file-earmark-word text-primary',
                     docx:'bi-file-earmark-word text-primary', xls:'bi-file-earmark-excel text-success',
                     xlsx:'bi-file-earmark-excel text-success', png:'bi-file-earmark-image text-info',
                     jpg:'bi-file-earmark-image text-info', jpeg:'bi-file-earmark-image text-info',
                     zip:'bi-file-earmark-zip text-warning' };

    function formatBytes(b) {
        if (b<1024) return b+' B';
        if (b<1048576) return (b/1024).toFixed(1)+' KB';
        return (b/1048576).toFixed(1)+' MB';
    }
    function showFilePreview(file) {
        var ext = file.name.split('.').pop().toLowerCase();
        $('#fileIcon').attr('class','bi '+(ICON_MAP[ext]||'bi-file-earmark text-secondary')+' fs-2');
        $('#fileName').text(file.name);
        $('#fileSize').text(formatBytes(file.size));
        $('#filePreview').show();
        $('#uploadArea').addClass('has-file');
        $('#hdnHasAttachment').val('true');
    }
    function clearFile() {
        $('#attachmentFile').val('');
        $('#filePreview').hide();
        $('#uploadArea').removeClass('has-file');
        $('#hdnHasAttachment').val('false');
    }
    function handleFile(file) {
        if (!file) return;
        if (file.size > MAX_MB*1024*1024) {
            alert('File exceeds '+MAX_MB+' MB limit.'); clearFile(); return;
        }
        showFilePreview(file);
    }

    $('#attachmentFile').on('change', function () { handleFile(this.files[0]); });
    $('#btnRemoveFile').on('click', clearFile);
    $('#uploadArea').on('click', function (e) {
        if (!$(e.target).is('input,label,button')) $('#attachmentFile').trigger('click');
    });
    $('#uploadArea').on('dragover dragenter', function (e) {
        e.preventDefault(); $(this).addClass('drag-over');
    }).on('dragleave dragend', function (e) {
        e.preventDefault(); $(this).removeClass('drag-over');
    }).on('drop', function (e) {
        e.preventDefault(); $(this).removeClass('drag-over');
        var files = e.originalEvent.dataTransfer.files;
        if (files.length > 0) {
            var dt = new DataTransfer();
            dt.items.add(files[0]);
            $('#attachmentFile')[0].files = dt.files;
            handleFile(files[0]);
        }
    });

});
