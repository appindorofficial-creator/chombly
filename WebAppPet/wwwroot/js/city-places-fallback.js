/**
 * Fallback city/address suggestions when Google Maps API key is missing.
 * Uses /api/places/suggest and fills the same lat/lng hooks as google-places.js.
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

  function fill(input, item, opts) {
    var addressEl = byIdOrSel(opts.addressSel) || document.querySelector("[data-places-fill='address']");
    var cityEl = byIdOrSel(opts.citySel) || (opts.mode === "city" ? input : document.querySelector("[data-places-fill='city']"));
    var latEl = byIdOrSel(opts.latSel) || document.querySelector("[data-places-fill='lat']");
    var lngEl = byIdOrSel(opts.lngSel) || document.querySelector("[data-places-fill='lng']");

    if (opts.mode === "city") {
      input.value = item.city || item.label || "";
      input.dispatchEvent(new Event("change", { bubbles: true }));
    } else {
      if (addressEl) {
        addressEl.value = item.address || item.label || "";
        addressEl.dispatchEvent(new Event("change", { bubbles: true }));
      } else {
        input.value = item.address || item.label || "";
        input.dispatchEvent(new Event("change", { bubbles: true }));
      }
      if (cityEl && item.city) {
        cityEl.value = item.city;
        cityEl.dispatchEvent(new Event("change", { bubbles: true }));
      }
    }

    if (latEl) {
      latEl.value = String(item.lat);
      latEl.dispatchEvent(new Event("change", { bubbles: true }));
    }
    if (lngEl) {
      lngEl.value = String(item.lng);
      lngEl.dispatchEvent(new Event("change", { bubbles: true }));
    }

    var err = document.getElementById("city-maps-error");
    if (err) err.hidden = true;
  }

  function attachOne(input) {
    if (!input || input._chomblyPlacesFallback) return;
    var mode = (input.getAttribute("data-places") || "address").toLowerCase();
    var opts = {
      mode: mode,
      addressSel: input.getAttribute("data-places-address"),
      citySel: input.getAttribute("data-places-city"),
      latSel: input.getAttribute("data-places-lat"),
      lngSel: input.getAttribute("data-places-lng")
    };
    var list = ensureList(input);
    if (!list) return;

    var timer = null;
    var seq = 0;

    function hide() {
      list.hidden = true;
      list.innerHTML = "";
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
        li.textContent = item.label;
        li.addEventListener("mousedown", function (e) {
          e.preventDefault();
          fill(input, item, opts);
          hide();
        });
        list.appendChild(li);
      });
      list.hidden = false;
    }

    function search(q) {
      var my = ++seq;
      var type = mode === "city" ? "city" : "address";
      fetch("/api/places/suggest?type=" + encodeURIComponent(type) + "&q=" + encodeURIComponent(q), {
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
      var q = (input.value || "").trim();
      clearTimeout(timer);
      if (q.length < 2) {
        hide();
        return;
      }
      timer = setTimeout(function () { search(q); }, 280);
    });

    input.addEventListener("blur", function () {
      setTimeout(hide, 150);
    });

    input.addEventListener("keydown", function (e) {
      if (e.key === "Escape") hide();
    });

    input._chomblyPlacesFallback = true;
    input.setAttribute("autocomplete", "off");
  }

  function initChomblyPlacesFallback() {
    document.querySelectorAll("[data-places]").forEach(attachOne);
  }

  global.initChomblyPlaces = initChomblyPlacesFallback;
  global.chomblyRefreshPlaces = initChomblyPlacesFallback;

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", initChomblyPlacesFallback);
  } else {
    initChomblyPlacesFallback();
  }
})(window);
