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

  function passwordToggleLabels() {
    var lang = (document.documentElement.lang || 'es').toLowerCase();
    if (lang.indexOf('en') === 0) {
      return { show: 'Show password', hide: 'Hide password' };
    }
    return { show: 'Mostrar contraseña', hide: 'Ocultar contraseña' };
  }

  function bindPasswordToggles(root) {
    var labels = passwordToggleLabels();
    (root || document).querySelectorAll('input[type="password"]').forEach(function (input) {
      if (input.dataset.passwordToggleBound === '1') return;
      if (input.getAttribute('data-pay-field') || input.getAttribute('data-no-password-toggle') === '1') return;
      input.dataset.passwordToggleBound = '1';

      var wrap = input.closest('.password-field');
      if (!wrap) {
        wrap = document.createElement('div');
        wrap.className = 'password-field';
        input.parentNode.insertBefore(wrap, input);
        wrap.appendChild(input);
      }

      var btn = wrap.querySelector('.password-toggle');
      if (!btn) {
        btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'password-toggle';
        btn.setAttribute('aria-label', labels.show);
        btn.setAttribute('title', labels.show);
        btn.innerHTML =
          '<svg class="password-toggle-icon password-toggle-icon--show" viewBox="0 0 24 24" aria-hidden="true"><path fill="currentColor" d="M12 5c-5 0-9.3 3.1-11 7 1.7 3.9 6 7 11 7s9.3-3.1 11-7c-1.7-3.9-6-7-11-7zm0 12a5 5 0 1 1 0-10 5 5 0 0 1 0 10zm0-2.5A2.5 2.5 0 1 0 12 9a2.5 2.5 0 0 0 0 5z"/></svg>' +
          '<svg class="password-toggle-icon password-toggle-icon--hide" viewBox="0 0 24 24" aria-hidden="true"><path fill="currentColor" d="M3.3 2.2 2 3.5l3.1 3.1C3.3 8 1.7 9.7 1 12c1.7 3.9 6 7 11 7 1.7 0 3.3-.4 4.7-1l3.3 3.3 1.3-1.3L3.3 2.2zM12 17c-3.7 0-6.9-2-8.5-5 .6-1.2 1.6-2.3 2.8-3.1l1.7 1.7A5 5 0 0 0 12 17zm0-10a5 5 0 0 1 4.9 4l2.1 2.1c.5-.6.9-1.3 1.2-2.1C18.9 7 14.7 5 12 5c-.7 0-1.3.1-2 .2l1.4 1.4c.2 0 .4-.1.6-.1z"/></svg>';
        wrap.appendChild(btn);
      }

      btn.addEventListener('click', function () {
        var revealing = input.type === 'password';
        input.type = revealing ? 'text' : 'password';
        wrap.classList.toggle('is-visible', revealing);
        btn.setAttribute('aria-label', revealing ? labels.hide : labels.show);
        btn.setAttribute('title', revealing ? labels.hide : labels.show);
        input.focus();
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

  function submitConfirmForm(form) {
    form.dataset.chConfirmReady = '1';
    if (typeof form.requestSubmit === 'function') form.requestSubmit();
    else form.submit();
  }

  function animateThenSubmit(form) {
    var removeSel = form.getAttribute('data-animate-remove');
    if (removeSel) {
      var target = form.closest(removeSel);
      if (target) {
        target.classList.add('is-removing');
        var done = false;
        function finish() {
          if (done) return;
          done = true;
          submitConfirmForm(form);
        }
        target.addEventListener('transitionend', finish, { once: true });
        setTimeout(finish, 340);
        return;
      }
    }

    var clearSel = form.getAttribute('data-animate-clear');
    if (clearSel) {
      var items = Array.prototype.slice.call(document.querySelectorAll(clearSel));
      if (items.length) {
        items.forEach(function (el, i) {
          setTimeout(function () { el.classList.add('is-removing'); }, i * 45);
        });
        setTimeout(function () { submitConfirmForm(form); }, items.length * 45 + 320);
        return;
      }
    }

    submitConfirmForm(form);
  }

  function bindNotifSwipe(root) {
    var list = (root || document).querySelectorAll('[data-notif-swipe]');
    list.forEach(function (card) {
      if (card.dataset.notifSwipeBound === '1') return;
      card.dataset.notifSwipeBound = '1';

      var startX = 0;
      var startY = 0;
      var dx = 0;
      var dragging = false;
      var axis = null;
      var maxSwipe = 88;

      function setX(x) {
        dx = Math.max(-maxSwipe, Math.min(0, x));
        card.style.transform = dx ? 'translateX(' + dx + 'px)' : '';
      }

      function reset() {
        card.classList.remove('is-dragging');
        setX(0);
        dragging = false;
        axis = null;
      }

      function triggerDelete() {
        var form = card.querySelector('form[data-animate-remove]');
        card.classList.remove('is-dragging');
        setX(-maxSwipe);
        setTimeout(function () {
          if (!form) { reset(); return; }
          if (typeof form.requestSubmit === 'function') form.requestSubmit();
          else form.submit();
          setTimeout(function () {
            if (!card.closest('.notif-swipe.is-removing')) reset();
          }, 80);
        }, 120);
      }

      card.addEventListener('pointerdown', function (e) {
        if (e.button != null && e.button !== 0) return;
        if (e.target.closest('button, a, input, label')) return;
        dragging = true;
        axis = null;
        startX = e.clientX;
        startY = e.clientY;
        dx = 0;
        card.classList.add('is-dragging');
        try { card.setPointerCapture(e.pointerId); } catch (_) { }
      });

      card.addEventListener('pointermove', function (e) {
        if (!dragging) return;
        var mx = e.clientX - startX;
        var my = e.clientY - startY;
        if (!axis) {
          if (Math.abs(mx) < 8 && Math.abs(my) < 8) return;
          axis = Math.abs(mx) > Math.abs(my) ? 'x' : 'y';
          if (axis === 'y') {
            dragging = false;
            card.classList.remove('is-dragging');
            card.style.transform = '';
            return;
          }
        }
        if (axis !== 'x') return;
        e.preventDefault();
        setX(mx);
      });

      function endDrag() {
        if (!dragging && !card.classList.contains('is-dragging')) return;
        card.classList.remove('is-dragging');
        if (dx <= -56) triggerDelete();
        else reset();
      }

      card.addEventListener('pointerup', endDrag);
      card.addEventListener('pointercancel', reset);
      card.addEventListener('lostpointercapture', function () {
        if (card.classList.contains('is-dragging')) endDrag();
      });
    });
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
            animateThenSubmit(form);
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

  function prefersReducedMotion() {
    return window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  }

  function bindPageTransitions() {
    // Cross-document View Transitions are driven by CSS:
    // @view-transition { navigation: auto; } + view-transition-name on shell regions.
    if (prefersReducedMotion()) return;
  }

  function showAppToast(message, opts) {
    opts = opts || {};
    var host = document.getElementById('app-toast-host');
    if (!host || !message) return;
    var el = document.createElement('div');
    el.className = 'app-toast' + (opts.kind === 'error' ? ' app-toast--error' : opts.kind === 'info' ? ' app-toast--info' : '');
    el.setAttribute('role', opts.kind === 'error' ? 'alert' : 'status');
    el.textContent = String(message);
    host.appendChild(el);
    var ttl = typeof opts.duration === 'number' ? opts.duration : 2800;
    setTimeout(function () {
      el.classList.add('is-leaving');
      setTimeout(function () { el.remove(); }, 200);
    }, ttl);
  }

  function feedbackLabel(form, key, fallback) {
    if (!form) return fallback;
    return form.getAttribute(key) || fallback;
  }

  function setButtonFeedback(btn, state, labels) {
    if (!btn) return;
    labels = labels || {};
    if (btn.dataset.feedbackHtml == null) {
      btn.dataset.feedbackHtml = btn.innerHTML;
    }
    if (state === 'loading') {
      btn.classList.add('is-loading', 'app-btn');
      btn.classList.remove('is-success');
      btn.disabled = true;
      btn.setAttribute('aria-busy', 'true');
      btn.innerHTML = '<span class="app-btn-spinner" aria-hidden="true"></span><span class="app-btn-label"></span>';
      btn.querySelector('.app-btn-label').textContent = labels.loading || '…';
      return;
    }
    if (state === 'done') {
      btn.classList.remove('is-loading');
      btn.classList.add('is-success', 'app-btn');
      btn.disabled = true;
      btn.innerHTML = '<span class="app-btn-check" aria-hidden="true">✓</span><span class="app-btn-label"></span>';
      btn.querySelector('.app-btn-label').textContent = labels.done || 'Listo';
      return;
    }
    btn.classList.remove('is-loading', 'is-success');
    btn.disabled = false;
    btn.removeAttribute('aria-busy');
    btn.innerHTML = btn.dataset.feedbackHtml;
  }

  function bindFeedbackForms(root) {
    (root || document).querySelectorAll('form[data-feedback]').forEach(function (form) {
      if (form.dataset.feedbackBound === '1') return;
      form.dataset.feedbackBound = '1';
      form.addEventListener('submit', function () {
        if (form.dataset.chConfirmBound === '1' && form.dataset.chConfirmReady !== '1') {
          // confirm modal will re-submit later
        }
        var kind = form.getAttribute('data-feedback') || 'save';
        var btn = form.querySelector('button[type="submit"], .btn-primary, .btn-enter, .app-btn');
        var loading = feedbackLabel(form, 'data-feedback-loading',
          kind === 'login' ? '…' : '…');
        var done = feedbackLabel(form, 'data-feedback-done', '✓');
        setButtonFeedback(btn, 'loading', { loading: loading, done: done });
      });
    });
  }

  function bindCopyActions(root) {
    (root || document).querySelectorAll('[data-copy]').forEach(function (el) {
      if (el.dataset.copyBound === '1') return;
      el.dataset.copyBound = '1';
      el.addEventListener('click', function (e) {
        e.preventDefault();
        var text = el.getAttribute('data-copy') || '';
        var okMsg = el.getAttribute('data-copy-ok') || '✓ Copiado';
        var failMsg = el.getAttribute('data-copy-fail') || 'No se pudo copiar';
        function ok() {
          showAppToast(okMsg, { kind: 'success' });
          el.classList.add('is-copied');
          setTimeout(function () { el.classList.remove('is-copied'); }, 1200);
        }
        function fail() { showAppToast(failMsg, { kind: 'error' }); }
        if (navigator.clipboard && navigator.clipboard.writeText) {
          navigator.clipboard.writeText(text).then(ok).catch(fail);
        } else {
          try {
            var ta = document.createElement('textarea');
            ta.value = text;
            document.body.appendChild(ta);
            ta.select();
            document.execCommand('copy');
            ta.remove();
            ok();
          } catch (_) { fail(); }
        }
      });
    });
  }

  function bindDownloadProgress(root) {
    (root || document).querySelectorAll('[data-download]').forEach(function (el) {
      if (el.dataset.downloadBound === '1') return;
      el.dataset.downloadBound = '1';
      el.addEventListener('click', function (e) {
        var href = el.getAttribute('href') || el.getAttribute('data-download');
        if (!href || href === '#') return;
        e.preventDefault();
        var barHost = el.querySelector('.app-download-bar') || document.createElement('span');
        barHost.className = 'app-download-bar';
        if (!barHost.parentNode) el.appendChild(barHost);
        barHost.style.width = '8%';
        showAppToast(el.getAttribute('data-download-loading') || 'Descargando…', { kind: 'info', duration: 1600 });
        var prog = 8;
        var tick = setInterval(function () {
          prog = Math.min(90, prog + 12);
          barHost.style.width = prog + '%';
        }, 180);
        fetch(href, { credentials: 'same-origin' }).then(function (res) {
          if (!res.ok) throw new Error('dl');
          return res.blob().then(function (blob) {
            var url = URL.createObjectURL(blob);
            var a = document.createElement('a');
            a.href = url;
            a.download = el.getAttribute('data-download-name') || '';
            document.body.appendChild(a);
            a.click();
            a.remove();
            URL.revokeObjectURL(url);
          });
        }).then(function () {
          clearInterval(tick);
          barHost.style.width = '100%';
          showAppToast(el.getAttribute('data-download-done') || '✓ Descarga lista', { kind: 'success' });
          setTimeout(function () { barHost.style.width = '0%'; }, 600);
        }).catch(function () {
          clearInterval(tick);
          barHost.style.width = '0%';
          showAppToast(el.getAttribute('data-download-fail') || 'Error al descargar', { kind: 'error' });
        });
      });
    });
  }

  function bindSoftFilters(root) {
    var scope = root || document;
    function markUpdating(fromEl) {
      var body = (fromEl && fromEl.closest) ? (fromEl.closest('.home-body') || fromEl.closest('.app-shell')) : document.querySelector('.home-body');
      if (!body) return;
      body.classList.add('is-soft-updating');
      var sk = body.querySelector('.app-soft-skeleton');
      if (!sk) {
        sk = document.createElement('div');
        sk.className = 'app-soft-skeleton';
        sk.innerHTML = '<app-skeleton></app-skeleton>';
        // plain HTML skeleton (tag helpers won't run client-side)
        sk.innerHTML =
          '<div class="app-skeleton app-skeleton--text" aria-hidden="true">' +
          '<span class="app-skeleton-line" style="width:100%"></span>' +
          '<span class="app-skeleton-line" style="width:92%"></span>' +
          '<span class="app-skeleton-line" style="width:70%"></span>' +
          '</div>';
        body.appendChild(sk);
      }
    }

    scope.querySelectorAll('.filter-bar a, .chip-row a, a.chip, .hotel-flow a[href]').forEach(function (a) {
      if (a.dataset.softFilterBound === '1') return;
      a.dataset.softFilterBound = '1';
      a.addEventListener('click', function () {
        if (a.target === '_blank') return;
        markUpdating(a);
      });
    });

    scope.querySelectorAll('.hotel-flow form, form[data-soft-filter]').forEach(function (form) {
      if (form.dataset.softFilterBound === '1') return;
      form.dataset.softFilterBound = '1';
      form.addEventListener('change', function () {
        markUpdating(form);
      }, true);
      form.addEventListener('submit', function () {
        markUpdating(form);
      });
    });
  }

  function bindRoutePending() {
    document.querySelectorAll('[data-bottom-nav] a').forEach(function (a) {
      a.addEventListener('click', function () {
        document.documentElement.classList.add('ch-route-pending');
      });
    });
  }

  function bindFlashAndAlerts() {
    var flash = document.getElementById('app-flash-toast');
    if (flash) {
      var msg = flash.getAttribute('data-toast');
      var kind = flash.getAttribute('data-toast-kind') || 'success';
      if (msg) showAppToast(msg, { kind: kind });
    }

    document.querySelectorAll('.alert-success').forEach(function (el) {
      if (el.dataset.toasted === '1') return;
      el.dataset.toasted = '1';
      var text = (el.textContent || '').trim();
      if (text) showAppToast('✓ ' + text.replace(/^✓\s*/, ''), { kind: 'success' });
      el.classList.add('alert-success--quiet');
    });

    document.querySelectorAll('.alert-danger').forEach(function (el) {
      el.classList.add('alert-danger--pop');
    });
  }

  function bindBottomNav(root) {
    var nav = (root || document).querySelector('[data-bottom-nav]');
    if (!nav || nav.dataset.chNavBound === '1') return;
    nav.dataset.chNavBound = '1';
    var links = Array.prototype.slice.call(nav.querySelectorAll('a'));
    if (!links.length) return;

    function setActiveIndex(i) {
      if (i < 0) i = 0;
      nav.style.setProperty('--nav-active', String(i));
    }

    var initial = links.findIndex(function (a) { return a.classList.contains('active'); });
    setActiveIndex(initial >= 0 ? initial : 0);

    links.forEach(function (a, i) {
      a.addEventListener('pointerdown', function () {
        setActiveIndex(i);
      });
    });
  }

  function bindTrustBanner(root) {
    var banners = (root || document).querySelectorAll('[data-trust-banner]');
    banners.forEach(function (banner) {
      if (banner.dataset.chTrustBound === '1') return;
      banner.dataset.chTrustBound = '1';

      var items = Array.prototype.slice.call(banner.querySelectorAll('[data-trust-item]'));
      var dotsHost = banner.querySelector('[data-trust-dots]');
      if (items.length < 2) return;

      var index = Math.max(0, items.findIndex(function (el) { return el.classList.contains('is-active'); }));
      if (index < 0) index = 0;
      var timer = null;
      var reduceMotion = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
      var intervalMs = 3400;

      function renderDots() {
        if (!dotsHost) return;
        dotsHost.innerHTML = '';
        items.forEach(function (_, i) {
          var btn = document.createElement('button');
          btn.type = 'button';
          btn.className = 'trust-banner-dot' + (i === index ? ' is-active' : '');
          btn.setAttribute('aria-label', 'Trust ' + (i + 1));
          btn.addEventListener('click', function () {
            show(i, true);
          });
          dotsHost.appendChild(btn);
        });
      }

      function show(next, userDriven) {
        if (next === index) return;
        var prev = items[index];
        var cur = items[next];
        if (!cur) return;

        if (prev) {
          prev.classList.remove('is-active');
          prev.classList.add('is-leaving');
          prev.setAttribute('aria-hidden', 'true');
          window.setTimeout(function () {
            prev.classList.remove('is-leaving');
          }, reduceMotion ? 0 : 280);
        }

        cur.setAttribute('aria-hidden', 'false');
        void cur.offsetWidth;
        cur.classList.add('is-active');
        index = next;
        renderDots();

        if (userDriven) restart();
      }

      function next() {
        show((index + 1) % items.length, false);
      }

      function restart() {
        if (timer) window.clearInterval(timer);
        if (reduceMotion) return;
        timer = window.setInterval(next, intervalMs);
      }

      items.forEach(function (el, i) {
        var active = i === index;
        el.classList.toggle('is-active', active);
        el.classList.remove('is-leaving');
        el.setAttribute('aria-hidden', active ? 'false' : 'true');
      });
      renderDots();
      restart();

      banner.addEventListener('pointerenter', function () {
        if (timer) window.clearInterval(timer);
      });
      banner.addEventListener('pointerleave', restart);
    });
  }

  function bindHowGlow(root) {
    var shells = (root || document).querySelectorAll('[data-appt-how]');
    shells.forEach(function (shell) {
      if (shell.dataset.chHowBound === '1') return;
      shell.dataset.chHowBound = '1';

      var reduceMotion = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
      if (reduceMotion) return;

      function isVisible(el) {
        if (!el) return false;
        var style = window.getComputedStyle(el);
        if (style.display === 'none' || style.visibility === 'hidden' || style.opacity === '0') return false;
        return !!(el.offsetWidth || el.offsetHeight || el.getClientRects().length);
      }

      function targets() {
        var list = Array.prototype.slice.call(shell.querySelectorAll('[data-how-glow] .how-num'));
        var primary = shell.querySelector('[data-appt-primary-cta]');
        var tabCta = shell.querySelector('[data-how-cta].tabs-cta, .tabs-cta[data-how-cta]');
        var cta = isVisible(primary) ? primary : (isVisible(tabCta) ? tabCta : null);
        if (cta) list.push(cta);
        return list;
      }

      var index = 0;
      var timer = null;

      function clearGlow(list) {
        shell.querySelectorAll('.is-glow').forEach(function (el) {
          el.classList.remove('is-glow');
        });
      }

      function tick() {
        var list = targets();
        clearGlow();
        if (!list.length) return;
        if (index >= list.length) index = 0;
        list[index].classList.add('is-glow');
        index = (index + 1) % list.length;
      }

      tick();
      window.setInterval(tick, 1500);
    });
  }

  document.addEventListener('DOMContentLoaded', function () {
    bindPhoneInputs(document);
    bindPasswordToggles(document);
    bindLocalizedValidation(document);
    restoreFlowScroll();
    bindConfirmForms(document);
    bindNotifSwipe(document);
    bindBottomNav(document);
    bindPageTransitions();
    bindFeedbackForms(document);
    bindCopyActions(document);
    bindDownloadProgress(document);
    bindSoftFilters(document);
    bindRoutePending();
    bindFlashAndAlerts();
    bindAcceptTerms(document);
    bindFavoriteButtons(document);
    bindTrustBanner(document);
    bindHowGlow(document);
    scrollToVisibleTermsError();
  });
  window.ChomblyBindPhones = bindPhoneInputs;
  window.ChomblyBindPasswordToggles = bindPasswordToggles;
  window.ChomblyBindLocalizedValidation = bindLocalizedValidation;
  window.ChomblyConfirm = openConfirm;
  window.ChomblyBindConfirmForms = bindConfirmForms;
  window.ChomblyBindNotifSwipe = bindNotifSwipe;
  window.ChomblyBindBottomNav = bindBottomNav;
  window.ChomblyToast = { show: showAppToast };
  window.ChomblyFeedback = {
    setButton: setButtonFeedback,
    toast: showAppToast
  };
  window.ChomblyBindAcceptTerms = bindAcceptTerms;
  window.ChomblyBindFavoriteButtons = bindFavoriteButtons;
})();
