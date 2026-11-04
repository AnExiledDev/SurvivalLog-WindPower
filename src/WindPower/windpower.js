// Wind Power: a Wind Intensity row in the HUD's weather tooltip (the CoreUI1 page), drawn with the game's own row
// classes so it sits with Light Intensity and Cold Intensity, above the Tomorrow line. The plugin hands it the
// weather's wind every couple of seconds through apply. Reads and draws only; nothing the game keeps is changed.
(function () {
    if (window.__slwind) return;

    var ROW = 'slwind-row';

    var reading = null;
    var tooltip = null;
    var row = null;
    var watcher = null;

    // The tooltip stays in the page while the HUD is up; a page that rebuilt its HUD has a new one.
    function findTooltip() {
        if (tooltip && tooltip.isConnected) return tooltip;

        tooltip = document.querySelector('.weather-tooltip');
        watch(tooltip);

        return tooltip;
    }

    // Vue redraws the tooltip's own rows and adds or drops Tomorrow; a watch on the tooltip alone puts the wind row
    // back in place after it, instead of searching the page on a timer.
    function watch(element) {
        if (watcher) watcher.disconnect();

        watcher = null;

        if (!element || typeof MutationObserver === 'undefined') return;

        watcher = new MutationObserver(function () { place(); });
        watcher.observe(element, { childList: true });
    }

    function makeRow() {
        var made = document.createElement('div');
        var label = document.createElement('span');
        var value = document.createElement('span');

        made.className = 'weather-tt-row ' + ROW;
        label.className = 'weather-tt-label';
        value.className = 'weather-tt-value';
        made.appendChild(label);
        made.appendChild(value);

        return made;
    }

    function setText(element, text) {
        if (element.textContent !== text) element.textContent = text;
    }

    function place() {
        var tip = findTooltip();

        if (!tip) return 'notooltip';

        if (!reading || reading.pct < 0) {
            if (row && row.parentElement) row.parentElement.removeChild(row);

            settle();

            return 'ok';
        }

        if (!row) row = makeRow();

        setText(row.children[0], String(reading.label));
        setText(row.children[1], reading.pct + '%');

        var next = tip.querySelector('.weather-tt-next');
        var before = next && next.parentElement === tip ? next : null;

        if (row.parentElement !== tip || row.nextElementSibling !== before) tip.insertBefore(row, before);

        settle();

        return 'ok';
    }

    // Our own writes are not changes to answer.
    function settle() {
        if (watcher) watcher.takeRecords();
    }

    window.__slwind = {
        apply: function (next) {
            reading = next;

            return place();
        }
    };
})();
