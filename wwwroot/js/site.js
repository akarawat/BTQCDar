/**
 * site.js — BTQCDar global JS
 * Loaded on every page via _Layout.cshtml
 */

$(function () {

    // Auto-dismiss alerts after 4s
    $('.alert-auto-dismiss').each(function () {
        var el = this;
        setTimeout(function () {
            $(el).fadeOut(400, function () { $(el).remove(); });
        }, 4000);
    });

    // Active nav link highlight
    var path = window.location.pathname.toLowerCase();
    $('.navbar-nav .nav-link').each(function () {
        var href = $(this).attr('href') || '';
        if (href !== '/' && path.startsWith(href.toLowerCase())) {
            $(this).addClass('active');
        }
    });

    // Confirm on data-confirm buttons
    $(document).on('click', '[data-confirm]', function (e) {
        var msg = $(this).data('confirm') || 'Are you sure?';
        if (!confirm(msg)) e.preventDefault();
    });

    // Prevent double submit
    $('form').on('submit', function () {
        var btn = $(this).find('[type=submit]');
        btn.prop('disabled', true).html(
            '<span class="spinner-border spinner-border-sm me-1"></span>Saving...'
        );
    });


    // ── Pending count badge (nav + dashboard) ─────────────────────────────
    function loadPendingCount() {
        $.getJSON('/Dar/PendingCount', function (res) {
            var n = res.count || 0;

            // Nav badge
            var $nav = $('#navPendingBadge');
            if (n > 0) $nav.text(n > 99 ? '99+' : n).show();
            else $nav.hide();

            // Dashboard card badge (only on dashboard page)
            var $dash = $('#dashPendingBadge');
            if ($dash.length) {
                if (n > 0) $dash.text(n > 99 ? '99+' : n).show();
                else $dash.hide();
            }
        }).fail(function () {
            $('#navPendingBadge, #dashPendingBadge').hide();
        });
    }

    // Load on every page
    loadPendingCount();

});
