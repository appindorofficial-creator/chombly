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
    if (!href || href.charAt(0) === '#') return;
    if (/^(tel|mailto|sms|whatsapp):/i.test(href) || href.indexOf('javascript:') === 0) return;
    try {
      var url = new URL(a.href, window.location.href);
      if (!/^https?:$/i.test(url.protocol)) return;
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
    overlay.classList.remove('ch-confirm-overlay--apple');
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

  function resetStuckSubmitButtons(form) {
    if (!form) return;
    form.querySelectorAll('button[type="submit"], button.is-loading, button[aria-busy="true"], #biz-continue').forEach(function (btn) {
      btn.disabled = false;
      btn.removeAttribute('aria-busy');
      btn.classList.remove('is-loading', 'is-success');
      if (btn.dataset.feedbackHtml != null) {
        btn.innerHTML = btn.dataset.feedbackHtml;
      } else if (btn.dataset.idleLabel != null) {
        btn.textContent = btn.dataset.idleLabel;
      }
    });
  }

  function bindAcceptTerms(root) {
    (root || document).querySelectorAll('form').forEach(function (form) {
      if (form.dataset.termsBound === '1') return;
      var cb = form.querySelector('[data-terms-checkbox], input[name="AcceptTerms"][type="checkbox"]');
      if (!cb) return;
      form.dataset.termsBound = '1';
      var err = form.querySelector('[data-terms-error]');
      var renewal = form.querySelector('[data-care-renewal], input[name="AcceptRenewal"][type="checkbox"]');

      function termsReady() {
        return !!(cb.checked && (!renewal || renewal.checked));
      }

      function syncTermsSubmit() {
        var ready = termsReady();
        form.querySelectorAll('[data-terms-submit]').forEach(function (btn) {
          btn.disabled = !ready;
          btn.setAttribute('aria-disabled', ready ? 'false' : 'true');
        });
        if (ready) setTermsErrorVisible(err, false);
      }

      cb.addEventListener('change', syncTermsSubmit);
      if (renewal) renewal.addEventListener('change', syncTermsSubmit);
      syncTermsSubmit();

      form.addEventListener('submit', function (e) {
        var submitter = e.submitter;
        // Promo "Aplicar" and other non-gated submits stay available.
        if (submitter && !submitter.hasAttribute('data-terms-submit')) return;

        if (termsReady()) {
          setTermsErrorVisible(err, false);
          return;
        }
        e.preventDefault();
        e.stopPropagation();
        resetStuckSubmitButtons(form);
        setTermsErrorVisible(err, true);
        try {
          if (!cb.checked) cb.focus();
          else if (renewal) renewal.focus();
        } catch (_) { }
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

        // Radio groups: .value is set even when unchecked; check the group instead.
        if (type === 'radio') {
          if (!el.name) return;
          var group = form.querySelectorAll('input[type="radio"][name="' + el.name.replace(/"/g, '\\"') + '"]');
          var needsRequired = false;
          var reqMsg = msgs.required;
          Array.prototype.forEach.call(group, function (r) {
            if (r.required || r.getAttribute('data-msg-required')) {
              needsRequired = needsRequired || !!r.required;
              if (r.getAttribute('data-msg-required')) reqMsg = r.getAttribute('data-msg-required');
            }
          });
          if (needsRequired) {
            var anyChecked = Array.prototype.some.call(group, function (r) { return r.checked; });
            if (!anyChecked && el.required) {
              el.setCustomValidity(el.getAttribute('data-msg-required') || reqMsg || msgs.required);
            }
          }
          return;
        }

        if (type === 'checkbox') {
          if (el.name) {
            var cbGroup = form.querySelectorAll('input[type="checkbox"][name="' + el.name.replace(/"/g, '\\"') + '"]');
            if (cbGroup.length > 1) {
              var cbNeedsRequired = false;
              var cbReqMsg = msgs.required;
              Array.prototype.forEach.call(cbGroup, function (c) {
                if (c.required || c.getAttribute('data-msg-required')) {
                  cbNeedsRequired = true;
                  if (c.getAttribute('data-msg-required')) cbReqMsg = c.getAttribute('data-msg-required');
                }
              });
              if (cbNeedsRequired) {
                var cbAny = Array.prototype.some.call(cbGroup, function (c) { return c.checked; });
                var cbAnchor = null;
                Array.prototype.forEach.call(cbGroup, function (c) {
                  if (!cbAnchor && (c.required || c.getAttribute('data-msg-required'))) cbAnchor = c;
                });
                if (!cbAnchor) cbAnchor = cbGroup[0];
                if (!cbAny && el === cbAnchor) {
                  el.setCustomValidity(el.getAttribute('data-msg-required') || cbReqMsg || msgs.required);
                } else if (cbAny) {
                  Array.prototype.forEach.call(cbGroup, function (c) { c.setCustomValidity(''); });
                }
              }
              return;
            }
          }
          if (el.required && !el.checked) {
            el.setCustomValidity(el.getAttribute('data-msg-required') || msgs.required);
          }
          return;
        }

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

      function isVisuallyHiddenControl(el) {
        if (!el || !el.getBoundingClientRect) return false;
        var rect = el.getBoundingClientRect();
        if (rect.width < 2 || rect.height < 2) return true;
        try {
          var style = window.getComputedStyle(el);
          if (style.opacity === '0' || style.visibility === 'hidden') return true;
        } catch (_) { /* ignore */ }
        return false;
      }

      function showInvalidFeedback(invalid) {
        if (!invalid) {
          form.reportValidity();
          if (msgs.required) showAppToast(msgs.required, { kind: 'error' });
          return;
        }
        var msg = invalid.validationMessage
          || invalid.getAttribute('data-msg-required')
          || msgs.required
          || '';
        var group = invalid.closest('.form-group')
          || invalid.closest('.pet-care-panel')
          || invalid.closest('fieldset');
        var err = group ? group.querySelector('.field-error') : null;
        if (err && msg) {
          err.textContent = msg;
          err.hidden = false;
        }

        var scrollEl = invalid.closest('.temperament-pick, .species-opt, .size-pick, .care-flag, label')
          || group
          || invalid;
        if (scrollEl && typeof scrollEl.scrollIntoView === 'function') {
          scrollEl.scrollIntoView({ behavior: 'smooth', block: 'center' });
        }

        // Always toast — native bubbles often fail on hidden radios / custom UI.
        if (msg) showAppToast(msg, { kind: 'error' });

        if (!isVisuallyHiddenControl(invalid) && typeof invalid.reportValidity === 'function') {
          try { invalid.reportValidity(); } catch (_) { /* ignore */ }
        }
      }

      form.addEventListener('input', function (e) {
        if (!e.target) return;
        apply(e.target);
        var group = e.target.closest('.form-group');
        var err = group && group.querySelector('.field-error');
        if (err && e.target.validity && e.target.validity.valid) err.textContent = '';
      }, true);
      form.addEventListener('change', function (e) {
        if (!e.target) return;
        apply(e.target);
        var group = e.target.closest('.form-group');
        var err = group && group.querySelector('.field-error');
        if (err && e.target.validity && e.target.validity.valid) err.textContent = '';
      }, true);
      form.addEventListener('submit', function (e) {
        validateAll();
        if (!form.checkValidity()) {
          e.preventDefault();
          e.stopImmediatePropagation();
          showInvalidFeedback(form.querySelector(':invalid'));
        }
      }, true);
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
      form.addEventListener('submit', function (e) {
        // Validation / confirm handlers may cancel submit; never leave the button stuck.
        if (e.defaultPrevented) return;
        if (form.dataset.chConfirmBound === '1' && form.dataset.chConfirmReady !== '1') return;
        if (typeof form.checkValidity === 'function' && !form.checkValidity()) return;

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
    var softNavBusy = false;
    var softUpdatingTimer = null;
    var softAbort = null;

    function markUpdating(fromEl, on) {
      var body = (fromEl && fromEl.closest)
        ? (fromEl.closest('.home-body') || fromEl.closest('.app-shell'))
        : document.querySelector('.home-body');
      if (!body) return;
      if (softUpdatingTimer) {
        clearTimeout(softUpdatingTimer);
        softUpdatingTimer = null;
      }
      if (!on) {
        body.classList.remove('is-soft-updating');
        return;
      }
      // Delay indicator so fast responses never look like a reload flash
      softUpdatingTimer = setTimeout(function () {
        softUpdatingTimer = null;
        body.classList.add('is-soft-updating');
      }, 450);
    }

    function rebindAfterSoftReplace(scopeEl) {
      bindPhoneInputs(scopeEl);
      bindSoftFilters(scopeEl);
      bindToggleSingleChips(scopeEl);
      bindHotelSummaryToggle(document);
      bindCheckoutStickyCta(scopeEl);
      bindFeedbackForms(scopeEl);
      bindCopyActions(scopeEl);
      bindConfirmForms(scopeEl);
      bindFavoriteButtons(scopeEl);
      bindAcceptTerms(scopeEl);
      bindLocalizedValidation(scopeEl);
    }

    function findResultsAnchor(root) {
      if (!root) return null;
      return root.querySelector('[id$="-results"]')
        || root.querySelector('.groomers-results-title')
        || root.querySelector('.hotel-card, .groomer-card-wrap, .empty, [id$="-need-basics"]');
    }

    function resultsStartNode(root) {
      var anchor = findResultsAnchor(root);
      if (!anchor) return null;
      if (anchor.id && /-results$/.test(anchor.id)) return anchor;
      if (anchor.classList && anchor.classList.contains('groomers-results-title')) return anchor;
      if (anchor.classList && anchor.classList.contains('section-title')) return anchor;
      var prev = anchor.previousElementSibling;
      if (prev && prev.classList && prev.classList.contains('section-title')) return prev;
      return anchor;
    }

    function reuseImages(fromRoot, intoRoot) {
      if (!fromRoot || !intoRoot) return;
      var pool = {};
      Array.prototype.forEach.call(fromRoot.querySelectorAll('img[src]'), function (img) {
        var src = img.getAttribute('src');
        if (!src) return;
        if (!pool[src]) pool[src] = [];
        pool[src].push(img);
      });
      Array.prototype.forEach.call(intoRoot.querySelectorAll('img[src]'), function (img) {
        var src = img.getAttribute('src');
        var list = pool[src];
        if (!list || !list.length) return;
        var old = list.shift();
        if (old && old.parentNode) {
          img.replaceWith(old);
        }
      });
    }

    function preResultsSignature(form, resultsStart) {
      if (!form || !resultsStart) return '';
      var hasDateRow = 0;
      var hasCustomTime = 0;
      var hasPetError = 0;
      var filterCount = 0;
      var pastOrBusy = 0;
      var n = form.firstChild;
      while (n && n !== resultsStart) {
        if (n.nodeType === 1) {
          if ((n.matches && n.matches('.date-row')) || (n.querySelector && n.querySelector('.date-row'))) hasDateRow = 1;
          if (n.querySelector && n.querySelector('input[type="time"][name="CustomStart"]')) hasCustomTime = 1;
          if (n.querySelector && n.querySelector('.field-error')) hasPetError = 1;
          if (n.querySelector) {
            filterCount += n.querySelectorAll('.filter-check, .chip:not(.filter-check), .when-chip, .radio-card').length;
            pastOrBusy += n.querySelectorAll('.when-chip.is-disabled, .when-chip input:disabled').length;
          }
        }
        n = n.nextSibling;
      }
      // Pet count / selection must bust the signature or soft-nav keeps the old picker UI.
      var petCountEl = form.querySelector('input[name="PetCount"]');
      var petCount = petCountEl ? String(petCountEl.value || '') : '';
      var stepperStrong = form.querySelector('.stepper strong');
      if (stepperStrong && stepperStrong.textContent)
        petCount = String(stepperStrong.textContent).trim() || petCount;
      var petIdEl = form.querySelector('select[name="PetId"], input[name="PetId"]:not([type="checkbox"])');
      var petId = petIdEl ? String(petIdEl.value || '') : '';
      var petIds = Array.prototype.map.call(
        form.querySelectorAll('input[type="checkbox"][name="PetIds"]:checked'),
        function (el) { return String(el.value || ''); }
      ).sort().join(',');
      return [hasDateRow, hasCustomTime, hasPetError, filterCount, pastOrBusy, petCount, petId, petIds].join(':');
    }

    function replaceRangeBefore(parent, stopNode, nextNodes) {
      var remove = [];
      var n = parent.firstChild;
      while (n && n !== stopNode) {
        remove.push(n);
        n = n.nextSibling;
      }
      remove.forEach(function (el) { parent.removeChild(el); });
      nextNodes.forEach(function (el) {
        parent.insertBefore(el, stopNode);
      });
    }

    function collectUntil(node, stopNode) {
      var out = [];
      var n = node;
      while (n && n !== stopNode) {
        out.push(n);
        n = n.nextSibling;
      }
      return out;
    }

    function syncFilterChrome(curForm, nextForm) {
      if (!curForm || !nextForm) return;
      // Date fields: server may clamp past values; keep live inputs in sync
      Array.prototype.forEach.call(nextForm.querySelectorAll('input[type="date"]'), function (nextInput) {
        if (!nextInput.name) return;
        var curInput = curForm.querySelector(
          'input[type="date"][name="' + String(nextInput.name).replace(/"/g, '\\"') + '"]'
        );
        if (!curInput) return;
        var min = nextInput.getAttribute('min');
        var max = nextInput.getAttribute('max');
        if (min) curInput.setAttribute('min', min);
        if (max) curInput.setAttribute('max', max);
        else curInput.removeAttribute('max');
        curInput.value = nextInput.value || '';
      });
      // Occupied / disabled time slots without rebuilding the whole form
      Array.prototype.forEach.call(nextForm.querySelectorAll('.when-chip'), function (nextLab) {
        var input = nextLab.querySelector('input[type="radio"]');
        if (!input || !input.name) return;
        var curInput = curForm.querySelector(
          'input[type="radio"][name="' + input.name + '"][value="' + String(input.value).replace(/"/g, '\\"') + '"]'
        );
        if (!curInput) return;
        var curLab = curInput.closest('label');
        if (!curLab) return;
        curInput.disabled = !!input.disabled;
        if (!input.disabled)
          curInput.checked = !!input.checked;
        else if (input.disabled)
          curInput.checked = false;
        curLab.classList.toggle('is-disabled', nextLab.classList.contains('is-disabled'));
        curLab.classList.toggle('disabled', nextLab.classList.contains('disabled'));
        curLab.classList.toggle('active', !!curInput.checked);
        if (curLab.childNodes.length && nextLab.childNodes.length) {
          var curText = Array.prototype.filter.call(curLab.childNodes, function (n) { return n.nodeType === 3; });
          var nextText = Array.prototype.filter.call(nextLab.childNodes, function (n) { return n.nodeType === 3; });
          if (curText.length && nextText.length) {
            curText[curText.length - 1].textContent = nextText[nextText.length - 1].textContent;
          }
        }
      });

      // Pet count stepper (+/−) — keep display, hidden field, and hrefs in sync
      var nextPetCount = nextForm.querySelector('input[name="PetCount"]');
      var curPetCount = curForm.querySelector('input[name="PetCount"]');
      if (nextPetCount && curPetCount)
        curPetCount.value = nextPetCount.value || '';
      var nextStepper = nextForm.querySelector('.stepper');
      var curStepper = curForm.querySelector('.stepper');
      if (nextStepper && curStepper) {
        var nextStrong = nextStepper.querySelector('strong');
        var curStrong = curStepper.querySelector('strong');
        if (nextStrong && curStrong)
          curStrong.textContent = nextStrong.textContent;
        var nextBtns = nextStepper.querySelectorAll('a.stepper-btn');
        var curBtns = curStepper.querySelectorAll('a.stepper-btn');
        for (var si = 0; si < nextBtns.length && si < curBtns.length; si++) {
          var nh = nextBtns[si].getAttribute('href');
          if (nh != null) curBtns[si].setAttribute('href', nh);
        }
      }
      var nextPetSelect = nextForm.querySelector('select[name="PetId"]');
      var curPetSelect = curForm.querySelector('select[name="PetId"]');
      if (nextPetSelect && curPetSelect && nextPetSelect.value !== curPetSelect.value)
        curPetSelect.value = nextPetSelect.value;

      // Hotel multi-pet switches
      Array.prototype.forEach.call(nextForm.querySelectorAll('input[type="checkbox"][name="PetIds"]'), function (nextInput) {
        var curInput = curForm.querySelector(
          'input[type="checkbox"][name="PetIds"][value="' + String(nextInput.value).replace(/"/g, '\\"') + '"]'
        );
        if (!curInput) return;
        curInput.checked = !!nextInput.checked;
        curInput.disabled = !!nextInput.disabled;
        var curLab = curInput.closest('label.behavior-pet-row, label.hotel-pet-pick');
        var nextLab = nextInput.closest('label.behavior-pet-row, label.hotel-pet-pick');
        if (curLab && nextLab) curLab.className = nextLab.className;
      });

      // Optional preference chips (✓ prefix) without rebuilding filters
      Array.prototype.forEach.call(nextForm.querySelectorAll('label.filter-check'), function (nextLab) {
        var nextInput = nextLab.querySelector('input[type="checkbox"]');
        if (!nextInput || !nextInput.name) return;
        var curInput = curForm.querySelector(
          'input[type="checkbox"][name="' + nextInput.name + '"][value="' + String(nextInput.value).replace(/"/g, '\\"') + '"]'
        );
        if (!curInput) return;
        var curLab = curInput.closest('label');
        if (!curLab) return;
        curInput.checked = !!nextInput.checked;
        curLab.className = nextLab.className;
        var html = nextLab.innerHTML;
        curLab.innerHTML = html;
      });
    }

    function syncFlowChrome(cur, next) {
      // Groomers search filters / species chips live outside the search form
      var curBars = cur.querySelectorAll('.groomers-filters, .filter-bar.groomers-filters');
      var nextBars = next.querySelectorAll('.groomers-filters, .filter-bar.groomers-filters');
      for (var i = 0; i < curBars.length && i < nextBars.length; i++) {
        var imported = document.importNode(nextBars[i], true);
        reuseImages(curBars[i], imported);
        curBars[i].replaceWith(imported);
      }
      var curHint = cur.querySelector('.groomers-loc, #loc-hint');
      var nextHint = next.querySelector('.groomers-loc, #loc-hint');
      if (curHint && nextHint) {
        curHint.replaceWith(document.importNode(nextHint, true));
      } else if (curHint && !nextHint) {
        curHint.remove();
      } else if (!curHint && nextHint) {
        var results = findResultsAnchor(cur);
        if (results) results.parentNode.insertBefore(document.importNode(nextHint, true), results);
      }
      var curSearch = cur.querySelector('form.search-box, form[data-soft-filter]');
      var nextSearch = next.querySelector('form.search-box, form[data-soft-filter]');
      if (curSearch && nextSearch) {
        Array.prototype.forEach.call(nextSearch.querySelectorAll('input'), function (nextInput) {
          if (!nextInput.name) return;
          var curInput = curSearch.querySelector('[name="' + nextInput.name + '"]');
          if (curInput && 'value' in curInput) curInput.value = nextInput.value;
        });
      }
      var curHead = cur.querySelector('.groomers-head');
      var nextHead = next.querySelector('.groomers-head');
      if (curHead && nextHead) {
        curHead.replaceWith(document.importNode(nextHead, true));
      }
    }

    function replaceNodeRange(parent, fromNode, nextNodes) {
      if (!parent || !fromNode) return false;
      var remove = [];
      var n = fromNode;
      while (n) {
        remove.push(n);
        n = n.nextSibling;
      }
      remove.forEach(function (el) { parent.removeChild(el); });
      nextNodes.forEach(function (el) { parent.appendChild(el); });
      return true;
    }

    function collectFrom(node) {
      var out = [];
      var n = node;
      while (n) {
        out.push(n);
        n = n.nextSibling;
      }
      return out;
    }

    function swapResultsSlice(curParent, curStart, nextParent, nextStart) {
      var nextSlice = collectFrom(nextStart).map(function (node) {
        return document.importNode(node, true);
      });
      var staging = document.createElement('div');
      nextSlice.forEach(function (el) { staging.appendChild(el); });
      reuseImages(curParent, staging);
      var importedSlice = Array.prototype.slice.call(staging.childNodes);
      replaceNodeRange(curParent, curStart, importedSlice);
    }

    function applySoftHotelFlow(cur, next) {
      var y = window.scrollY || window.pageYOffset || 0;
      var curForm = cur.querySelector('form[id$="-filter-form"]') || cur.querySelector('form');
      var nextForm = next.querySelector('form[id$="-filter-form"]') || next.querySelector('form');
      var curStartInForm = resultsStartNode(curForm);
      var nextStartInForm = resultsStartNode(nextForm);

      // Booking flows: Hotel / Daycare / Walkers / Trainers (results inside filter form)
      if (curForm && nextForm && curStartInForm && nextStartInForm
          && curForm.contains(curStartInForm) && nextForm.contains(nextStartInForm)) {
        var curSig = preResultsSignature(curForm, curStartInForm);
        var nextSig = preResultsSignature(nextForm, nextStartInForm);
        if (curSig !== nextSig) {
          var preNext = collectUntil(nextForm.firstChild, nextStartInForm).map(function (node) {
            return document.importNode(node, true);
          });
          var preStage = document.createElement('div');
          preNext.forEach(function (el) { preStage.appendChild(el); });
          reuseImages(curForm, preStage);
          replaceRangeBefore(curForm, curStartInForm, Array.prototype.slice.call(preStage.childNodes));
          // resultsStart may have been invalidated if stop node moved — re-find
          curStartInForm = resultsStartNode(curForm) || curStartInForm;
        } else {
          syncFilterChrome(curForm, nextForm);
        }

        swapResultsSlice(curForm, curStartInForm, nextForm, nextStartInForm);

        var afterCur = [];
        var sib = curForm.nextSibling;
        while (sib) {
          afterCur.push(sib);
          sib = sib.nextSibling;
        }
        var afterNext = [];
        sib = nextForm.nextSibling;
        while (sib) {
          afterNext.push(document.importNode(sib, true));
          sib = sib.nextSibling;
        }
        afterCur.forEach(function (el) {
          if (el.parentNode === cur) cur.removeChild(el);
        });
        afterNext.forEach(function (el) { cur.appendChild(el); });

        cur.className = next.className;
        try { window.scrollTo(0, y); } catch (_) { }
        rebindAfterSoftReplace(cur);
        return cur;
      }

      // Groomers / list pages: results live as siblings of the search form
      var curFlowStart = resultsStartNode(cur);
      var nextFlowStart = resultsStartNode(next);
      if (curFlowStart && nextFlowStart && cur.contains(curFlowStart) && next.contains(nextFlowStart)
          && (!curForm || !curForm.contains(curFlowStart))) {
        if (cur.querySelector('.groomers-filters')) {
          syncFlowChrome(cur, next);
          swapResultsSlice(cur, curFlowStart, next, nextFlowStart);
        } else {
          // Behavior / similar: keep page heading, soft-swap from first filter section
          var curFrom = cur.querySelector('section') || curFlowStart;
          var nextFrom = next.querySelector('section') || nextFlowStart;
          swapResultsSlice(cur, curFrom, next, nextFrom);
        }
        cur.className = next.className;
        try { window.scrollTo(0, y); } catch (_) { }
        rebindAfterSoftReplace(cur);
        return cur;
      }

      // Fallback: full swap, but reuse decoded images to avoid flash
      var h = cur.getBoundingClientRect().height;
      if (h > 0) cur.style.minHeight = Math.ceil(h) + 'px';
      var imported = document.importNode(next, true);
      reuseImages(cur, imported);
      cur.replaceWith(imported);
      try { window.scrollTo(0, y); } catch (_) { }
      if (typeof requestAnimationFrame === 'function') {
        requestAnimationFrame(function () {
          window.scrollTo(0, y);
          imported.style.minHeight = '';
        });
      } else {
        imported.style.minHeight = '';
      }
      rebindAfterSoftReplace(imported);
      return imported;
    }

    function syncBookingSummary(doc) {
      if (!doc) return;
      var nextSummary = doc.querySelector('[data-hotel-summary]');
      var curSummary = document.querySelector('[data-hotel-summary]');

      function bindSummary(el) {
        if (!el) return;
        rebindAfterSoftReplace(el);
        bindHotelSummaryToggle(document);
        // Open sheet so the user can confirm after choosing a provider
        el.classList.remove('is-collapsed');
        var flow = document.querySelector('.hotel-flow');
        if (flow) flow.classList.remove('is-summary-collapsed');
        el.querySelectorAll('[data-summary-toggle].hotel-summary-toggle').forEach(function (btn) {
          btn.setAttribute('aria-expanded', 'true');
          var min = btn.querySelector('[data-label-min]');
          var max = btn.querySelector('[data-label-max]');
          if (min) min.hidden = false;
          if (max) max.hidden = true;
        });
      }

      if (nextSummary && curSummary) {
        var imported = document.importNode(nextSummary, true);
        reuseImages(curSummary, imported);
        curSummary.replaceWith(imported);
        bindSummary(imported);
        return;
      }

      if (nextSummary && !curSummary) {
        var add = document.importNode(nextSummary, true);
        var flow = document.querySelector('.hotel-flow');
        var clearance = document.querySelector('.bottom-nav-clearance');
        var host = (flow && flow.parentNode) || document.body;
        if (clearance && clearance.parentNode === host) {
          host.insertBefore(add, clearance);
        } else if (flow && flow.nextSibling) {
          host.insertBefore(add, flow.nextSibling);
        } else {
          host.appendChild(add);
        }
        bindSummary(add);
        return;
      }

      if (!nextSummary && curSummary) {
        curSummary.remove();
        var flowOnly = document.querySelector('.hotel-flow');
        if (flowOnly) flowOnly.classList.remove('has-summary', 'is-summary-collapsed');
      }
    }

    function softNavigateHotelFlow(form, submitter) {
      if (!form) return;
      var method = (form.getAttribute('method') || 'get').toLowerCase();
      if (method !== 'get') {
        if (typeof form.requestSubmit === 'function') form.requestSubmit();
        else form.submit();
        return;
      }

      var action = form.getAttribute('action') || window.location.pathname;
      var params = new URLSearchParams(new FormData(form));
      if (submitter && submitter.name) {
        params.set(submitter.name, submitter.value == null ? '' : String(submitter.value));
      }
      // Drop empty optional params for cleaner URLs
      Array.from(params.keys()).forEach(function (k) {
        var v = params.get(k);
        if (v === '' || v == null) params.delete(k);
      });
      var url = action + (params.toString() ? ('?' + params.toString()) : '');

      if (softAbort) {
        try { softAbort.abort(); } catch (_) { }
      }
      softAbort = typeof AbortController !== 'undefined' ? new AbortController() : null;
      softNavBusy = true;
      storeFlowScrollY();
      markUpdating(form, true);

      fetch(url, {
        method: 'GET',
        credentials: 'same-origin',
        signal: softAbort ? softAbort.signal : undefined,
        headers: {
          'Accept': 'text/html',
          'X-Requested-With': 'XMLHttpRequest'
        }
      }).then(function (res) {
        if (!res.ok) throw new Error('soft-nav ' + res.status);
        return res.text();
      }).then(function (html) {
        var doc = new DOMParser().parseFromString(html, 'text/html');
        var next = doc.querySelector('.hotel-flow');
        var cur = document.querySelector('.hotel-flow');
        if (!next || !cur) {
          window.location.assign(url);
          return;
        }
        var live = applySoftHotelFlow(cur, next);
        syncBookingSummary(doc);
        try { history.replaceState(null, '', url); } catch (_) { }
        markUpdating(live || document.querySelector('.hotel-flow'), false);
      }).catch(function (err) {
        if (err && err.name === 'AbortError') {
          markUpdating(document.querySelector('.hotel-flow') || form, false);
          return;
        }
        window.location.assign(url);
      }).finally(function () {
        softNavBusy = false;
      });
    }

    /** Sticky continue must POST current form state (Notes, pets…) not a stale ContinueHref. */
    function bindCheckoutStickyCta(root) {
      var scope = root || document;
      scope.querySelectorAll('a.checkout-sticky-cta').forEach(function (a) {
        if (a.dataset.payFormBound === '1') return;
        a.dataset.payFormBound = '1';
        a.addEventListener('click', function (e) {
          var flow = a.closest('.hotel-flow') || document.querySelector('.hotel-flow');
          if (!flow) return;
          var form = flow.querySelector('form[id$="-filter-form"]') || flow.querySelector('form');
          if (!form) return;
          var method = (form.getAttribute('method') || 'get').toLowerCase();
          if (method !== 'get') return;

          e.preventDefault();
          e.stopImmediatePropagation();

          var pay = form.querySelector('input[name="Pay"], input[name="pay"]');
          if (!pay) {
            pay = document.createElement('input');
            pay.type = 'hidden';
            pay.name = 'Pay';
            form.appendChild(pay);
          }
          pay.value = 'true';
          softNavigateHotelFlow(form, null);
        }, true);
      });
    }

    // Expose for inline fallbacks if needed
    window.chomblySoftFilter = softNavigateHotelFlow;

    // HTMLFormElement.submit() (used by onchange="this.form.submit()") does NOT fire "submit".
    // Route those through soft-nav so chips never do a full document reload.
    if (!HTMLFormElement.prototype.__chomblySoftSubmitPatched) {
      var nativeSubmit = HTMLFormElement.prototype.submit;
      HTMLFormElement.prototype.submit = function () {
        try {
          if (this && this.closest && this.closest('.hotel-flow')) {
            var m = (this.getAttribute('method') || 'get').toLowerCase();
            if (m === 'get') {
              softNavigateHotelFlow(this, null);
              return;
            }
          }
        } catch (_) { }
        return nativeSubmit.apply(this, arguments);
      };
      HTMLFormElement.prototype.__chomblySoftSubmitPatched = true;
    }

    // Keep date pickers from accepting past days (browsers often allow typing past min=).
    scope.querySelectorAll('.hotel-flow input[type="date"][min]').forEach(function (el) {
      if (el.dataset.dateClampBound === '1') return;
      el.dataset.dateClampBound = '1';
      function clampDateInput() {
        var min = el.getAttribute('min');
        if (!min || !el.value) return false;
        var changed = false;
        if (el.value < min) {
          el.value = min;
          changed = true;
        }
        var max = el.getAttribute('max');
        if (max && el.value > max) {
          el.value = max;
          changed = true;
        }
        return changed;
      }
      clampDateInput();
      el.addEventListener('input', clampDateInput);
      el.addEventListener('change', clampDateInput);
      el.addEventListener('blur', clampDateInput);
    });

    scope.querySelectorAll('.hotel-flow form').forEach(function (form) {
      if (form.dataset.softNavBound === '1') return;
      var method = (form.getAttribute('method') || 'get').toLowerCase();
      if (method !== 'get') return;
      form.dataset.softNavBound = '1';

      form.addEventListener('change', function (e) {
        var t = e.target;
        if (!t || !form.contains(t)) return;
        if (t.matches && t.matches('input[type="date"][min]')) {
          var min = t.getAttribute('min');
          if (min && t.value && t.value < min) t.value = min;
        }
        // Notes: wait for blur-driven change only (already onchange); still soft-nav once
        softNavigateHotelFlow(form);
      });

      // Capture submit (requestSubmit / Enter / choose-provider buttons) before full navigation
      form.addEventListener('submit', function (e) {
        e.preventDefault();
        softNavigateHotelFlow(form, e.submitter || null);
      });
    });

    // Sticky "Continuar" must include live form fields (Notes, pets, etc.) — not a stale ?pay= URL.
    bindCheckoutStickyCta(scope);

    // Same-path filter links (stepper +/−, choose provider) → soft fetch
    scope.querySelectorAll('.hotel-flow a[href]').forEach(function (a) {
      if (a.dataset.softLinkBound === '1') return;
      a.dataset.softLinkBound = '1';
      a.addEventListener('click', function (e) {
        if (a.target === '_blank' || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return;
        if (a.hasAttribute('download')) return;
        var href = a.getAttribute('href') || '';
        if (!href || href.charAt(0) === '#') return;
        // Never soft-nav external apps / dialers (Emergency Call now, mailto, etc.)
        if (/^(tel|mailto|sms|whatsapp):/i.test(href) || href.indexOf('javascript:') === 0) return;
        var url;
        try {
          url = new URL(a.href, window.location.href);
          if (!/^https?:$/i.test(url.protocol)) return;
          if (url.origin !== window.location.origin) return;
          var curPath = (window.location.pathname || '').replace(/\/$/, '');
          var nextPath = (url.pathname || '').replace(/\/$/, '');
          if (curPath !== nextPath) return;
        } catch (_) {
          return;
        }
        e.preventDefault();
        if (softAbort) {
          try { softAbort.abort(); } catch (_) { }
        }
        softAbort = typeof AbortController !== 'undefined' ? new AbortController() : null;
        softNavBusy = true;
        storeFlowScrollY();
        markUpdating(a, true);
        fetch(url.toString(), {
          method: 'GET',
          credentials: 'same-origin',
          signal: softAbort ? softAbort.signal : undefined,
          headers: { 'Accept': 'text/html', 'X-Requested-With': 'XMLHttpRequest' }
        }).then(function (res) {
          if (!res.ok) throw new Error('soft-link ' + res.status);
          return res.text();
        }).then(function (html) {
          var doc = new DOMParser().parseFromString(html, 'text/html');
          var next = doc.querySelector('.hotel-flow');
          var cur = document.querySelector('.hotel-flow');
          if (!next || !cur) {
            window.location.assign(url.toString());
            return;
          }
          var live = applySoftHotelFlow(cur, next);
          syncBookingSummary(doc);
          try { history.replaceState(null, '', url.toString()); } catch (_) { }
          markUpdating(live || document.querySelector('.hotel-flow'), false);
        }).catch(function (err) {
          if (err && err.name === 'AbortError') {
            markUpdating(document.querySelector('.hotel-flow') || a, false);
            return;
          }
          window.location.assign(url.toString());
        }).finally(function () {
          softNavBusy = false;
        });
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

  // After tel:/app switch (Emergency "Call now"), iOS/Safari can leave the page
  // looking frozen or non-interactive. Clear transient locks on resume.
  function clearTransientUiLocks() {
    document.documentElement.classList.remove('ch-route-pending');
    document.querySelectorAll('.is-soft-updating').forEach(function (el) {
      el.classList.remove('is-soft-updating');
    });
    try {
      if (typeof document.getAnimations === 'function') {
        document.getAnimations().forEach(function (anim) {
          try { anim.cancel(); } catch (_) { }
        });
      }
    } catch (_) { }
  }

  window.addEventListener('pageshow', clearTransientUiLocks);
  document.addEventListener('visibilitychange', function () {
    if (document.visibilityState === 'visible') clearTransientUiLocks();
  });
  window.addEventListener('focus', clearTransientUiLocks);

  // Emergency / dialer links: never leave the page in a soft-updating lock.
  document.addEventListener('click', function (e) {
    var a = e.target && e.target.closest ? e.target.closest('a[data-external-action], a[href^="tel:"], a[href^="mailto:"]') : null;
    if (!a) return;
    clearTransientUiLocks();
    // Drop stuck :active / focus that can make buttons look disabled after returning.
    setTimeout(function () {
      try { if (a.blur) a.blur(); } catch (_) { }
      clearTransientUiLocks();
    }, 0);
  }, true);

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

      function getHowNums() {
        return Array.prototype.slice.call(shell.querySelectorAll('[data-how-glow] .how-num'))
          .filter(isVisible);
      }

      function getCta() {
        var primary = shell.querySelector('[data-appt-primary-cta]');
        var tabCta = shell.querySelector('[data-how-cta].tabs-cta, .tabs-cta[data-how-cta]');
        return isVisible(primary) ? primary : (isVisible(tabCta) ? tabCta : null);
      }

      var index = 0;
      var pulseOffTimer = null;

      function clearGlow() {
        shell.querySelectorAll('.is-glow').forEach(function (el) {
          el.classList.remove('is-glow');
        });
      }

      // Próximas: cycle how-steps + CTA every 1.5s.
      function tickHow() {
        var how = getHowNums();
        if (!how.length) return;
        clearGlow();
        var list = how.slice();
        var cta = getCta();
        if (cta) list.push(cta);
        if (!list.length) return;
        if (index >= list.length) index = 0;
        list[index].classList.add('is-glow');
        index = (index + 1) % list.length;
      }

      // Historial: soft halo on + Reservar every 4s (brief pulse, not sustained).
      function tickHistoryPulse() {
        if (getHowNums().length) return;
        var cta = getCta();
        if (!cta) return;
        clearGlow();
        cta.classList.add('is-glow');
        if (pulseOffTimer) window.clearTimeout(pulseOffTimer);
        pulseOffTimer = window.setTimeout(clearGlow, 1100);
      }

      tickHow();
      window.setInterval(tickHow, 1500);
      window.setTimeout(tickHistoryPulse, 800);
      window.setInterval(tickHistoryPulse, 4000);
    });
  }

  function bindFeaturedBanner(root) {
    var banners = (root || document).querySelectorAll('[data-featured-banner]');
    banners.forEach(function (banner) {
      if (banner.dataset.chFeaturedBound === '1') return;
      banner.dataset.chFeaturedBound = '1';

      var slides = Array.prototype.slice.call(banner.querySelectorAll('[data-featured-slide]'));
      var dotsHost = banner.querySelector('[data-featured-dots]');
      if (slides.length < 2) return;

      var index = Math.max(0, slides.findIndex(function (el) { return el.classList.contains('is-active'); }));
      if (index < 0) index = 0;
      var timer = null;
      var reduceMotion = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
      var intervalMs = 3800;

      function renderDots() {
        if (!dotsHost) return;
        dotsHost.innerHTML = '';
        slides.forEach(function (_, i) {
          var btn = document.createElement('button');
          btn.type = 'button';
          btn.className = 'featured-banner-dot' + (i === index ? ' is-active' : '');
          btn.setAttribute('aria-label', 'Featured ' + (i + 1));
          btn.addEventListener('click', function () {
            show(i, true);
          });
          dotsHost.appendChild(btn);
        });
      }

      function show(next, userDriven) {
        if (next === index) return;
        var prev = slides[index];
        var cur = slides[next];
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
        show((index + 1) % slides.length, false);
      }

      function restart() {
        if (timer) window.clearInterval(timer);
        if (reduceMotion) return;
        timer = window.setInterval(next, intervalMs);
      }

      slides.forEach(function (el, i) {
        var active = i === index;
        el.classList.toggle('is-active', active);
        el.classList.remove('is-leaving');
        el.setAttribute('aria-hidden', active ? 'false' : 'true');
      });
      renderDots();
      restart();

      banner.addEventListener('pointerenter', function () {
        if (timer) window.clearInterval(timer);
        timer = null;
      });
      banner.addEventListener('pointerleave', restart);
    });
  }

  function bindNavReplace(root) {
    (root || document).querySelectorAll('a[data-nav-replace][href]').forEach(function (a) {
      if (a.dataset.navReplaceBound === '1') return;
      a.dataset.navReplaceBound = '1';
      a.addEventListener('click', function (e) {
        if (a.target === '_blank' || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return;
        var href = a.getAttribute('href') || '';
        if (!href || href.charAt(0) === '#' || href.indexOf('javascript:') === 0) return;
        e.preventDefault();
        var url = a.href;
        // Prefer assign: some webviews no-op on replace after preventDefault and leave the user stuck.
        try {
          if (typeof window.location.assign === 'function') window.location.assign(url);
          else window.location.href = url;
        } catch (_) {
          window.location.href = url;
        }
      });
    });
  }

  function bindHotelSummaryToggle(root) {
    var scope = root || document;
    var sheets = scope.querySelectorAll('[data-hotel-summary]');
    if (!sheets.length) return;

    sheets.forEach(function (sheet) {
      if (sheet.dataset.summaryBound === '1') return;
      // Full-page / sticky checkout — not the legacy collapsible sheet.
      if (sheet.hasAttribute('data-checkout-sticky') || sheet.hasAttribute('data-checkout-page')) return;
      sheet.dataset.summaryBound = '1';

      var flow = document.querySelector('.hotel-flow');
      var storageKey = 'chombly.summaryCollapsed:' + (location.pathname || '');
      var startCollapsed = sheet.hasAttribute('data-summary-start-collapsed');
      var stored = null;
      try { stored = sessionStorage.getItem(storageKey); } catch (_) { }

      function apply(collapsed) {
        sheet.classList.toggle('is-collapsed', collapsed);
        if (flow) flow.classList.toggle('is-summary-collapsed', collapsed);
        sheet.querySelectorAll('[data-summary-toggle]').forEach(function (btn) {
          if (btn.classList.contains('hotel-summary-toggle')) {
            btn.setAttribute('aria-expanded', collapsed ? 'false' : 'true');
            btn.setAttribute('aria-label', collapsed ? 'Expand' : 'Minimize');
            var min = btn.querySelector('[data-label-min]');
            var max = btn.querySelector('[data-label-max]');
            if (min) min.hidden = collapsed;
            if (max) max.hidden = !collapsed;
          }
        });
        try { sessionStorage.setItem(storageKey, collapsed ? '1' : '0'); } catch (_) { }
      }

      var collapsed = startCollapsed || stored === '1';
      // If user just got a validation error, force collapse so the form is usable.
      if (startCollapsed) collapsed = true;
      apply(collapsed);

      sheet.addEventListener('click', function (e) {
        var btn = e.target && e.target.closest ? e.target.closest('[data-summary-toggle]') : null;
        if (!btn || !sheet.contains(btn)) return;
        e.preventDefault();
        apply(!sheet.classList.contains('is-collapsed'));
      });

      // Grabber tap collapses when expanded (Apple sheet habit).
      var grabber = sheet.querySelector('.hotel-summary-grabber');
      if (grabber) {
        grabber.style.cursor = 'pointer';
        grabber.addEventListener('click', function () {
          if (!sheet.classList.contains('is-collapsed')) apply(true);
        });
      }
    });
  }

  function bindToggleSingleChips(root) {
    var scope = root || document;
    scope.querySelectorAll('.hotel-flow form').forEach(function (form) {
      if (form.dataset.toggleChipsBound === '1') return;
      form.dataset.toggleChipsBound = '1';

      function radioLabel(el) {
        var label = el && el.closest ? el.closest('label') : null;
        if (!label || !form.contains(label) || label.classList.contains('filter-check')) return null;
        var input = label.querySelector('input[type="radio"]');
        if (!input || input.disabled) return null;
        if (label.classList.contains('is-disabled') || label.classList.contains('disabled')) return null;
        return { label: label, input: input };
      }

      form.addEventListener('change', function (e) {
        var hit = radioLabel(e.target);
        if (!hit) return;
        form.querySelectorAll('input[type="radio"][name="' + hit.input.name + '"]').forEach(function (radio) {
          var lab = radio.closest('label');
          if (lab) lab.classList.toggle('active', radio.checked);
        });
      });

      form.addEventListener('click', function (e) {
        var hit = radioLabel(e.target);
        if (!hit) return;

        e.preventDefault();
        e.stopPropagation();

        var wasChecked = !!hit.input.checked;
        form.querySelectorAll('input[type="radio"][name="' + hit.input.name + '"]').forEach(function (radio) {
          radio.checked = false;
          var lab = radio.closest('label');
          if (lab) lab.classList.remove('active');
        });
        if (!wasChecked) {
          hit.input.checked = true;
          hit.label.classList.add('active');
        }

        if (typeof window.chomblySoftFilter === 'function') window.chomblySoftFilter(form);
        else if (typeof form.requestSubmit === 'function') form.requestSubmit();
        else form.submit();
      }, true);
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
    bindToggleSingleChips(document);
    bindSoftFilters(document);
    bindRoutePending();
    bindFlashAndAlerts();
    bindAcceptTerms(document);
    bindFavoriteButtons(document);
    bindTrustBanner(document);
    bindHowGlow(document);
    bindFeaturedBanner(document);
    bindNavReplace(document);
    bindHotelSummaryToggle(document);
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
