/*
 * Shared behaviour of the HS (Tea Sale Management) report parameter forms -- same look and feel as the
 * Bought Leaf report forms (Despatch Register): dd/mm/yyyy date boxes with a calendar icon bounded by the
 * financial year, jquery.inputpicker drop-downs (libraries/MULTI_SELECT_DROPDOWN) with "All", and the
 * criteria posted to a new tab.
 *
 *   HsReport.init({ title, fyFrom, fyTo })
 *   HsReport.picker({ id, multiple, data | load, fields, width, placeholder, onChange })
 *   HsReport.codes(id)  -> "All" or "A,B"          HsReport.value(id) -> picked code ("" when none)
 *   HsReport.date(id)   -> Date or null             HsReport.open(url, data)
 */
(function ($, window) {
    "use strict";

    var cfg = { title: "Report", fyFrom: "", fyTo: "" };
    var pickers = {};

    function parseDate(s) {
        var m = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec($.trim(s || ""));
        return m ? new Date(+m[3], +m[2] - 1, +m[1]) : null;
    }

    function alertBox(msg) {
        if ($.confirm) {
            $.confirm({ title: cfg.title, type: "red", content: $("<div>").text(msg).html(), typeAnimated: true, buttons: { Okay: function () { } } });
        } else {
            window.alert(msg);
        }
    }

    function init(o) {
        $.extend(cfg, o || {});
        $(".sr-date").each(function () {
            if (!$(this).val() && $(this).data("default")) $(this).val($(this).data("default"));
        });
        $(".sr-date").datepicker({
            dateFormat: "dd/mm/yy",
            changeMonth: true,
            changeYear: true,
            showAnim: "",
            minDate: parseDate(cfg.fyFrom),
            maxDate: parseDate(cfg.fyTo),
            beforeShow: function () { setTimeout(function () { $("#ui-datepicker-div").css("z-index", 99999); }, 0); }
        });
        $(document).on("click", ".sr-cal", function () { $($(this).data("target")).datepicker("show"); });
        // Close an open picker list when clicking elsewhere.
        $(document).on("mousedown", function (e) {
            if (!$(e.target).closest(".inputpicker-wrapped-list, .inputpicker-div, .rpt-picker").length) $(".inputpicker-wrapped-list").hide();
        });
    }

    /*
     * A drop-down on the input #id (inside a holder div #id_holder, so it can be rebuilt when its list changes).
     * o.data: rows ([{ CODE, DESCN }]) or o.load(): a jqXHR / promise giving the rows -- called again by reload().
     */
    function picker(o) {
        // allText: the value a multi-select shows when nothing is ticked ("All"); "" for a required list.
        var p = $.extend({ multiple: true, fields: null, width: "360px", placeholder: "All", allText: "All", data: null, load: null, onChange: null, auto: true }, o);
        p.rows = null;
        pickers[p.id] = p;
        render(p, p.multiple ? p.allText : "");
        if (p.data) p.rows = p.data;
        else if (p.load) reload(p.id);
        $(document).off("click.hsr", "#" + p.id).on("click.hsr", "#" + p.id, function () { open(p.id); });
        return p;
    }

    function render(p, value, placeholder) {
        var $holder = $("#" + p.id + "_holder");
        $(".inputpicker-wrapped-list").remove();
        $holder.html('<input type="text" id="' + p.id + '" class="form-control rpt-picker" autocomplete="off" />');
        var $in = $("#" + p.id);
        $in.attr("placeholder", placeholder || p.placeholder);
        if (value) $in.val(value);
    }

    function reload(id, keepValue) {
        var p = pickers[id];
        if (!p || !p.load) return $.Deferred().resolve([]).promise();
        p.rows = null;
        render(p, p.multiple ? p.allText : "", "Loading...");
        var seq = (p.seq || 0) + 1;
        p.seq = seq;
        return $.when(p.load()).then(function (list) {
            if (seq !== p.seq) return;
            if (list && list.error) { p.rows = []; render(p, "", "-"); alertBox(list.error); return; }
            p.rows = $.isArray(list) ? list : (list && list.data) || [];
            var single = !p.multiple && p.auto && p.rows.length === 1;
            render(p, p.multiple ? p.allText : (single ? p.rows[0].CODE : ""), p.rows.length ? p.placeholder : (p.emptyText || "None found"));
            if (single && p.onChange) p.onChange(p.rows[0].CODE, p.rows[0]);
            return p.rows;
        }, function () {
            if (seq !== p.seq) return;
            p.rows = [];
            render(p, "", "-");
            alertBox("Unable to load the list.");
        });
    }

    function setRows(id, rows) {
        var p = pickers[id];
        if (!p) return;
        p.rows = rows || [];
        render(p, p.multiple ? p.allText : "", p.rows.length ? p.placeholder : "None found");
    }

    function open(id) {
        var p = pickers[id];
        var $in = $("#" + id);
        $(".inputpicker-wrapped-list").remove();
        if ($in.data("inputpicker")) { $in.inputpicker("show"); return; }
        if (!p || p.rows === null || p.rows.length === 0) return;
        var fields = p.fields || [
            { name: "CODE", text: "CODE", width: "80px" },
            { name: "DESCN", text: "NAME", width: "260px" }
        ];
        if ($in.val() === "All") $in.val("");
        var opts = {
            data: p.rows,
            fields: fields,
            fieldValue: "CODE",
            fieldText: "CODE",
            filterOpen: true,
            headShow: true,
            autoOpen: true,
            width: p.width,
            height: "240px"
        };
        if (p.multiple) $.extend(opts, { multiple: true, checkbox: true, selectAll: true, selectAllText: "All", selectAllClears: true, delimiter: "," });
        $in.inputpicker(opts);
        // Built on this click: open the list now (otherwise it opens only on the next click).
        setTimeout(function () { $in.inputpicker("show"); $in.next().find("input").first().trigger("focus"); }, 0);
        $in.on("change", function () {
            if (!p.onChange) return;
            var v = $.trim($in.val() || "");
            var row = null;
            $.each(p.rows, function (_, r) { if (r.CODE === v) { row = r; return false; } });
            p.onChange(v, row);
        });
    }

    function codes(id) {
        var v = $.trim($("#" + id).val() || "");
        return v === "" ? "All" : v;
    }

    function value(id) {
        var v = $.trim($("#" + id).val() || "");
        return v === "All" ? "" : v;
    }

    function date(id) { return parseDate($("#" + id).val()); }

    /* Checks a From / To pair against the financial year; returns false (after a message) when wrong. */
    function checkPeriod(fromId, toId) {
        var f = date(fromId), t = toId ? date(toId) : null, min = parseDate(cfg.fyFrom), max = parseDate(cfg.fyTo);
        if (!f) { alertBox("Select the " + (toId ? "From " : "") + "Date (dd/mm/yyyy)."); return false; }
        if (toId && !t) { alertBox("Select the To Date (dd/mm/yyyy)."); return false; }
        if (t && f > t) { alertBox("From Date cannot be after To Date."); return false; }
        if ((min && f < min) || (max && (t || f) > max)) { alertBox("The dates must be within the financial year " + cfg.fyFrom + " - " + cfg.fyTo + "."); return false; }
        return true;
    }

    /* Posts the criteria to a new tab (lists of codes can be long). */
    function openReport(url, data) {
        var $form = $('<form method="post" target="_blank" style="display:none"></form>').attr("action", url);
        $.each(data, function (name, v) { $('<input type="hidden" />').attr("name", name).val(v).appendTo($form); });
        var token = $('input[name="__RequestVerificationToken"]').first().val();
        if (token) $('<input type="hidden" name="__RequestVerificationToken" />').val(token).appendTo($form);
        $form.appendTo("body").submit().remove();
    }

    window.HsReport = {
        init: init, picker: picker, reload: reload, setRows: setRows, codes: codes, value: value, date: date,
        checkPeriod: checkPeriod, open: openReport, alert: alertBox, parseDate: parseDate
    };
})(jQuery, window);
