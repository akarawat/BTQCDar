/**
 * dar-create.js — DAR Create page
 * Pattern: cshtml → AJAX → Controller → SP → DB
 *
 * 1. DocType radio  → permission check → load Reviewer/Approver dropdowns
 * 2. Reviewer select → populate hidden fields
 * 3. Approver select → auto-select if single, populate hidden fields
 * 4. ForStandard / Purpose conditional toggles
 * 5. Form submit     → client validation → AJAX POST → redirect to Detail
 * 6. File upload     → drag & drop + browse
 */

$(function () {

    // ══════════════════════════════════════════════════════════════════
    // 1. DocType change → permission check → load dropdowns
    // ══════════════════════════════════════════════════════════════════

    $('.doc-type-radio').on('change', function () {
        var docType = $(this).val();
        clearRadioError('grpDocType', 'dtInvalidMsg');

        $.getJSON('/Dar/CheckCreatorPermission?docType=' + docType)
            .done(function (res) {
                if (!res.isAllowed) {
                    $('#notAuthorizedBanner').show();
                    $('#cardReviewerApprover').hide();
                    $('#btnSubmit').prop('disabled', true);
                    return;
                }
                $('#notAuthorizedBanner').hide();
                $('#btnSubmit').prop('disabled', false);
                $('#cardReviewerApprover').show();
                loadReviewers(docType);
                loadApprover(docType);
            })
            .fail(function () {
                // API fail — allow submit anyway (server will re-validate)
                $('#notAuthorizedBanner').hide();
                $('#btnSubmit').prop('disabled', false);
                $('#cardReviewerApprover').show();
                loadReviewers(docType);
                loadApprover(docType);
            });
    });

    // ── Load Reviewer dropdown ──────────────────────────────────────
    function loadReviewers(docType) {
        var $ddl = $('#ddlReviewer');
        $('#reviewerLoading').show();
        $ddl.prop('disabled', true)
            .empty()
            .append('<option value="">Loading...</option>');

        $.getJSON('/Dar/GetReviewers?docType=' + docType)
            .done(function (data) {
                $ddl.empty().append('<option value="">-- Select Reviewer --</option>');

                // Group by Department using optgroup
                var depts = {};
                data.forEach(function (u) {
                    var d = u.department || 'Other';
                    if (!depts[d]) depts[d] = [];
                    depts[d].push(u);
                });

                $.each(depts, function (dept, users) {
                    var $grp = $('<optgroup>').attr('label', dept);
                    users.forEach(function (u) {
                        $grp.append(
                            $('<option>').val(u.samAcc)
                                .text((u.fullName || u.samAcc) + '  (' + u.roleName + ')')
                                .data('user', u)
                        );
                    });
                    $ddl.append($grp);
                });

                $ddl.prop('disabled', false);
                // Auto-select if only 1 person
                if (data.length === 1) {
                    $ddl.val(data[0].samAcc).trigger('change');
                }
            })
            .fail(function () {
                $ddl.empty()
                    .append('<option value="">-- No reviewers found --</option>');
                $ddl.prop('disabled', false);
            })
            .always(function () { $('#reviewerLoading').hide(); });
    }

    // ── Load Approver dropdown (fixed role) ─────────────────────────
    function loadApprover(docType) {
        var $ddl = $('#ddlApprover');
        $('#approverLoading').show();
        $ddl.prop('disabled', true)
            .empty()
            .append('<option value="">Loading...</option>');
        $('#approverNote').text('');

        $.getJSON('/Dar/GetApprover?docType=' + docType)
            .done(function (data) {
                $ddl.empty().append('<option value="">-- Select Approver --</option>');
                data.forEach(function (u) {
                    $ddl.append(
                        $('<option>').val(u.samAcc)
                            .text((u.fullName || u.samAcc) + '  (' + u.roleName + ')')
                            .data('user', u)
                    );
                });

                if (data.length === 1) {
                    // Auto-select and lock
                    $ddl.val(data[0].samAcc).trigger('change');
                    $ddl.prop('disabled', true);
                    $('#approverNote').text('Auto-selected: fixed role for this document type');
                } else if (data.length > 1) {
                    $ddl.prop('disabled', false);
                    $('#approverNote').text('Multiple approvers available — please select one');
                } else {
                    $ddl.empty()
                        .append('<option value="">-- No approver configured --</option>');
                    $('#approverNote').html(
                        '<span class="text-danger">No approver assigned — contact Admin</span>');
                }
            })
            .fail(function () {
                $ddl.empty().append('<option value="">-- Error loading --</option>');
                $ddl.prop('disabled', false);
            })
            .always(function () { $('#approverLoading').hide(); });
    }

    // ══════════════════════════════════════════════════════════════════
    // 2. Reviewer change → populate hidden fields
    // ══════════════════════════════════════════════════════════════════
    $('#ddlReviewer').on('change', function () {
        var u = $(this).find('option:selected').data('user') || {};
        $('#hdnReviewerName').val(u.fullName || '');
        $('#hdnReviewerEmail').val(u.email || '');
        if ($(this).val()) $('#reviewerInvalidMsg').hide();
    });

    // ══════════════════════════════════════════════════════════════════
    // 3. Approver change → populate hidden fields
    // ══════════════════════════════════════════════════════════════════
    $('#ddlApprover').on('change', function () {
        var u = $(this).find('option:selected').data('user') || {};
        $('#hdnApproverName').val(u.fullName || '');
        $('#hdnApproverEmail').val(u.email || '');
    });

    // ══════════════════════════════════════════════════════════════════
    // 4. ForStandard / Purpose conditional toggles
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
    // 5. Form submit → client validation → AJAX POST → redirect
    // ══════════════════════════════════════════════════════════════════
    $('#frmDar').on('submit', function (e) {
        e.preventDefault(); // always intercept

        // ── Client-side validation ─────────────────────────────────
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
        if ($('#cardReviewerApprover').is(':visible') && !$('#ddlReviewer').val()) {
            $('#reviewerInvalidMsg').show(); valid = false;
        }
        $('.req-field').each(function () {
            if (!$(this).val().trim()) {
                $(this).addClass('is-invalid'); valid = false;
            } else {
                $(this).removeClass('is-invalid');
            }
        });

        if (!valid) {
            // Scroll to first error
            var $first = $('.radio-group-invalid, .is-invalid, #reviewerInvalidMsg:visible').first();
            if ($first.length)
                $('html,body').animate({ scrollTop: $first.offset().top - 100 }, 300);
            return;
        }

        // ── All valid → AJAX POST ──────────────────────────────────
        submitDar();
    });

    function submitDar() {
        // Show loading state on button
        var $btn = $('#btnSubmit');
        $btn.prop('disabled', true)
            .html('<span class="spinner-border spinner-border-sm me-1"></span>Submitting...');

        // Hide any previous error
        $('#submitError').remove();

        // Build FormData (supports file upload)
        var formData = new FormData($('#frmDar')[0]);

        // If Approver select is disabled (auto-selected), FormData won't include it
        // — add it manually from the select value
        if ($('#ddlApprover').prop('disabled') && $('#ddlApprover').val()) {
            formData.set('ApproverSamAcc', $('#ddlApprover').val());
        }

        $.ajax({
            url: '/Dar/Create',
            type: 'POST',
            data: formData,
            processData: false,    // MUST be false for FormData
            contentType: false,    // MUST be false for FormData
            success: function (res) {
                if (res.success) {
                    // Show brief success toast then redirect
                    showSuccessToast('DAR created: ' + res.darNo);
                    setTimeout(function () {
                        window.location.href = res.redirect;
                    }, 800);
                } else {
                    // Show inline error
                    showSubmitError(res.message || 'An error occurred. Please try again.');
                    resetButton($btn);
                }
            },
            error: function (xhr) {
                var msg = 'Server error (' + xhr.status + '). Please try again.';
                if (xhr.status === 400) msg = 'Validation error. Please check the form.';
                if (xhr.status === 401) msg = 'Session expired. Please refresh the page.';
                showSubmitError(msg);
                resetButton($btn);
            }
        });
    }

    function resetButton($btn) {
        $btn.prop('disabled', false)
            .html('<i class="bi bi-send me-1"></i>Submit DAR');
    }

    function showSubmitError(msg) {
        $('#submitError').remove();
        var $err = $('<div id="submitError" class="alert alert-danger d-flex align-items-center gap-2 mt-3">'
            + '<i class="bi bi-exclamation-triangle-fill fs-5"></i>'
            + '<div>' + $('<span>').text(msg).html() + '</div>'
            + '</div>');
        $('#frmDar').append($err);
        $('html,body').animate({ scrollTop: $err.offset().top - 80 }, 300);
    }

    function showSuccessToast(msg) {
        var $toast = $('<div class="position-fixed top-0 end-0 p-3" style="z-index:9999">'
            + '<div class="toast show align-items-center text-bg-success border-0">'
            + '<div class="d-flex"><div class="toast-body">'
            + '<i class="bi bi-check-circle me-1"></i>' + $('<span>').text(msg).html()
            + '</div></div></div></div>');
        $('body').append($toast);
        setTimeout(function () { $toast.remove(); }, 2000);
    }

    // Clear .is-invalid on input
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

    // ══════════════════════════════════════════════════════════════════
    // 6. File Upload — Drag & Drop + Browse
    // ══════════════════════════════════════════════════════════════════
    var MAX_MB = 20;
    var ICON_MAP = {
        pdf: 'bi-file-earmark-pdf text-danger', doc: 'bi-file-earmark-word text-primary',
        docx: 'bi-file-earmark-word text-primary', xls: 'bi-file-earmark-excel text-success',
        xlsx: 'bi-file-earmark-excel text-success', png: 'bi-file-earmark-image text-info',
        jpg: 'bi-file-earmark-image text-info', jpeg: 'bi-file-earmark-image text-info',
        zip: 'bi-file-earmark-zip text-warning'
    };

    function formatBytes(b) {
        if (b < 1024) return b + ' B';
        if (b < 1048576) return (b / 1024).toFixed(1) + ' KB';
        return (b / 1048576).toFixed(1) + ' MB';
    }

    function showFilePreview(file) {
        var ext = file.name.split('.').pop().toLowerCase();
        $('#fileIcon').attr('class', 'bi ' + (ICON_MAP[ext] || 'bi-file-earmark text-secondary') + ' fs-2');
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
        if (file.size > MAX_MB * 1024 * 1024) {
            alert('File exceeds ' + MAX_MB + ' MB limit. Please choose a smaller file.');
            clearFile();
            return;
        }
        showFilePreview(file);
    }

    $('#attachmentFile').on('change', function () { handleFile(this.files[0]); });
    $('#btnRemoveFile').on('click', clearFile);

    $('#uploadArea').on('click', function (e) {
        if (!$(e.target).is('input, label, button'))
            $('#attachmentFile').trigger('click');
    });

    $('#uploadArea')
        .on('dragover dragenter', function (e) {
            e.preventDefault(); $(this).addClass('drag-over');
        })
        .on('dragleave dragend', function (e) {
            e.preventDefault(); $(this).removeClass('drag-over');
        })
        .on('drop', function (e) {
            e.preventDefault();
            $(this).removeClass('drag-over');
            var files = e.originalEvent.dataTransfer.files;
            if (files.length > 0) {
                var dt = new DataTransfer();
                dt.items.add(files[0]);
                $('#attachmentFile')[0].files = dt.files;
                handleFile(files[0]);
            }
        });

});
