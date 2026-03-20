/**
 * dar-list.js — DAR Index page JS
 * Client-side search/filter on the DAR table
 */

$(function () {

    // ── Live search ───────────────────────────────────────────────────
    $('#tblSearch').on('input', function () {
        var keyword = $(this).val().toLowerCase().trim();

        $('#tblDar tbody tr').each(function () {
            var text = $(this).text().toLowerCase();
            $(this).toggle(text.indexOf(keyword) > -1);
        });
    });

    // ── Sort on header click ──────────────────────────────────────────
    var sortAsc = {};
    $('#tblDar thead th').on('click', function () {
        var col = $(this).index();
        sortAsc[col] = !sortAsc[col];
        var rows = $('#tblDar tbody tr').toArray();

        rows.sort(function (a, b) {
            var aText = $(a).children('td').eq(col).text().trim();
            var bText = $(b).children('td').eq(col).text().trim();
            return sortAsc[col]
                ? aText.localeCompare(bText, 'th')
                : bText.localeCompare(aText, 'th');
        });

        $('#tblDar tbody').append(rows);
    });

    // Give clickable cursor to headers
    $('#tblDar thead th').css('cursor', 'pointer');
});
