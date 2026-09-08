/**
 * Chombly Google Places Autocomplete
 * Marca inputs con:
 *   data-places="address"  → dirección completa (llena address, city, lat, lng)
 *   data-places="city"     → solo ciudades
 * Opcional: data-places-address, data-places-city, data-places-lat, data-places-lng (selectores)
 */
(function (global) {
  function byIdOrSel(sel) {
    if (!sel) return null;
    return document.querySelector(sel) || document.getElementById(sel.replace(/^#/, ""));
  }

  function fillFromPlace(place, opts) {
    if (!place || !place.address_components) return;

    var streetNumber = "";
    var route = "";
    var city = "";
    var state = "";
    var country = "";
    var postal = "";

    place.address_components.forEach(function (c) {
      var t = c.types || [];
      if (t.indexOf("street_number") >= 0) streetNumber = c.long_name;
      if (t.indexOf("route") >= 0) route = c.long_name;
      if (t.indexOf("locality") >= 0) city = c.long_name;
      if (t.indexOf("sublocality") >= 0 && !city) city = c.long_name;
      if (t.indexOf("administrative_area_level_1") >= 0) state = c.short_name;
      if (t.indexOf("country") >= 0) country = c.long_name;
      if (t.indexOf("postal_code") >= 0) postal = c.long_name;
    });

    var addressLine = [streetNumber, route].filter(Boolean).join(" ").trim();
    if (!addressLine && place.formatted_address) {
      addressLine = place.formatted_address.split(",")[0];
    }

    var cityLabel = [city, state].filter(Boolean).join(", ");
    if (!cityLabel && place.formatted_address) {
      var parts = place.formatted_address.split(",").map(function (p) { return p.trim(); });
      if (parts.length >= 2) cityLabel = parts[1] + (parts[2] ? ", " + parts[2].split(" ")[0] : "");
    }

    var addressEl = byIdOrSel(opts.addressSel) || document.querySelector("[data-places-fill='address']");
    var cityEl = byIdOrSel(opts.citySel) || document.querySelector("[data-places-fill='city']");
    var latEl = byIdOrSel(opts.latSel) || document.querySelector("[data-places-fill='lat']");
    var lngEl = byIdOrSel(opts.lngSel) || document.querySelector("[data-places-fill='lng']");

    if (opts.mode === "city") {
      if (cityEl) {
        cityEl.value = cityLabel || place.formatted_address || cityEl.value;
        cityEl.dispatchEvent(new Event("change", { bubbles: true }));
      }
    } else {
      if (addressEl && addressLine) {
        addressEl.value = addressLine;
        addressEl.dispatchEvent(new Event("change", { bubbles: true }));
      }
      if (cityEl && cityLabel) {
        cityEl.value = cityLabel;
        cityEl.dispatchEvent(new Event("change", { bubbles: true }));
      }
    }

    if (place.geometry && place.geometry.location) {
      var lat = place.geometry.location.lat();
      var lng = place.geometry.location.lng();
      if (latEl) {
        latEl.value = typeof lat === "number" ? lat.toFixed(6) : lat;
        latEl.dispatchEvent(new Event("change", { bubbles: true }));
      }
      if (lngEl) {
        lngEl.value = typeof lng === "number" ? lng.toFixed(6) : lng;
        lngEl.dispatchEvent(new Event("change", { bubbles: true }));
      }
    }
  }

  function attachOne(input) {
    if (!input || input._chomblyPlaces) return;
    var mode = (input.getAttribute("data-places") || "address").toLowerCase();
    var opts = {
      mode: mode,
      addressSel: input.getAttribute("data-places-address"),
      citySel: input.getAttribute("data-places-city"),
      latSel: input.getAttribute("data-places-lat"),
      lngSel: input.getAttribute("data-places-lng")
    };

    var options = {
      fields: ["address_components", "formatted_address", "geometry", "name"],
      componentRestrictions: { country: ["us", "co", "mx", "es"] }
    };
    if (mode === "city") {
      options.types = ["(cities)"];
    } else {
      options.types = ["address"];
    }

    var ac = new google.maps.places.Autocomplete(input, options);
    if (global.CHOMBLY_MAPS && global.CHOMBLY_MAPS.defaultLat != null) {
      var circle = new google.maps.Circle({
        center: {
          lat: Number(global.CHOMBLY_MAPS.defaultLat),
          lng: Number(global.CHOMBLY_MAPS.defaultLng)
        },
        radius: 80000
      });
      ac.setBounds(circle.getBounds());
    }

    ac.addListener("place_changed", function () {
      fillFromPlace(ac.getPlace(), opts);
    });

    input._chomblyPlaces = ac;
    input.setAttribute("autocomplete", "off");
    input.placeholder = input.placeholder || (mode === "city" ? "Busca tu ciudad…" : "Busca la dirección…");
  }

  function initChomblyPlaces() {
    if (!global.google || !google.maps || !google.maps.places) {
      console.warn("Google Places no cargó.");
      return;
    }
    document.querySelectorAll("[data-places]").forEach(attachOne);
  }

  global.initChomblyPlaces = initChomblyPlaces;
  global.chomblyRefreshPlaces = initChomblyPlaces;
})(window);
