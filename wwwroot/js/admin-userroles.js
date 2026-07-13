/**
 * admin-userroles.js — Admin: User Approval Roles page
 * Flow: Load users → display table → assign/remove role via AJAX → SP → DB
 */

$(function () {

    var allUsers  = [];
    var roleConfig = [];
    var qmrPermissMap = {}; // samAcc(lowercase) -> qmrPermiss bool, RoleType=2 (QMR) only
    var $toast = new bootstrap.Toast($('#toastMsg')[0], { delay: 3000 });

    function showToast(msg, isSuccess) {
        $('#toastMsg').removeClass('text-bg-success text-bg-danger')
                      .addClass(isSuccess ? 'text-bg-success' : 'text-bg-danger');
        $('#toastText').text(msg);
        $toast.show();
    }

    // QMR Approve cell — checkbox for RoleType=2 (QMR) rows only, "—" for every other role
    function buildQmrCell($cell, u) {
        if (u.roleType === 2) {
            var checked = !!qmrPermissMap[u.samAcc.toLowerCase()];
            $cell.html('<input type="checkbox" class="form-check-input qmr-permiss-chk" '
                + 'title="Allow this QMR to approve DAR">')
                .find('.qmr-permiss-chk')
                .data('samacc', u.samAcc)
                .prop('checked', checked);
        } else {
            $cell.html('<span class="text-muted">—</span>');
        }
    }

    // ── 1. Load role config first ───────────────────────────────────
    function loadRoleConfig(cb) {
        $.getJSON('/Admin/GetRoleConfig', function (data) {
            roleConfig = data;
            // Populate filter dropdown
            data.forEach(function (r) {
                $('#filterRole').append($('<option>').val(r.roleType).text(r.roleName));
            });
            if (cb) cb();
        });
    }

    // ── 2a. Load QMRPermiss map (RoleType=2 users only) ─────────────
    function loadQmrPermiss(cb) {
        $.getJSON('/Admin/GetUserRoles', function (data) {
            qmrPermissMap = {};
            data.forEach(function (r) {
                if (r.roleType === 2) qmrPermissMap[r.samAcc.toLowerCase()] = !!r.qmrPermiss;
            });
            if (cb) cb();
        }).fail(function () { if (cb) cb(); }); // non-fatal — checkboxes default unchecked
    }

    // ── 2b. Load all users ───────────────────────────────────────────
    function loadUsers() {
        $('#tblBody').html('<tr><td colspan="8" class="text-center py-4 text-muted">'
            + '<span class="spinner-border spinner-border-sm me-2"></span>Loading...</td></tr>');

        loadQmrPermiss(function () {
            $.getJSON('/Admin/GetAllUsers', function (data) {
                allUsers = data;

                // Populate dept filter
                var depts = {};
                data.forEach(function (u) { depts[u.department] = true; });
                $('#filterDept').find('option:not(:first)').remove();
                Object.keys(depts).sort().forEach(function (d) {
                    $('#filterDept').append($('<option>').val(d).text(d));
                });

                renderTable(data);
            }).fail(function () {
                $('#tblBody').html('<tr><td colspan="8" class="text-danger text-center py-3">'
                    + 'Failed to load users.</td></tr>');
            });
        });
    }

    // ── 3. Render table ─────────────────────────────────────────────
    function renderTable(data) {
        var $body = $('#tblBody').empty();
        $('#userCount').text(data.length + ' users');

        if (data.length === 0) {
            $body.html('<tr><td colspan="8" class="text-center text-muted py-3">No users found.</td></tr>');
            return;
        }

        data.forEach(function (u) {
            // Role select for this row
            var $sel = $('<select class="form-select form-select-sm role-select">');
            $sel.append($('<option>').val('').text('-- No Role --'));
            roleConfig.forEach(function (r) {
                var $opt = $('<option>').val(r.roleType).text(r.roleName);
                if (u.roleType && u.roleType === r.roleType) $opt.prop('selected', true);
                $sel.append($opt);
            });
            $sel.data('user', u);

            // Delete button (only if has role)
            var $del = $('<button class="btn btn-outline-danger btn-sm ms-1" title="Remove role">'
                       + '<i class="bi bi-trash"></i></button>');
            $del.data('user', u).toggle(!!u.roleType);

            var $actionCell = $('<td class="d-flex gap-1 align-items-center">').append($sel).append($del);

            var currentRoleBadge = u.roleName
                ? '<span class="badge bg-danger bg-opacity-75">' + $('<span>').text(u.roleName).html() + '</span>'
                : '<span class="text-muted small">—</span>';

            // QMR Approve — checkbox only for RoleType=2 (QMR); every other role is read-only "—"
            var $qmrCell = $('<td class="text-center qmr-cell">');
            buildQmrCell($qmrCell, u);

            var $tr = $('<tr>').attr('data-samacc', u.samAcc.toLowerCase())
                .append($('<td>').html('<code class="small">' + $('<span>').text(u.samAcc).html() + '</code>'))
                .append($('<td>').text(u.fullName))
                .append($('<td class="small">').text(u.department))
                .append($('<td class="small text-muted">').text(u.managerName))
                .append($('<td class="current-role">').html(currentRoleBadge))
                .append($qmrCell)
                .append($actionCell);

            $body.append($tr);
        });

        // Role select change → save
        $body.find('.role-select').on('change', function () {
            var $sel  = $(this);
            var u     = $sel.data('user');
            var rt    = parseInt($sel.val());

            if (!rt) return; // clear handled by delete button

            $.post('/Admin/SaveUserRole', {
                samAcc   : u.samAcc,
                fullName : u.fullName,
                depCode  : u.depCode,
                depart   : u.department,
                roleType : rt,
                __RequestVerificationToken: $('input[name="__RequestVerificationToken"]').val()
            })
            .done(function (res) {
                if (res.success) {
                    var roleName = roleConfig.find(function(r){ return r.roleType===rt; })?.roleName || '';
                    $sel.closest('tr').find('.current-role')
                        .html('<span class="badge bg-danger bg-opacity-75">'
                             + $('<span>').text(roleName).html() + '</span>');
                    $sel.closest('tr').find('button.btn-outline-danger').show();
                    // Role changed — rebuild QMR cell (newly-assigned QMR starts unchecked, matches DB default)
                    u.roleType = rt;
                    buildQmrCell($sel.closest('tr').find('.qmr-cell').empty(), u);
                    showToast('Role assigned: ' + u.fullName + ' → ' + roleName, true);
                } else {
                    showToast('Error: ' + (res.message || 'Unknown error'), false);
                }
            }).fail(function () {
                showToast('Network error. Please try again.', false);
            });
        });

        // QMR Approve checkbox → save
        $body.find('.qmr-permiss-chk').on('change', function () {
            var $chk    = $(this);
            var samAcc  = $chk.data('samacc');
            var permiss = $chk.prop('checked');

            $.post('/Admin/SaveQMRPermiss', {
                samAcc: samAcc, permiss: permiss,
                __RequestVerificationToken: $('input[name="__RequestVerificationToken"]').val()
            })
            .done(function (res) {
                if (res.success) {
                    qmrPermissMap[samAcc.toLowerCase()] = permiss;
                    showToast((permiss ? 'QMR Approve granted to ' : 'QMR Approve revoked from ') + samAcc, true);
                } else {
                    $chk.prop('checked', !permiss);
                    showToast('Error: ' + (res.message || 'Unknown error'), false);
                }
            })
            .fail(function () {
                $chk.prop('checked', !permiss);
                showToast('Network error. Please try again.', false);
            });
        });

        // Delete button → remove role
        $body.find('button.btn-outline-danger').on('click', function () {
            var u = $(this).data('user');
            console.log(u);
            if (!confirm('Remove role from ' + (u.fullName || u.samAcc) + '?')) return;

            $.post('/Admin/DeleteUserRole', {
                id: u.id || 0,
                __RequestVerificationToken: $('input[name="__RequestVerificationToken"]').val()
            })
            .done(function (res) {
                if (res.success) {
                    showToast('Role removed: ' + (u.fullName || u.samAcc), true);
                    loadUsers(); // refresh
                } else {
                    showToast('Error: ' + (res.message || ''), false);
                }
            });
        });
    }

    // ── 4. Search + filter ──────────────────────────────────────────
    function applyFilter() {
        var kw   = $('#userSearch').val().toLowerCase().trim();
        var dept = $('#filterDept').val();
        var role = $('#filterRole').val();

        var filtered = allUsers.filter(function (u) {
            var matchKw   = !kw   || (u.samAcc+u.fullName+u.department).toLowerCase().includes(kw);
            var matchDept = !dept || u.department === dept;
            var matchRole = !role || String(u.roleType) === role;
            return matchKw && matchDept && matchRole;
        });
        renderTable(filtered);
    }

    $('#userSearch').on('input', applyFilter);
    $('#filterDept, #filterRole').on('change', applyFilter);
    $('#btnRefresh').on('click', loadUsers);

    // ── 5. CSRF token for all AJAX POSTs ───────────────────────────
    // ── Init ────────────────────────────────────────────────────────
    loadRoleConfig(loadUsers);

});
