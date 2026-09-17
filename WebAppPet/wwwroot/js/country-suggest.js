/**
 * Country autocomplete from /api/countries/suggest (catalog ISO list).
 * Mark search input with data-country-suggest and optional data-country-iso="#hiddenIso".
 */
(function (global) {
  function byIdOrSel(sel) {
    if (!sel) return null;
    return document.querySelector(sel) || document.getElementById(sel.replace(/^#/, ""));
  }

  function ensureList(input) {
    var wrap = input.closest(".places-field");
    if (!wrap) {
      wrap = document.createElement("div");
      wrap.className = "places-field";
      input.parentNode.insertBefore(wrap, input);
      wrap.appendChild(input);
    }
    var list = wrap.querySelector(".places-suggest");
    if (!list) {
      list = document.createElement("ul");
      list.className = "places-suggest";
      list.hidden = true;
      list.setAttribute("role", "listbox");
      wrap.appendChild(list);
    }
    return list;
  }

  function attachOne(input) {
    if (!input || input._chomblyCountrySuggest) return;
    var isoSel = input.getAttribute("data-country-iso");
    var isoEl = byIdOrSel(isoSel);
    var list = ensureList(input);
    if (!list) return;

    var timer = null;
    var seq = 0;

    function hide() {
      list.hidden = true;
      list.innerHTML = "";
    }

    function pick(item) {
      input.value = item.name || item.label || "";
      if (isoEl) {
        isoEl.value = item.iso || "";
        isoEl.dispatchEvent(new Event("change", { bubbles: true }));
        isoEl.dispatchEvent(new Event("input", { bubbles: true }));
      }
      input.dispatchEvent(new Event("change", { bubbles: true }));
      hide();
    }

    function show(items) {
      list.innerHTML = "";
      if (!items || !items.length) {
        hide();
        return;
      }
      items.forEach(function (item) {
        var li = document.createElement("li");
        li.setAttribute("role", "option");
        li.textContent = item.label || item.name || item.iso;
        li.addEventListener("mousedown", function (e) {
          e.preventDefault();
          pick(item);
        });
        list.appendChild(li);
      });
      list.hidden = false;
    }

    function search(q) {
      var my = ++seq;
      fetch("/api/countries/suggest?q=" + encodeURIComponent(q), {
        headers: { Accept: "application/json" }
      })
        .then(function (r) { return r.ok ? r.json() : []; })
        .then(function (items) {
          if (my !== seq) return;
          show(Array.isArray(items) ? items : []);
        })
        .catch(function () {
          if (my === seq) hide();
        });
    }

    input.addEventListener("input", function () {
      // Typing freely invalidates prior ISO until a suggestion is picked
      // (or a 2-letter code is entered).
      var q = (input.value || "").trim();
      if (isoEl) {
        if (/^[A-Za-z]{2}$/.test(q)) {
          isoEl.value = q.toUpperCase();
        } else if (isoEl.value && q.toUpperCase() !== isoEl.value) {
          isoEl.value = "";
        }
      }
      clearTimeout(timer);
      if (q.length < 1) {
        hide();
        return;
      }
      timer = setTimeout(function () { search(q); }, 220);
    });

    input.addEventListener("focus", function () {
      var q = (input.value || "").trim();
      if (q.length >= 1) search(q);
      else search("");
    });

    input.addEventListener("blur", function () {
      setTimeout(hide, 150);
    });

    input.addEventListener("keydown", function (e) {
      if (e.key === "Escape") hide();
    });

    input._chomblyCountrySuggest = true;
    input.setAttribute("autocomplete", "off");
  }

  function initCountrySuggest() {
    document.querySelectorAll("[data-country-suggest]").forEach(attachOne);
  }

  global.initChomblyCountrySuggest = initCountrySuggest;

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", initCountrySuggest);
  } else {
    initCountrySuggest();
  }
})(window);
