// Chombly site helpers
(function () {
  function sanitizePhone(value) {
    // Solo dígitos, máx. 10 (número local)
    return String(value || '').replace(/\D/g, '').slice(0, 10);
  }

  function bindPhoneInputs(root) {
    (root || document).querySelectorAll('input[data-phone], input[name="Phone"], input#Phone').forEach(function (el) {
      if (el.dataset.phoneBound === '1') return;
      el.dataset.phoneBound = '1';
      el.setAttribute('inputmode', 'numeric');
      el.setAttribute('autocomplete', 'tel');
      el.setAttribute('maxlength', '10');
      el.setAttribute('pattern', '[0-9]{7,10}');
      el.addEventListener('input', function () {
        var before = el.value;
        var cleaned = sanitizePhone(before);
        if (cleaned !== before) el.value = cleaned;
      });
      el.addEventListener('paste', function (e) {
        e.preventDefault();
        var text = (e.clipboardData || window.clipboardData).getData('text');
        var start = el.selectionStart || 0;
        var end = el.selectionEnd || 0;
        el.value = sanitizePhone(el.value.slice(0, start) + text + el.value.slice(end));
      });
    });
  }

  // Preserve scroll across hotel-flow GET filter auto-submits (onchange → form.submit())
  // and same-page filter links (e.g. "Elegir paseador"). HTMLFormElement.submit() does
  // not fire "submit", so we listen for "change" instead.
  var FLOW_SCROLL_KEY = 'chombly.flowScroll';

  function storeFlowScrollY() {
    try {
      sessionStorage.setItem(FLOW_SCROLL_KEY, String(window.scrollY || window.pageYOffset || 0));
    } catch (_) { }
  }

  function saveFlowScroll(e) {
    var el = e.target;
    if (!el || !el.form) return;
    var form = el.form;
    if (!form.closest || !form.closest('.hotel-flow')) return;
    var method = (form.getAttribute('method') || 'get').toLowerCase();
    if (method !== 'get') return;
    storeFlowScrollY();
  }

  function saveFlowScrollOnNavClick(e) {
    var a = e.target && e.target.closest ? e.target.closest('a[href]') : null;
    if (!a || !a.closest || !a.closest('.hotel-flow, .hotel-summary')) return;
    if (a.target === '_blank' || a.hasAttribute('download')) return;
    var href = a.getAttribute('href') || '';
    if (!href || href.charAt(0) === '#' || href.indexOf('javascript:') === 0) return;
    try {
      var url = new URL(a.href, window.location.href);
      if (url.origin !== window.location.origin) return;
      var cur = (window.location.pathname || '').replace(/\/$/, '');
      var next = (url.pathname || '').replace(/\/$/, '');
      if (cur !== next) return;
    } catch (_) {
      return;
    }
    storeFlowScrollY();
  }

  function restoreFlowScroll() {
    if (!document.querySelector('.hotel-flow')) return;
    if ('scrollRestoration' in history) {
      try { history.scrollRestoration = 'manual'; } catch (_) { }
    }
    var raw;
    try {
      raw = sessionStorage.getItem(FLOW_SCROLL_KEY);
      sessionStorage.removeItem(FLOW_SCROLL_KEY);
    } catch (_) {
      return;
    }
    if (raw == null) return;
    var y = parseInt(raw, 10);
    if (isNaN(y)) return;
    var apply = function () { window.scrollTo(0, y); };
    apply();
    // Re-apply after layout/images so the browser cannot win a race back to top.
    if (typeof requestAnimationFrame === 'function') {
      requestAnimationFrame(function () {
        apply();
        requestAnimationFrame(apply);
      });
    }
    setTimeout(apply, 50);
    setTimeout(apply, 200);
  }

  document.addEventListener('change', saveFlowScroll, true);
  document.addEventListener('click', saveFlowScrollOnNavClick, true);

  // Branded confirm modal — use form[data-confirm] instead of window.confirm
  var confirmState = {
    overlay: null,
    onResult: null,
    previouslyFocused: null
  };

  function getFocusable(root) {
    return Array.prototype.slice.call(
      root.querySelectorAll('button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])')
    ).filter(function (el) {
      return el.offsetParent !== null || el === document.activeElement;
    });
  }

  function ensureConfirmOverlay() {
    if (confirmState.overlay) return confirmState.overlay;
    var overlay = document.createElement('div');
    overlay.className = 'ch-confirm-overlay';
    overlay.setAttribute('role', 'presentation');
    overlay.innerHTML =
      '<div class="ch-confirm-dialog" role="alertdialog" aria-modal="true" aria-labelledby="ch-confirm-title" aria-describedby="ch-confirm-msg" tabindex="-1">' +
        '<h2 id="ch-confirm-title"></h2>' +
        '<p id="ch-confirm-msg"></p>' +
        '<div class="ch-confirm-actions">' +
          '<button type="button" class="btn btn-block ch-confirm-keep" data-ch-confirm="no"></button>' +
          '<button type="button" class="btn btn-block ch-confirm-ok" data-ch-confirm="yes"></button>' +
        '</div>' +
      '</div>';
    document.body.appendChild(overlay);
    confirmState.overlay = overlay;

    overlay.addEventListener('click', function (e) {
      if (e.target === overlay) closeConfirm(false);
    });
    overlay.addEventListener('keydown', function (e) {
      if (e.key === 'Escape') {
        e.preventDefault();
        closeConfirm(false);
        return;
      }
      if (e.key !== 'Tab') return;
      var dialog = overlay.querySelector('.ch-confirm-dialog');
      var focusable = getFocusable(dialog);
      if (!focusable.length) return;
      var first = focusable[0];
      var last = focusable[focusable.length - 1];
      if (e.shiftKey && document.activeElement === first) {
        e.preventDefault();
        last.focus();
      } else if (!e.shiftKey && document.activeElement === last) {
        e.preventDefault();
        first.focus();
      }
    });
    overlay.querySelectorAll('[data-ch-confirm]').forEach(function (btn) {
      btn.addEventListener('click', function () {
        closeConfirm(btn.getAttribute('data-ch-confirm') === 'yes');
      });
    });
    return overlay;
  }

  function closeConfirm(confirmed) {
    var overlay = confirmState.overlay;
    if (!overlay || !overlay.classList.contains('is-open')) return;
    overlay.classList.remove('is-open');
    document.body.classList.remove('ch-confirm-open');
    var cb = confirmState.onResult;
    confirmState.onResult = null;
    var prev = confirmState.previouslyFocused;
    confirmState.previouslyFocused = null;
    if (prev && typeof prev.focus === 'function') {
      try { prev.focus(); } catch (_) { }
    }
    if (typeof cb === 'function') cb(!!confirmed);
  }

  function openConfirm(opts) {
    opts = opts || {};
    var overlay = ensureConfirmOverlay();
    overlay.querySelector('#ch-confirm-title').textContent = opts.title || '';
    overlay.querySelector('#ch-confirm-msg').textContent = opts.message || '';
    overlay.querySelector('[data-ch-confirm="no"]').textContent = opts.cancelLabel || 'No';
    overlay.querySelector('[data-ch-confirm="yes"]').textContent = opts.okLabel || 'OK';
    confirmState.previouslyFocused = document.activeElement;
    confirmState.onResult = opts.onResult || null;
    document.body.classList.add('ch-confirm-open');
    overlay.classList.add('is-open');
    var prefer = opts.focusOk
      ? overlay.querySelector('[data-ch-confirm="yes"]')
      : overlay.querySelector('[data-ch-confirm="no"]');
    setTimeout(function () { prefer && prefer.focus(); }, 0);
  }

  function bindConfirmForms(root) {
    (root || document).querySelectorAll('form[data-confirm]').forEach(function (form) {
      if (form.dataset.chConfirmBound === '1') return;
      form.dataset.chConfirmBound = '1';
      form.addEventListener('submit', function (e) {
        if (form.dataset.chConfirmReady === '1') {
          delete form.dataset.chConfirmReady;
          return;
        }
        e.preventDefault();
        openConfirm({
          title: form.getAttribute('data-confirm-title') || '',
          message: form.getAttribute('data-confirm-message') || form.getAttribute('data-confirm') || '',
          cancelLabel: form.getAttribute('data-confirm-cancel') || 'No',
          okLabel: form.getAttribute('data-confirm-ok') || 'OK',
          onResult: function (ok) {
            if (!ok) return;
            form.dataset.chConfirmReady = '1';
            if (typeof form.requestSubmit === 'function') form.requestSubmit();
            else form.submit();
          }
        });
      });
    });
  }

  function setTermsErrorVisible(err, show) {
    if (!err) return;
    if (show) {
      var msg = err.getAttribute('data-terms-message');
      if (msg) err.textContent = msg;
      err.hidden = false;
      err.removeAttribute('hidden');
    } else {
      err.hidden = true;
      err.setAttribute('hidden', '');
    }
  }

  function bindAcceptTerms(root) {
    (root || document).querySelectorAll('form').forEach(function (form) {
      if (form.dataset.termsBound === '1') return;
      var cb = form.querySelector('[data-terms-checkbox], input[name="AcceptTerms"][type="checkbox"]');
      if (!cb) return;
      form.dataset.termsBound = '1';
      var err = form.querySelector('[data-terms-error]');

      cb.addEventListener('change', function () {
        if (cb.checked) setTermsErrorVisible(err, false);
      });

      form.addEventListener('submit', function (e) {
        if (cb.checked) {
          setTermsErrorVisible(err, false);
          return;
        }
        e.preventDefault();
        e.stopPropagation();
        setTermsErrorVisible(err, true);
        try { cb.focus(); } catch (_) { }
        if (err && typeof err.scrollIntoView === 'function') {
          err.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
        }
      });
    });
  }

  function scrollToVisibleTermsError() {
    var err = document.querySelector('[data-terms-error]:not([hidden])');
    if (!err || err.hidden) return;
    if (typeof err.scrollIntoView === 'function') {
      err.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }
  }

  function bindLocalizedValidation(root) {
    (root || document).querySelectorAll('form[data-validate-i18n]').forEach(function (form) {
      if (form.dataset.validateBound === '1') return;
      form.dataset.validateBound = '1';
      form.setAttribute('novalidate', 'novalidate');

      var msgs = {
        required: form.getAttribute('data-msg-required') || '',
        email: form.getAttribute('data-msg-email') || '',
        phone: form.getAttribute('data-msg-phone') || '',
        password: form.getAttribute('data-msg-password') || '',
        passwordMismatch: form.getAttribute('data-msg-password-mismatch') || ''
      };

      function apply(el) {
        if (!el || !el.setCustomValidity) return;
        el.setCustomValidity('');
        if (el.disabled) return;

        var raw = el.value == null ? '' : String(el.value);
        var v = raw.trim();
        var type = (el.getAttribute('type') || '').toLowerCase();
        var name = (el.name || '').toLowerCase();
        var isPhone = el.getAttribute('data-phone') === '1' || name === 'phone';

        if (el.required && !v) {
          el.setCustomValidity(el.getAttribute('data-msg-required') || msgs.required);
          return;
        }
        if (!v) return;

        if (type === 'email' || name === 'email') {
          var emailOk = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v);
          if (!emailOk) el.setCustomValidity(msgs.email);
          return;
        }

        if (isPhone) {
          var digits = v.replace(/\D/g, '');
          if (digits.length < 7 || digits.length > 10) {
            el.setCustomValidity(msgs.phone);
          }
          return;
        }

        if (type === 'password') {
          var min = el.minLength > 0 ? el.minLength : 0;
          var needsPolicy = el.getAttribute('data-password-policy') === '1';
          if (needsPolicy) {
            if (!isStrongPassword(raw)) {
              el.setCustomValidity(msgs.password);
              return;
            }
          } else if (min && v.length < min) {
            el.setCustomValidity(msgs.password);
            return;
          }
          if (msgs.passwordMismatch && (name === 'confirmpassword' || name === 'confirm-password')) {
            var newPwd = form.querySelector('[name="NewPassword"], #NewPassword, [name="Password"][data-password-policy]');
            if (newPwd && String(newPwd.value || '') !== raw) {
              el.setCustomValidity(msgs.passwordMismatch);
            }
          }
        }
      }

      function isStrongPassword(pwd) {
        if (!pwd || pwd.length < 6) return false;
        var onlyDigits = true;
        var hasUpper = false;
        var hasSpecial = false;
        for (var i = 0; i < pwd.length; i++) {
          var c = pwd.charAt(i);
          var code = pwd.charCodeAt(i);
          if (c < '0' || c > '9') onlyDigits = false;
          if (code >= 65 && code <= 90) hasUpper = true;
          if (!((code >= 48 && code <= 57) || (code >= 65 && code <= 90) || (code >= 97 && code <= 122))) {
            hasSpecial = true;
          }
        }
        return !onlyDigits && hasUpper && hasSpecial;
      }

      function validateAll() {
        form.querySelectorAll('input, select, textarea').forEach(apply);
      }

      form.addEventListener('input', function (e) {
        if (e.target) apply(e.target);
      }, true);
      form.addEventListener('change', function (e) {
        if (e.target) apply(e.target);
      }, true);
      form.addEventListener('submit', function (e) {
        validateAll();
        if (!form.checkValidity()) {
          e.preventDefault();
          e.stopPropagation();
          form.reportValidity();
        }
      });
    });
  }

  function bindFavoriteButtons(root) {
    (root || document).querySelectorAll('[data-favorite-toggle]').forEach(function (btn) {
      if (btn.dataset.chFavBound === '1') return;
      btn.dataset.chFavBound = '1';
      btn.addEventListener('click', function (e) {
        e.preventDefault();
        e.stopPropagation();
        if (btn.classList.contains('is-busy')) return;

        var groomerId = btn.getAttribute('data-groomer-id');
        if (!groomerId) return;

        btn.classList.add('is-busy');
        var body = new URLSearchParams();
        body.set('groomerId', groomerId);

        fetch('/Account/ToggleFavorite?format=json', {
          method: 'POST',
          headers: {
            'Content-Type': 'application/x-www-form-urlencoded',
            'Accept': 'application/json',
            'X-Requested-With': 'XMLHttpRequest'
          },
          body: body,
          credentials: 'same-origin'
        }).then(function (res) {
          if (res.status === 401) {
            var next = '/Groomers/Details/' + encodeURIComponent(groomerId);
            window.location.href = '/Account/Login?returnUrl=' + encodeURIComponent(next);
            return null;
          }
          if (!res.ok) throw new Error('fav');
          return res.json();
        }).then(function (data) {
          if (!data || !data.ok) return;
          var on = !!data.isFavorite;
          btn.classList.toggle('is-on', on);
          btn.setAttribute('aria-pressed', on ? 'true' : 'false');
          var label = on
            ? (btn.getAttribute('data-label-remove') || 'Remove')
            : (btn.getAttribute('data-label-add') || 'Save');
          btn.setAttribute('aria-label', label);
          btn.setAttribute('title', label);
          if (btn.getAttribute('data-remove-card') === '1' && !on) {
            var wrap = btn.closest('.groomer-card-wrap');
            if (wrap) wrap.remove();
          }
        }).catch(function () {
          /* keep previous state */
        }).finally(function () {
          btn.classList.remove('is-busy');
        });
      });
    });
  }

  document.addEventListener('DOMContentLoaded', function () {
    bindPhoneInputs(document);
    bindLocalizedValidation(document);
    restoreFlowScroll();
    bindConfirmForms(document);
    bindAcceptTerms(document);
    bindFavoriteButtons(document);
    scrollToVisibleTermsError();
  });
  window.ChomblyBindPhones = bindPhoneInputs;
  window.ChomblyBindLocalizedValidation = bindLocalizedValidation;
  window.ChomblyConfirm = openConfirm;
  window.ChomblyBindConfirmForms = bindConfirmForms;
  window.ChomblyBindAcceptTerms = bindAcceptTerms;
  window.ChomblyBindFavoriteButtons = bindFavoriteButtons;
})();
