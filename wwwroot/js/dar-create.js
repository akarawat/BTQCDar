/**
 * dar-create.js — DAR Create page
 * Pattern: cshtml → AJAX → Controller → SP → DB
 *
 * DocType 1–6  : ผ่าน Flow (CheckCreatorPermission + SP dropdown)
 * DocType 7–8  : ไม่ผ่าน Flow — เลือก Reviewer/Approver จาก All Users โดยตรง
 */

$(function () {

    // DocType ที่ไม่ต้องผ่าน Flow approval
    var FREE_TYPES = [7, 8];

    // ══════════════════════════════════════════════════════════════════
    // 1. DocType change
    // ══════════════════════════════════════════════════════════════════
    $('.doc-type-radio').on('change', function () {
        var docType = parseInt($(this).val());
        clearRadioError('grpDocType', 'dtInvalidMsg');

        // Show/hide ForStandard section — dt7/dt8 ไม่บังคับ
        if (FREE_TYPES.indexOf(docType) >= 0) {
            $('#forStandardSection').hide();
            $('input[name="ForStandard"]').prop('checked', false);
            clearRadioError('grpForStandard', 'fsInvalidMsg');
        } else {
            $('#forStandardSection').show();
        }

        // Load dropdowns
        $('#cardReviewerApprover').show();
        resetReviewerApprover();

        if (FREE_TYPES.indexOf(docType) >= 0) {
            // dt7/dt8 — ไม่ check permission, load all users
            loadAllUsersForReviewer();
            loadAllUsersForApprover();
        } else {
            // dt1–6 — check permission first, then load by SP
            checkPermissionAndLoad(docType);
        }
    });

    // ── Check permission → load (for dt1–6) ────────────────────────
    function checkPermissionAndLoad(docType) {
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
                loadReviewersByDocType(docType);
                loadApproverByDocType(docType);
            })
            .fail(function () {
                $('#notAuthorizedBanner').hide();
                $('#btnSubmit').prop('disabled', false);
                loadReviewersByDocType(docType);
                loadApproverByDocType(docType);
            });
    }

    // ── Reset dropdowns ─────────────────────────────────────────────
    function resetReviewerApprover() {
        $('#notAuthorizedBanner').hide();
        $('#btnSubmit').prop('disabled', false);
        $('#hdnReviewerName, #hdnReviewerEmail').val('');
        $('#hdnApproverName, #hdnApproverEmail').val('');
        $('#approverNote').text('');
    }

    // ══════════════════════════════════════════════════════════════════
    // 2. Load Reviewer — by DocType SP (dt1–6)
    // ══════════════════════════════════════════════════════════════════
    function loadReviewersByDocType(docType) {
        fillDropdown('#ddlReviewer', '#reviewerLoading',
            '/Dar/GetReviewers?docType=' + docType,
            '-- Select Reviewer --', false);
    }

    // ══════════════════════════════════════════════════════════════════
    // 3. Load Approver — fixed role SP (dt1–6)
    // ══════════════════════════════════════════════════════════════════
    function loadApproverByDocType(docType) {
        fillDropdown('#ddlApprover', '#approverLoading',
            '/Dar/GetApprover?docType=' + docType,
            '-- Select Approver --', true);
    }

    // ══════════════════════════════════════════════════════════════════
    // 4. Load All Users — สำหรับ dt7/dt8 (free selection)
    // ══════════════════════════════════════════════════════════════════
    function loadAllUsersForReviewer() {
        fillDropdown('#ddlReviewer', '#reviewerLoading',
            '/Admin/GetAllUsers',
            '-- Select Reviewer --', false, mapAdUser);
    }

    function loadAllUsersForApprover() {
        fillDropdown('#ddlApprover', '#approverLoading',
            '/Admin/GetAllUsers',
            '-- Select Approver --', false, mapAdUser);
    }

    // Map ADUserModel (from GetAllUsers) to same shape as UserDropdownModel
    function mapAdUser(u) {
        return {
            samAcc: u.samAcc,
            fullName: u.fullName,
            email: u.email,
            depCode: u.depCode,
            department: u.department,
            roleType: u.roleType || 0,
            roleName: u.roleName || ''
        };
    }

    // ══════════════════════════════════════════════════════════════════
    // Generic dropdown filler (groups by department)
    // ══════════════════════════════════════════════════════════════════
    function fillDropdown($sel, $loading, url, placeholder, autoLockIfOne, mapFn) {
        var $ddl = $($sel);
        $($loading).show();
        $ddl.prop('disabled', true)
            .empty()
            .append('<option value="">Loading...</option>');

        $.getJSON(url)
            .done(function (data) {
                // Apply mapper if provided (for ADUserModel → UserDropdownModel shape)
                if (mapFn) data = data.map(mapFn);

                $ddl.empty().append('<option value="">' + placeholder + '</option>');

                // Group by department
                var depts = {};
                data.forEach(function (u) {
                    var d = u.department || 'Other';
                    if (!depts[d]) depts[d] = [];
                    depts[d].push(u);
                });

                $.each(depts, function (dept, users) {
                    var $grp = $('<optgroup>').attr('label', dept);
                    users.forEach(function (u) {
                        var label = (u.fullName || u.samAcc);
                        if (u.roleName) label += '  (' + u.roleName + ')';
                        $grp.append(
                            $('<option>').val(u.samAcc)
                                .text(label)
                                .data('user', u)
                        );
                    });
                    $ddl.append($grp);
                });

                $ddl.prop('disabled', false);

                // Auto-select if only 1 user
                if (data.length === 1) {
                    $ddl.val(data[0].samAcc).trigger('change');
                    if (autoLockIfOne) {
                        $ddl.prop('disabled', true);
                        $('#approverNote').text('Auto-selected: fixed role for this document type');
                    }
                } else if (autoLockIfOne && data.length > 1) {
                    $('#approverNote').text('Multiple approvers — please select one');
                }
            })
            .fail(function () {
                $ddl.empty().append('<option value="">-- Error loading --</option>');
                $ddl.prop('disabled', false);
            })
            .always(function () { $($loading).hide(); });
    }

    // ══════════════════════════════════════════════════════════════════
    // 5. Reviewer / Approver change → populate hidden fields
    // ══════════════════════════════════════════════════════════════════
    $('#ddlReviewer').on('change', function () {
        var u = $(this).find('option:selected').data('user') || {};
        $('#hdnReviewerName').val(u.fullName || '');
        $('#hdnReviewerEmail').val(u.email || '');
        if ($(this).val()) $('#reviewerInvalidMsg').hide();
    });

    $('#ddlApprover').on('change', function () {
        var u = $(this).find('option:selected').data('user') || {};
        $('#hdnApproverName').val(u.fullName || '');
        $('#hdnApproverEmail').val(u.email || '');
    });

    // ══════════════════════════════════════════════════════════════════
    // 6. ForStandard / Purpose toggles
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
    // 7. Form submit → client validation → AJAX POST → redirect
    // ══════════════════════════════════════════════════════════════════
    $('#frmDar').on('submit', function (e) {
        e.preventDefault();

        var valid = true;
        var docType = parseInt($('.doc-type-radio:checked').val() || '0');
        var isFreeType = FREE_TYPES.indexOf(docType) >= 0;

        // DocType required
        if (!docType) {
            markRadioError('grpDocType', 'dtInvalidMsg'); valid = false;
        }

        // ForStandard required only for dt1–6
        if (!isFreeType && !$('input[name="ForStandard"]:checked').val()) {
            markRadioError('grpForStandard', 'fsInvalidMsg'); valid = false;
        }

        // Purpose required
        if (!$('input[name="Purpose"]:checked').val()) {
            markRadioError('grpPurpose', 'purposeInvalidMsg'); valid = false;
        }

        // Reviewer required
        if ($('#cardReviewerApprover').is(':visible') && !$('#ddlReviewer').val()) {
            $('#reviewerInvalidMsg').show(); valid = false;
        }

        // Required text fields
        $('.req-field').each(function () {
            if (!$(this).val().trim()) {
                $(this).addClass('is-invalid'); valid = false;
            } else {
                $(this).removeClass('is-invalid');
            }
        });

        if (!valid) {
            var $first = $('.radio-group-invalid, .is-invalid, #reviewerInvalidMsg:visible').first();
            if ($first.length)
                $('html,body').animate({ scrollTop: $first.offset().top - 100 }, 300);
            return;
        }

        submitDar();
    });

    function submitDar() {
        var $btn = $('#btnSubmit');
        $btn.prop('disabled', true)
            .html('<span class="spinner-border spinner-border-sm me-1"></span>Submitting...');

        $('#submitError').remove();

        var formData = new FormData($('#frmDar')[0]);

        // Disabled selects are not included in FormData — add manually
        if ($('#ddlApprover').prop('disabled') && $('#ddlApprover').val()) {
            formData.set('ApproverSamAcc', $('#ddlApprover').val());
        }

        $.ajax({
            url: '/Dar/Create',
            type: 'POST',
            data: formData,
            processData: false,
            contentType: false,
            success: function (res) {
                if (res.success) {
                    showSuccessToast('DAR created: ' + res.darNo);
                    setTimeout(function () {
                        window.location.href = res.redirect;
                    }, 800);
                } else {
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
            + '<i class="bi bi-exclamation-triangle-fill fs-5 flex-shrink-0"></i>'
            + '<div>' + $('<span>').text(msg).html() + '</div></div>');
        $('#frmDar').append($err);
        $('html,body').animate({ scrollTop: $err.offset().top - 80 }, 300);
    }

    function showSuccessToast(msg) {
        var $t = $('<div class="position-fixed top-0 end-0 p-3" style="z-index:9999">'
            + '<div class="toast show align-items-center text-bg-success border-0">'
            + '<div class="d-flex"><div class="toast-body fw-semibold">'
            + '<i class="bi bi-check-circle me-1"></i>' + $('<span>').text(msg).html()
            + '</div></div></div></div>');
        $('body').append($t);
        setTimeout(function () { $t.remove(); }, 2000);
    }

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
    // 8. File Upload — Drag & Drop + Browse
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
            alert('File exceeds ' + MAX_MB + ' MB limit.');
            clearFile(); return;
        }
        showFilePreview(file);
    }

    $('#attachmentFile').on('change', function () { handleFile(this.files[0]); });
    $('#btnRemoveFile').on('click', clearFile);
    $('#uploadArea').on('click', function (e) {
        if (!$(e.target).is('input,label,button')) $('#attachmentFile').trigger('click');
    });
    $('#uploadArea')
        .on('dragover dragenter', function (e) { e.preventDefault(); $(this).addClass('drag-over'); })
        .on('dragleave dragend', function (e) { e.preventDefault(); $(this).removeClass('drag-over'); })
        .on('drop', function (e) {
            e.preventDefault(); $(this).removeClass('drag-over');
            var files = e.originalEvent.dataTransfer.files;
            if (files.length > 0) {
                var dt = new DataTransfer(); dt.items.add(files[0]);
                $('#attachmentFile')[0].files = dt.files;
                handleFile(files[0]);
            }
        });

});
