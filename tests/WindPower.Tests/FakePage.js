// Just enough of the CoreUI1 page for windpower.js to run in Jint: elements with classes, text and a parent, the
// one-class selectors the script uses, and the HUD's weather tooltip drawn the way the game's template draws it.
// Test code only; nothing here ships.
var window = this;

var FakeElement = class {
    constructor(tagName, className) {
        var element = this;

        this.tagName = tagName;
        this.parentElement = null;
        this.children = [];
        this.writes = 0;
        this.text = '';
        this.classes = className ? className.split(' ') : [];
        this.classList = {
            contains: function (name) { return element.classes.indexOf(name) >= 0; }
        };
    }

    get className() { return this.classes.join(' '); }

    set className(value) { this.classes = String(value).split(' ').filter(function (c) { return c; }); }

    // Counts writes, so a test can tell a redraw from a reading that changed nothing.
    get textContent() { return this.text; }

    set textContent(value) { this.text = String(value); this.writes++; }

    get isConnected() {
        var element = this;

        while (element.parentElement) element = element.parentElement;

        return element === document.documentElement;
    }

    get nextElementSibling() {
        if (!this.parentElement) return null;

        var siblings = this.parentElement.children;

        return siblings[siblings.indexOf(this) + 1] || null;
    }

    appendChild(child) { return this.insertBefore(child, null); }

    insertBefore(child, before) {
        if (child.parentElement) child.parentElement.removeChild(child);

        var at = before ? this.children.indexOf(before) : this.children.length;

        if (at < 0) throw new Error('insertBefore: not a child of this element');

        child.parentElement = this;
        this.children.splice(at, 0, child);

        return child;
    }

    removeChild(child) {
        var at = this.children.indexOf(child);

        if (at < 0) throw new Error('removeChild: not a child of this element');

        this.children.splice(at, 1);
        child.parentElement = null;

        return child;
    }

    matches(selector) {
        if (/^\.[\w-]+$/.test(selector)) return this.classList.contains(selector.slice(1));

        throw new Error('the fake page does not know the selector "' + selector + '"');
    }

    querySelectorAll(selector) {
        return descendantsOf(this).filter(function (element) { return element.matches(selector); });
    }

    querySelector(selector) {
        return this.querySelectorAll(selector)[0] || null;
    }
};

function descendantsOf(root) {
    var found = [];

    root.children.forEach(function walk(child) {
        found.push(child);
        child.children.forEach(walk);
    });

    return found;
}

var document = {
    documentElement: new FakeElement('html'),
    createElement: function (tagName) { return new FakeElement(tagName); },
    querySelector: function (selector) { return document.documentElement.querySelector(selector); },
    querySelectorAll: function (selector) { return document.documentElement.querySelectorAll(selector); }
};

document.body = document.documentElement.appendChild(new FakeElement('body'));

// Draws the weather tooltip as CoreUI1's template does: the title, the Light and Cold rows, then Tomorrow when the
// forecast is known. spec: { hasNoTooltip, hasNoTomorrow }
function buildPage(spec) {
    var hud = document.body.appendChild(new FakeElement('div', 'weather-box'));

    if (spec.hasNoTooltip) return { hud: hud };

    return { hud: hud, tooltip: drawTooltip(hud, !spec.hasNoTomorrow) };
}

function drawTooltip(hud, hasTomorrow) {
    var tooltip = hud.appendChild(new FakeElement('div', 'weather-tooltip'));

    tooltip.appendChild(new FakeElement('div', 'weather-tt-title'));
    addRow(tooltip, 'weather-tt-row', 'Light Intensity', '80%');
    addRow(tooltip, 'weather-tt-row', 'Cold Intensity', '0%');

    if (hasTomorrow) addRow(tooltip, 'weather-tt-row weather-tt-next', 'Tomorrow', 'Rainy');

    return tooltip;
}

function addRow(tooltip, className, label, value) {
    var row = tooltip.appendChild(new FakeElement('div', className));

    row.appendChild(new FakeElement('span', 'weather-tt-label')).textContent = label;
    row.appendChild(new FakeElement('span', 'weather-tt-value')).textContent = value;

    return row;
}

// The tooltip's rows top to bottom, each as its label.
function rowLabels(tooltip) {
    return tooltip.children.filter(function (child) { return child.classList.contains('weather-tt-row'); }).map(function (row) { return row.children[0].textContent; }).join('|');
}

function windRow() {
    return document.querySelector('.slwind-row');
}
