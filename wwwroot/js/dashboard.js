/**
 * dashboard.js — Dashboard page JS
 * Loads stat counters via AJAX (add API endpoint later if needed)
 */

$(function () {
    // ── Load stats from /Dar/Stats API ────────────────────────────────
    $.getJSON('/Dar/Stats', function (data) {
        $('#stat-draft').text(data.draft ?? 0);
        $('#stat-pending').text(data.pending ?? 0);
        $('#stat-done').text(data.completed ?? 0);
        $('#stat-rejected').text(data.rejected ?? 0);
    }).fail(function () {
        // API not ready yet — show dashes
        $('.card h5[id^=stat]').text('—');
    });
});
