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


    // ══════════════════════════════════════════════════════════════════
    // 5. Approver Selection — load from API + checkbox + chips
    // ══════════════════════════════════════════════════════════════════

    var allApprovers = [];   // full dataset from API

    // Unique key for each approver row: "SamAcc|DepCode|RoleType"
    function approverKey(a) {
        return a.samAcc + '|' + a.depCode + '|' + a.roleType;
    }

    // Load from /Dar/GetApprovalUsers
    function loadApprovers() {
        $.getJSON('/Dar/GetApprovalUsers')
            .done(function (data) {
                allApprovers = data;
                populateFilters(data);
                renderList(data);
                $('#approverLoading').hide();
                $('#approverList').show();
            })
            .fail(function () {
                $('#approverLoading').html(
                    '<span class="text-danger small">Failed to load approvers.</span>');
            });
    }

    function populateFilters(data) {
        // Roles
        var roles = {};
        data.forEach(function (a) { roles[a.roleType] = a.roleName; });
        $.each(roles, function (rt, rn) {
            $('#filterRole').append($('<option>').val(rt).text(rn));
        });
        // Departments
        var depts = {};
        data.forEach(function (a) { depts[a.depCode] = a.depart; });
        $.each(depts, function (dc, dn) {
            $('#filterDept').append($('<option>').val(dc).text(dn));
        });
    }

    function renderList(data) {
        var $list = $('#approverList').empty();

        if (data.length === 0) {
            $list.html('<div class="text-muted text-center py-3 small">No results found.</div>');
            return;
        }

        // Group by Department
        var groups = {};
        data.forEach(function (a) {
            var grp = a.depCode + ' — ' + a.depart;
            if (!groups[grp]) groups[grp] = [];
            groups[grp].push(a);
        });

        $.each(groups, function (grpLabel, members) {
            var $grpHeader = $('<div class="px-3 py-1 bg-light border-bottom fw-semibold small text-muted">')
                .text(grpLabel);
            $list.append($grpHeader);

            members.forEach(function (a) {
                var key       = approverKey(a);
                var isChecked = $('input[name="selectedApprovers"][value="' + key + '"]').length > 0;
                var displayName = a.fullName || a.samAcc;

                var $row = $('<label class="d-flex align-items-center gap-3 px-3 py-2 approver-row border-bottom" style="cursor:pointer">')
                    .addClass(isChecked ? 'bg-danger bg-opacity-10' : '');

                var $chk = $('<input type="checkbox" class="form-check-input flex-shrink-0 approver-chk">')
                    .val(key)
                    .prop('checked', isChecked)
                    .attr('data-name',     displayName)
                    .attr('data-role',     a.roleName)
                    .attr('data-depart',   a.depart)
                    .attr('data-samAcc',   a.samAcc);

                var $info = $('<div class="flex-grow-1 small">');
                $info.append($('<div class="fw-semibold">').text(displayName));
                $info.append($('<div class="text-muted">').text(a.depart));

                var $badge = $('<span class="badge rounded-pill bg-danger bg-opacity-75 ms-auto small">')
                    .text(a.roleName);

                $row.append($chk).append($info).append($badge);
                $list.append($row);
            });
        });

        // Checkbox change handler
        $list.find('.approver-chk').on('change', function () {
            var key      = $(this).val();
            var $row     = $(this).closest('label');
            var checked  = $(this).is(':checked');

            $row.toggleClass('bg-danger bg-opacity-10', checked);

            if (checked) {
                addHiddenField(key);
                addChip(key, $(this).data('name'), $(this).data('role'), $(this).data('depart'));
            } else {
                removeHiddenField(key);
                removeChip(key);
            }
            updateCount();
            if (getSelectedCount() > 0) clearApproverError();
        });
    }

    // ── Hidden inputs submitted with the form ──────────────────────────────
    function addHiddenField(key) {
        if ($('input[name="selectedApprovers"][value="' + key + '"]').length === 0) {
            $('<input type="hidden" name="selectedApprovers">').val(key).appendTo('#frmDar');
        }
    }
    function removeHiddenField(key) {
        $('input[name="selectedApprovers"][value="' + key + '"]').remove();
    }

    // ── Chips (visual summary) ─────────────────────────────────────────────
    function addChip(key, name, role, dept) {
        var $chip = $('<span class="badge d-inline-flex align-items-center gap-1 py-2 px-3" '
                    + 'style="background:#dc3545;font-size:0.78rem;" data-key="' + key + '">')
            .html('<i class="bi bi-person-check me-1"></i>'
                + '<strong>' + $('<span>').text(name).html() + '</strong>'
                + ' <span class="opacity-75">(' + $('<span>').text(role).html() + ')</span>');

        var $del = $('<button type="button" class="btn-close btn-close-white ms-1" '
                   + 'style="font-size:0.6rem;" aria-label="Remove">');
        $del.on('click', function () {
            removeChip(key);
            removeHiddenField(key);
            uncheckRow(key);
            updateCount();
        });
        $chip.append($del);
        $('#selectedChips').append($chip);
    }
    function removeChip(key) {
        $('#selectedChips [data-key="' + key + '"]').remove();
    }
    function uncheckRow(key) {
        // Uncheck the checkbox in the rendered list
        $('.approver-chk[value="' + key + '"]')
            .prop('checked', false)
            .closest('label').removeClass('bg-danger bg-opacity-10');
    }

    function getSelectedCount() {
        return $('input[name="selectedApprovers"]').length;
    }
    function updateCount() {
        var n = getSelectedCount();
        $('#approverCount').text(n + (n === 1 ? ' selected' : ' selected'));
    }

    // ── Approver required validation ──────────────────────────────────────
    function clearApproverError() {
        $('#approverInvalidMsg').hide();
    }

    // ── Live search + filter ───────────────────────────────────────────────
    function getFiltered() {
        var kw   = $('#approverSearch').val().toLowerCase().trim();
        var role = $('#filterRole').val();
        var dept = $('#filterDept').val();
        return allApprovers.filter(function (a) {
            var matchKw   = !kw   || (a.fullName + a.samAcc + a.depart).toLowerCase().includes(kw);
            var matchRole = !role || String(a.roleType) === role;
            var matchDept = !dept || a.depCode === dept;
            return matchKw && matchRole && matchDept;
        });
    }

    $('#approverSearch, #filterRole, #filterDept').on('input change', function () {
        renderList(getFiltered());
    });

    // ── Extend form submit validation ─────────────────────────────────────
    var originalSubmit = $('#frmDar').data('events');
    $('#frmDar').on('submit.approver', function (e) {
        if (getSelectedCount() === 0) {
            e.preventDefault();
            e.stopImmediatePropagation();
            $('#approverInvalidMsg').show();
            $('html, body').animate({ scrollTop: $('#approverInvalidMsg').offset().top - 100 }, 300);
        }
    });

    // Kick off
    loadApprovers();

