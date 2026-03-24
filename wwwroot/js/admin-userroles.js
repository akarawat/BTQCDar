/**
 * admin-userroles.js — Admin: User Approval Roles page
 * Flow: Load users → display table → assign/remove role via AJAX → SP → DB
 */

$(function () {

    var allUsers  = [];
    var roleConfig = [];
    var $toast = new bootstrap.Toast($('#toastMsg')[0], { delay: 3000 });

    function showToast(msg, isSuccess) {
        $('#toastMsg').removeClass('text-bg-success text-bg-danger')
                      .addClass(isSuccess ? 'text-bg-success' : 'text-bg-danger');
        $('#toastText').text(msg);
        $toast.show();
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

    // ── 2. Load all users ───────────────────────────────────────────
    function loadUsers() {
        $('#tblBody').html('<tr><td colspan="7" class="text-center py-4 text-muted">'
            + '<span class="spinner-border spinner-border-sm me-2"></span>Loading...</td></tr>');

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
            $('#tblBody').html('<tr><td colspan="7" class="text-danger text-center py-3">'
                + 'Failed to load users.</td></tr>');
        });
    }

    // ── 3. Render table ─────────────────────────────────────────────
    function renderTable(data) {
        var $body = $('#tblBody').empty();
        $('#userCount').text(data.length + ' users');

        if (data.length === 0) {
            $body.html('<tr><td colspan="7" class="text-center text-muted py-3">No users found.</td></tr>');
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

            var $tr = $('<tr>').attr('data-samacc', u.samAcc.toLowerCase())
                .append($('<td>').html('<code class="small">' + $('<span>').text(u.samAcc).html() + '</code>'))
                .append($('<td>').text(u.fullName))
                .append($('<td class="small">').text(u.department))
                .append($('<td class="small text-muted">').text(u.managerName))
                .append($('<td class="current-role">').html(currentRoleBadge))
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
                    showToast('Role assigned: ' + u.fullName + ' → ' + roleName, true);
                } else {
                    showToast('Error: ' + (res.message || 'Unknown error'), false);
                }
            }).fail(function () {
                showToast('Network error. Please try again.', false);
            });
        });

        // Delete button → remove role
        $body.find('button.btn-outline-danger').on('click', function () {
            var u = $(this).data('user');
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
