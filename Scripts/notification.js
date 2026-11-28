(function() {
  if (window.top !== window.self) return;
  const __bridgeToken = $$BRIDGE_TOKEN$$;
  if (window.__notificationOverrideInstalled) return;
  window.__notificationOverrideInstalled = true;

  const activeCustomNotifications = {};

  // Collapses newlines and repeated whitespace into a clean single-line preview string.
  function normalizePreviewText(value) {
    if (value === null || value === undefined) return '';
    return String(value).replace(/\s+/g, ' ').trim();
  }

  // Recursively extracts text from DOM nodes while preserving <img alt="..."> emoji characters.
  function extractElementText(node) {
    if (!node) return '';
    try {
      if (node.nodeType === 3) {
        return node.nodeValue || '';
      }
      const tag = (node.tagName || '').toUpperCase();
      if (tag === 'IMG') {
        return (typeof node.getAttribute === 'function' ? (node.getAttribute('data-plain-text') || node.getAttribute('alt')) : '') || '';
      }
      if (tag === 'BR') {
        return ' ';
      }
      if (node.childNodes && node.childNodes.length > 0) {
        let buf = '';
        for (let i = 0; i < node.childNodes.length; i++) {
          buf += extractElementText(node.childNodes[i]);
        }
        return buf;
      }
      if (node.children && node.children.length > 0) {
        const parts = [];
        for (let i = 0; i < node.children.length; i++) {
          const t = extractElementText(node.children[i]);
          if (t) parts.push(t);
        }
        if (parts.length > 0) return parts.join(' ');
      }
      return node.textContent || '';
    } catch(e) {}
    return node.textContent || '';
  }

  // Extracts visible text from a preview container while ignoring nested unread badge counters.
  function extractTextWithoutBadges(el) {
    if (!el) return '';
    try {
      if (typeof el.cloneNode === 'function') {
        const clone = el.cloneNode(true);
        const badges = clone.querySelectorAll('.Badge, .badge, .dialog-subtitle-badge, [class*="unread-count"], [class*="numberBadge"], [class*="badge"]');
        for (let i = 0; i < badges.length; i++) {
          if (badges[i] && badges[i].parentNode) {
            badges[i].parentNode.removeChild(badges[i]);
          }
        }
        return normalizePreviewText(extractElementText(clone));
      }
    } catch(e) {}
    return normalizePreviewText(extractElementText(el));
  }

  // Returns true if the URL is a known 1x1 spacer or generic platform logo/favicon rather than message media.
  function isIgnoredMediaUrl(url) {
    if (!url || typeof url !== 'string') return true;
    const trimmed = url.trim();
    if (!trimmed) return true;
    const lower = trimmed.toLowerCase();
    if (lower.startsWith('data:image/gif;base64,r0lgodlhaqab')) return true;
    if (lower.includes('favicon') || lower.includes('apple-touch-icon') || lower.includes('manifest-icon')) return true;
    if (/\/(logo|app-icon|pwa-icon|whatsapp-logo|telegram-logo|discord-logo)[\.\-_]/i.test(lower)) return true;
    return false;
  }

  // Checks whether a DOM <img> represents a message photo, sticker, or custom emoji worth showing as a thumbnail.
  function isUsefulThumbnailImg(img) {
    if (!img || typeof img.getAttribute !== 'function') return false;
    const src = img.getAttribute('src') || img.currentSrc || '';
    if (isIgnoredMediaUrl(src)) return false;

    const alt = (img.getAttribute('data-plain-text') || img.getAttribute('alt') || '').trim();
    const cls = (img.className || '').toLowerCase();
    // Skip standard Unicode emoji sprite <img> tags (their Unicode glyph is extracted cleanly by extractElementText).
    if (cls.includes('emoji') && alt && !/^:[a-z0-9_+-]+:$/i.test(alt) && !src.includes('cdn.discordapp.com/emojis/')) {
      return false;
    }

    if (typeof img.complete === 'boolean' && img.complete && typeof img.naturalWidth === 'number') {
      if (img.naturalWidth <= 4 || img.naturalHeight <= 4) return false;
    }
    return true;
  }

  // Finds the first message thumbnail image inside a DOM container, skipping contact avatars.
  function extractThumbnailFromContainer(container) {
    if (!container || typeof container.querySelectorAll !== 'function') {
      return { url: '', imgEl: null };
    }
    try {
      const imgs = container.querySelectorAll('img');
      for (let i = 0; i < imgs.length; i++) {
        const img = imgs[i];
        if (!isUsefulThumbnailImg(img)) continue;
        if (typeof img.closest === 'function') {
          if (img.closest('[data-testid="cell-frame-title"], .Avatar, [class*="avatar" i], [data-testid="default-user"]')) {
            continue;
          }
        }
        const src = img.getAttribute('src') || img.currentSrc || '';
        if (src) {
          return { url: src, imgEl: img };
        }
      }
    } catch(e) {}
    return { url: '', imgEl: null };
  }

  // Draws a loaded <img> onto a compact 64x64 canvas to safely convert blob/authenticated images into a portable data URL.
  function tryDrawImgToDataUrl(img) {
    try {
      if (!img || typeof document === 'undefined' || typeof document.createElement !== 'function') return '';
      if (typeof img.naturalWidth === 'number' && img.naturalWidth <= 4) return '';
      const size = 64;
      const canvas = document.createElement('canvas');
      canvas.width = size;
      canvas.height = size;
      const ctx = canvas.getContext('2d');
      if (!ctx) return '';
      const w = img.naturalWidth || size;
      const h = img.naturalHeight || size;
      const scale = Math.min(size / w, size / h);
      const dw = Math.max(1, Math.round(w * scale));
      const dh = Math.max(1, Math.round(h * scale));
      const dx = Math.round((size - dw) / 2);
      const dy = Math.round((size - dh) / 2);
      ctx.drawImage(img, dx, dy, dw, dh);
      return canvas.toDataURL('image/png');
    } catch(e) {
      return '';
    }
  }

  // Resolves a raw image URL or DOM <img> into a WPF-compatible thumbnail URL (data:image/... or https://...).
  function resolveThumbnailUrlAsync(rawUrl, imgEl, callback) {
    if (!rawUrl || isIgnoredMediaUrl(rawUrl)) {
      callback('');
      return;
    }
    if (rawUrl.startsWith('data:image/')) {
      callback(rawUrl.length <= 350000 ? rawUrl : '');
      return;
    }
    if (imgEl && imgEl.complete) {
      const drawn = tryDrawImgToDataUrl(imgEl);
      if (drawn) {
        callback(drawn);
        return;
      }
    }
    if (rawUrl.startsWith('blob:') && typeof Image === 'function' && typeof document !== 'undefined' && typeof document.createElement === 'function') {
      let done = false;
      const finish = function(val) {
        if (done) return;
        done = true;
        callback(val || '');
      };
      try {
        const loader = new Image();
        const timer = setTimeout(function() { finish(''); }, 180);
        loader.onload = function() {
          clearTimeout(timer);
          finish(tryDrawImgToDataUrl(loader));
        };
        loader.onerror = function() {
          clearTimeout(timer);
          finish('');
        };
        loader.src = rawUrl;
        return;
      } catch(e) {
        callback('');
        return;
      }
    }
    if (rawUrl.startsWith('https://') || rawUrl.startsWith('http://127.0.0.1') || rawUrl.startsWith('http://localhost')) {
      callback(rawUrl);
      return;
    }
    callback('');
  }

  // Cleans Discord aria-label strings by removing trailing unread/mention counters.
  function cleanDiscordChannelLabel(label) {
    const text = normalizePreviewText(label);
    if (!text) return '';
    return text
      .replace(/,\s*\d+\s*(?:unread|mention|mentions|non\s*lett\w*|menzion\w*|ungelesen\w*|no\s*le[ií]\w*|non\s*lu\w*).*$/i, '')
      .replace(/,\s*(?:unread|non\s*lett\w*).*$/i, '')
      .trim();
  }

  // Attempts to extract the latest chat title, message preview, and thumbnail from the active platform DOM.
  function getLatestDomPreview() {
    try {
      const host = (location.hostname || '').toLowerCase();
      if (host === 'web.telegram.org' || host === 'telegram.org' || host.endsWith('.telegram.org')) {
        return scanTelegramUnreadCount();
      }
      if (host === 'discord.com' || host.endsWith('.discord.com')) {
        return scanDiscordUnread();
      }
      if (host === 'web.whatsapp.com' || host === 'whatsapp.com' || host.endsWith('.whatsapp.com')) {
        return scanWhatsAppUnread();
      }
    } catch(e) {}
    return { latestChatTitle: '', latestMessageText: '', latestThumbnailUrl: '', latestThumbnailEl: null };
  }

  // --- CANALE DIAGNOSTICA AVANZATA (TODO #73) ---
  function sendDiagnostic(subType, details) {
    try {
      if (window.chrome && window.chrome.webview && typeof window.chrome.webview.postMessage === 'function') {
        window.chrome.webview.postMessage({
          channel: 'DiagnosticChannel',
          type: subType,
          details: details,
          bridgeToken: __bridgeToken
        });
      }
    } catch(e) {}
  }

  // Intercettazione errori JS non gestiti e Promise rejections
  try {
    window.addEventListener('error', function(ev) {
      try {
        sendDiagnostic('WINDOW_ERROR', {
          message: ev.message || 'Unknown error',
          filename: ev.filename || '',
          lineno: ev.lineno || 0,
          colno: ev.colno || 0,
          stack: ev.error && ev.error.stack ? String(ev.error.stack).substring(0, 2000) : ''
        });
      } catch(e) {}
    });

    window.addEventListener('unhandledrejection', function(ev) {
      try {
        const reason = ev.reason;
        const msg = reason ? (reason.message || String(reason)) : 'Unknown rejection';
        const stack = reason && reason.stack ? String(reason.stack).substring(0, 2000) : '';
        sendDiagnostic('UNHANDLED_REJECTION', { message: msg, stack: stack });
        if (msg.includes('Data lost due to missing file') || msg.includes('irrecoverable') || (msg.includes('IndexedDB') && msg.includes('NotReadableError'))) {
          sendDiagnostic('INDEXEDDB_CORRUPTED', { message: msg, stack: stack });
        }
      } catch(e) {}
    });

    window.addEventListener('online', function() { sendDiagnostic('NET_ONLINE', { timestamp: Date.now() }); });
    window.addEventListener('offline', function() { sendDiagnostic('NET_OFFLINE', { timestamp: Date.now() }); });
  } catch(e) {}

  // Intercettazione console.error e console.warn
  try {
    const origConsoleError = console.error.bind(console);
    const origConsoleWarn = console.warn.bind(console);
    console.error = function() {
      try {
        const argsStr = Array.from(arguments).map(a => {
          if (a instanceof Error) return a.message + '\n' + a.stack;
          if (typeof a === 'object') {
            try { return JSON.stringify(a); } catch(e) { return String(a); }
          }
          return String(a);
        }).join(' ');
        sendDiagnostic('CONSOLE_ERROR', { message: argsStr.substring(0, 2000) });
      } catch(e) {}
      return origConsoleError.apply(console, arguments);
    };

    console.warn = function() {
      try {
        const argsStr = Array.from(arguments).map(a => {
          if (typeof a === 'object') {
            try { return JSON.stringify(a); } catch(e) { return String(a); }
          }
          return String(a);
        }).join(' ');
        if (argsStr.includes('IndexedDB') || argsStr.includes('socket') || argsStr.includes('disconnect') ||
            argsStr.includes('auth') || argsStr.includes('storage') || argsStr.includes('wawc') ||
            argsStr.includes('LevelDB') || argsStr.includes('close') || argsStr.includes('terminat')) {
          sendDiagnostic('CONSOLE_WARN', { message: argsStr.substring(0, 2000) });
        }
      } catch(e) {}
      return origConsoleWarn.apply(console, arguments);
    };
  } catch(e) {}

  // Native transport (WebSocket) and storage (IndexedDB) remain 100% untouched.
  // Storage persistence and passive health diagnostics live in session-health.js.

  // Rilevamento dialoghi modali di errore/disconnessione nel DOM
  let lastReportedPopupText = '';
  function scanErrorPopups() {
    try {
      const dialogs = document.querySelectorAll('div[role="dialog"], [data-animate-modal-popup="true"], [data-testid="popup-contents"], .landing-wrapper, [data-testid="intro-text"]');
      for (let i = 0; i < dialogs.length; i++) {
        const dlg = dialogs[i];
        const text = (dlg.textContent || '').trim();
        if (!text) continue;
        const lower = text.toLowerCase();
        if (lower.includes('disconness') || lower.includes('logged out') || lower.includes('qualcosa è andato storto') ||
            lower.includes('something went wrong') || lower.includes('usa qui') || lower.includes('use here') ||
            lower.includes('sessione scaduta') || lower.includes('session expired') || lower.includes('connetti il telefono') ||
            lower.includes('phone not connected') || lower.includes('errore del browser') || lower.includes('out of memory')) {
          if (text !== lastReportedPopupText) {
            lastReportedPopupText = text;
            const buttons = Array.from(dlg.querySelectorAll('button, [role="button"]')).map(b => (b.textContent || '').trim());
            sendDiagnostic('POPUP_ERROR_DETECTED', {
              dialogText: text.substring(0, 1000),
              buttons: buttons,
              html: dlg.outerHTML.substring(0, 1500)
            });
          }
          break;
        }
      }
    } catch(e) {}
  }

  try {
    // 1. Intercept ServiceWorkerRegistration.prototype.showNotification (WhatsApp Web, Telegram PWA)
    if (window.ServiceWorkerRegistration && window.ServiceWorkerRegistration.prototype) {
      try {
        const originalShowNotification = window.ServiceWorkerRegistration.prototype.showNotification;
        window.ServiceWorkerRegistration.prototype.showNotification = function(title, options) {
          const id = Math.random().toString(36).substring(2, 9);
          try {
            let cleanTitle = normalizePreviewText(title);
            let cleanBody = normalizePreviewText(options ? (options.body || '') : '');
            let rawThumb = '';
            let thumbEl = null;
            if (options) {
              if (options.image && !isIgnoredMediaUrl(options.image)) rawThumb = options.image;
              else if (options.icon && !isIgnoredMediaUrl(options.icon)) rawThumb = options.icon;
            }
            if (!cleanTitle || !cleanBody || !rawThumb) {
              const fallback = getLatestDomPreview();
              if (!cleanTitle && fallback.latestChatTitle) cleanTitle = fallback.latestChatTitle;
              if (!cleanBody && fallback.latestMessageText) cleanBody = fallback.latestMessageText;
              if (!rawThumb && fallback.latestThumbnailUrl) {
                rawThumb = fallback.latestThumbnailUrl;
                thumbEl = fallback.latestThumbnailEl || null;
              }
            }
            resolveThumbnailUrlAsync(rawThumb, thumbEl, function(resolvedThumb) {
              try {
                if (window.chrome && window.chrome.webview && typeof window.chrome.webview.postMessage === 'function') {
                  window.chrome.webview.postMessage({
                    channel: 'NotificationChannel',
                    type: 'NOTIFICATION_RECEIVED',
                    id: id,
                    title: cleanTitle,
                    body: cleanBody,
                    icon: options ? (options.icon || '') : '',
                    thumbnailUrl: resolvedThumb || '',
                    bridgeToken: __bridgeToken
                  });
                }
              } catch(e) {}
            });
          } catch(e) {}
          return Promise.resolve();
        };
      } catch(e) {}
    }

    // 2. Intercept window.Notification while preserving prototype chain and static members
    const OrigNotification = window.Notification;
    if (OrigNotification) {
      function CustomNotification(title, options) {
        var self = this;
        this.title = title;
        this.options = options || {};
        this.id = Math.random().toString(36).substring(2, 9);
        activeCustomNotifications[this.id] = this;
        this._listeners = {};

        try {
          let cleanTitle = normalizePreviewText(this.title);
          let cleanBody = normalizePreviewText(this.options.body || '');
          let rawThumb = '';
          let thumbEl = null;
          if (this.options.image && !isIgnoredMediaUrl(this.options.image)) rawThumb = this.options.image;
          else if (this.options.icon && !isIgnoredMediaUrl(this.options.icon)) rawThumb = this.options.icon;
          if (!cleanTitle || !cleanBody || !rawThumb) {
            const fallback = getLatestDomPreview();
            if (!cleanTitle && fallback.latestChatTitle) cleanTitle = fallback.latestChatTitle;
            if (!cleanBody && fallback.latestMessageText) cleanBody = fallback.latestMessageText;
            if (!rawThumb && fallback.latestThumbnailUrl) {
              rawThumb = fallback.latestThumbnailUrl;
              thumbEl = fallback.latestThumbnailEl || null;
            }
          }
          resolveThumbnailUrlAsync(rawThumb, thumbEl, function(resolvedThumb) {
            try {
              if (window.chrome && window.chrome.webview && typeof window.chrome.webview.postMessage === 'function') {
                window.chrome.webview.postMessage({
                  channel: 'NotificationChannel',
                  type: 'NOTIFICATION_RECEIVED',
                  id: self.id,
                  title: cleanTitle,
                  body: cleanBody,
                  icon: self.options.icon || '',
                  thumbnailUrl: resolvedThumb || '',
                  bridgeToken: __bridgeToken
                });
              }
            } catch(e) {}
          });
        } catch(e) {}

        this.close = function() {
          if (activeCustomNotifications[self.id]) {
            delete activeCustomNotifications[self.id];
            try {
              if (window.chrome && window.chrome.webview && typeof window.chrome.webview.postMessage === 'function') {
                window.chrome.webview.postMessage({
                  channel: 'NotificationChannel',
                  type: 'NOTIFICATION_CLOSED',
                  id: self.id,
                  bridgeToken: __bridgeToken
                });
              }
            } catch(e) {}
          }
        };

        this.addEventListener = function(event, callback) {
          if (!self._listeners[event]) {
            self._listeners[event] = [];
          }
          self._listeners[event].push(callback);
        };

        this.removeEventListener = function(event, callback) {
          if (self._listeners[event]) {
            const idx = self._listeners[event].indexOf(callback);
            if (idx !== -1) {
              self._listeners[event].splice(idx, 1);
            }
          }
        };
      }

      // Preserva la catena prototipale per instanceof ed ereditarietà
      try {
        CustomNotification.prototype = Object.create(OrigNotification.prototype);
        CustomNotification.prototype.constructor = CustomNotification;
        Object.setPrototypeOf(CustomNotification, OrigNotification);
      } catch(e) {}

      CustomNotification.permission = 'granted';
      CustomNotification.requestPermission = function(callback) {
        if (typeof callback === 'function') callback('granted');
        return Promise.resolve('granted');
      };

      try {
        Object.defineProperty(CustomNotification, 'permission', {
          get: function() { return 'granted'; },
          set: function() {}
        });
      } catch(e) {}

      window.Notification = CustomNotification;
    }

    // 3. Gestione permessi notifiche per WebView2
    if (navigator.permissions && typeof navigator.permissions.query === 'function') {
      try {
        const origQuery = navigator.permissions.query;
        navigator.permissions.query = function(parameters) {
          if (parameters && parameters.name === 'notifications') {
            return Promise.resolve({
              state: 'granted',
              name: 'notifications',
              onchange: null,
              addEventListener: function() {},
              removeEventListener: function() {},
              dispatchEvent: function() { return false; }
            });
          }
          return origQuery.apply(navigator.permissions, arguments);
        };
      } catch(e) {}
    }
  } catch(topEx) {
    console.warn('[NotificationOverride] init error:', topEx);
  }

  // Hook per Badging API nativa (navigator.setAppBadge / clearAppBadge usata da Telegram PWA e web moderni)
  let appBadgeCount = 0;
  try {
    if (typeof navigator !== 'undefined') {
      const origSetAppBadge = typeof navigator.setAppBadge === 'function' ? navigator.setAppBadge.bind(navigator) : null;
      navigator.setAppBadge = function(count) {
        try {
          if (count === undefined || count === null) {
            appBadgeCount = 1;
          } else {
            const num = typeof count === 'number' ? count : (parseInt(count, 10) || 0);
            appBadgeCount = Math.max(0, num);
          }
          scheduleAllChecks();
        } catch(e) {}
        if (origSetAppBadge) return origSetAppBadge(count);
        return Promise.resolve();
      };

      const origClearAppBadge = typeof navigator.clearAppBadge === 'function' ? navigator.clearAppBadge.bind(navigator) : null;
      navigator.clearAppBadge = function() {
        try {
          appBadgeCount = 0;
          scheduleAllChecks();
        } catch(e) {}
        if (origClearAppBadge) return origClearAppBadge();
        return Promise.resolve();
      };
    }
  } catch(e) {}

  // Monitoraggio unread count combinato (Title Observer + Title Setter Interceptor + DOM Badge Scanner + Badging API)
  let lastReportedUnreadCount = -1;
  let lastHeartbeatTime = 0;
  let updateDebounceTimer = null;

  function scanTelegramUnreadCount() {
    let count = 0;
    const debugItems = [];
    let latestChatTitle = '';
    let latestMessageText = '';
    let latestThumbnailUrl = '';
    let latestThumbnailEl = null;
    try {
      // 1. Badge chat Telegram Web (Web A, Web K, Web Z)
      const tgChatSelectors = [
        '.ChatBadge.unread',
        '.ChatBadge .Badge',
        '.Badge.unread',
        '.dialog-subtitle-badge',
        '.rp .badge',
        '.rp .unread',
        '.chatlist-chat .badge',
        '.chat-badge',
        '.unread-count',
        '[class*="unread-count"]',
        '[class*="unread_count"]',
        '.ListItem .Badge'
      ];
      const tgElements = document.querySelectorAll(tgChatSelectors.join(', '));
      if (tgElements && tgElements.length > 0) {
        const seen = new Set();
        tgElements.forEach(el => {
          // Ignora icone di fissaggio in alto (pin 📌) o elementi decorativi
          if (el.querySelector('.icon-pin, [class*="pin"], .icon-reaction') || el.classList.contains('icon-pin')) return;

          const container = el.closest('.ListItem, .rp, .chatlist-chat, [data-peer-id], li') || el;
          if (seen.has(container)) return;
          seen.add(container);

          const rawText = (el.textContent || '').trim();
          const digits = rawText.replace(/[^\d]/g, '');
          const isExplicitUnread = el.classList.contains('unread') || el.classList.contains('Badge') || el.classList.contains('dialog-subtitle-badge') || (el.getAttribute('class') || '').includes('unread');

          // Extract chat title and message preview without trailing badge digits
          let chatTitle = '';
          let messagePreview = '';
          let thumb = { url: '', imgEl: null };
          try {
            const titleEl = container.querySelector('.peer-title, .title, .dialog-title, .user-title, h3, .name, [class*="title"]');
            if (titleEl) chatTitle = extractTextWithoutBadges(titleEl);
            const prevEl = container.querySelector('.dialog-subtitle-message, .last-message, .dialog-subtitle, .subtitle, [class*="subtitle"], [class*="message"]');
            if (prevEl) {
              const candidate = extractTextWithoutBadges(prevEl);
              if (candidate && candidate !== chatTitle) {
                messagePreview = candidate;
              }
              thumb = extractThumbnailFromContainer(prevEl);
            }
            if (!thumb.url) {
              const mediaWrap = container.querySelector('.dialog-subtitle-media, .dialog-subtitle, .last-message');
              if (mediaWrap) thumb = extractThumbnailFromContainer(mediaWrap);
            }
          } catch(e) {}

          if (!latestChatTitle && chatTitle) {
            latestChatTitle = chatTitle;
            latestMessageText = messagePreview;
            latestThumbnailUrl = thumb.url;
            latestThumbnailEl = thumb.imgEl;
          }

          if (digits) {
            const n = parseInt(digits, 10);
            if (!isNaN(n) && n > 0) {
              count += n;
              debugItems.push({
                chatTitle: chatTitle || 'Chat',
                count: n,
                rawText: rawText,
                classes: el.className,
                html: el.outerHTML.substring(0, 150)
              });
            }
          } else if (isExplicitUnread && (el.classList.contains('unread') || rawText === '•' || rawText === '*')) {
            // Solo se è un contrassegno unread esplicito senza numero (es. pallino non letto)
            count += 1;
            debugItems.push({
              chatTitle: chatTitle || 'Chat',
              count: 1,
              rawText: rawText,
              classes: el.className,
              html: el.outerHTML.substring(0, 150)
            });
          }
        });
      }

      // 2. Se non ci sono badge nelle chat visibili (chat list virtualizzata o chiusa), fallback sui tab cartella (solo se contengono numeri > 0)
      if (count === 0) {
        const tgFolderSelectors = [
          '.folders-tabs .Tab .Badge',
          '.folders-tabs .Badge',
          '.tabs-tab .badge',
          '.tabs-tab .dialog-subtitle-badge',
          '.sidebar-header .Badge',
          '.sidebar-header .badge'
        ];
        const tgFolders = document.querySelectorAll(tgFolderSelectors.join(', '));
        if (tgFolders && tgFolders.length > 0) {
          const firstFolder = tgFolders[0];
          const rawText = (firstFolder.textContent || '').trim();
          const digits = rawText.replace(/[^\d]/g, '');
          if (digits) {
            const n = parseInt(digits, 10);
            if (!isNaN(n) && n > 0) {
              count = n;
              debugItems.push({
                chatTitle: 'Tab Cartella Principale',
                count: n,
                rawText: rawText,
                classes: firstFolder.className,
                html: firstFolder.outerHTML.substring(0, 150)
              });
            }
          }
        }
      }

      // 3. Controllo favicon per indicatore non letti di Telegram (solo se presente icona specifica unread)
      if (count === 0) {
        const iconEl = document.querySelector('link[rel*="icon"]');
        if (iconEl && iconEl.href) {
          const href = iconEl.href.toLowerCase();
          if (href.includes('unread') || href.includes('badge')) {
            count = 1;
            debugItems.push({
              chatTitle: 'Favicon Unread Indicator',
              count: 1,
              rawText: iconEl.href,
              classes: 'link-icon',
              html: iconEl.outerHTML.substring(0, 150)
            });
          }
        }
      }
    } catch(e) {}
    return { count: count, items: debugItems, latestChatTitle: latestChatTitle, latestMessageText: latestMessageText, latestThumbnailUrl: latestThumbnailUrl, latestThumbnailEl: latestThumbnailEl };
  }

  function scanWhatsAppUnread() {
    let count = 0;
    let latestChatTitle = '';
    let latestMessageText = '';
    let latestThumbnailUrl = '';
    let latestThumbnailEl = null;
    try {
      const waSelectors = [
        '[data-testid="unread-count"]', '[data-testid="icon-unread-count"]',
        'span[data-icon="unread-count"]', 'span[aria-label*="unread" i]',
        'span[aria-label*="non lett" i]', 'span[aria-label*="ungelesen" i]',
        'span[aria-label*="no leí" i]', 'span[aria-label*="non lu" i]',
        'span[aria-label*="não lida" i]', 'div[aria-label*="unread" i]',
        'div[aria-label*="non lett" i]'
      ];
      const waElements = document.querySelectorAll(waSelectors.join(', '));
      if (waElements && waElements.length > 0) {
        const seenWa = new Set();
        waElements.forEach(el => {
          if (seenWa.has(el)) return;
          seenWa.add(el);
          const txt = (el.textContent || '').trim().replace(/[^\d]/g, '');
          const aria = (el.getAttribute('aria-label') || '').trim();
          let num = 0;
          if (txt) {
            num = parseInt(txt, 10);
          } else if (aria) {
            const m = aria.match(/(\d+)\s*(?:non lett|unread|messagg)/i) || aria.match(/(\d+)/);
            if (m) num = parseInt(m[1], 10);
          }
          if (!isNaN(num) && num > 0) {
            count += num;
          } else {
            count += 1;
          }

          if (!latestChatTitle) {
            const container = el.closest('[data-testid="cell-frame-container"], [role="row"], [role="listitem"], div._ak72, div._ak7l, div[tabindex="-1"]') || (el.parentElement ? el.parentElement.parentElement : null);
            if (container) {
              const titleEl = container.querySelector('[data-testid="cell-frame-title"] span, span[title][dir="auto"], span[title], span[dir="auto"]');
              if (titleEl) {
                latestChatTitle = normalizePreviewText(titleEl.getAttribute('title') || extractElementText(titleEl) || '');
              }
              const secWrap = container.querySelector('[data-testid="cell-frame-secondary"]');
              const secEl = container.querySelector('[data-testid="cell-frame-secondary"] span[dir="ltr"], [data-testid="cell-frame-secondary"] span[dir="auto"], [data-testid="cell-frame-secondary"] span[title], [data-testid="cell-frame-secondary"] span, [data-testid="last-msg-status"] + span');
              if (secEl) {
                const secText = normalizePreviewText(secEl.getAttribute('title') || extractTextWithoutBadges(secEl) || '');
                if (secText && secText !== latestChatTitle) {
                  latestMessageText = secText;
                }
              }
              const thumb = extractThumbnailFromContainer(secWrap || secEl);
              if (thumb.url) {
                latestThumbnailUrl = thumb.url;
                latestThumbnailEl = thumb.imgEl;
              }
            }
          }
        });
      }
    } catch(e) {}
    return { count: count, latestChatTitle: latestChatTitle, latestMessageText: latestMessageText, latestThumbnailUrl: latestThumbnailUrl, latestThumbnailEl: latestThumbnailEl };
  }

  function scanDiscordUnread() {
    let count = 0;
    const debugItems = [];
    let latestChatTitle = '';
    let latestMessageText = '';
    let latestThumbnailUrl = '';
    let latestThumbnailEl = null;
    try {
      const discordMentionSelectors = [
        'div[class*="numberBadge"]',
        'span[class*="numberBadge"]',
        'div[class*="mention"]',
        'div[class*="unreadMentions"]',
        'div[class*="lowerBadge"] div[class*="badge"]',
        '[aria-label*="menzion" i]',
        '[aria-label*="mention" i]'
      ];
      const rawEls = Array.from(document.querySelectorAll(discordMentionSelectors.join(', ')));
      // Filter out containers that contain another matched badge element to avoid double-counting
      const mentionEls = rawEls.filter(el => !rawEls.some(child => child !== el && el.contains(child)));
      if (mentionEls && mentionEls.length > 0) {
        const seen = new Set();
        mentionEls.forEach(el => {
          if (seen.has(el)) return;
          seen.add(el);
          const rawText = (el.textContent || '').trim();
          const digits = rawText.replace(/[^\d]/g, '');

          if (!latestChatTitle) {
            const item = el.closest('a[data-list-item-id], [data-list-item-id], [aria-label], li');
            if (item) {
              const ariaLabel = item.getAttribute('aria-label') || '';
              const cleaned = cleanDiscordChannelLabel(ariaLabel);
              if (cleaned) {
                latestChatTitle = cleaned;
              } else {
                const nameEl = item.querySelector('[class*="name"], [class*="title"], span');
                if (nameEl) {
                  const candidate = extractTextWithoutBadges(nameEl);
                  if (candidate && !/^\d+$/.test(candidate)) latestChatTitle = candidate;
                }
              }
            }
          }

          if (digits) {
            const n = parseInt(digits, 10);
            if (!isNaN(n) && n > 0) {
              count += n;
              debugItems.push({
                chatTitle: latestChatTitle || 'Discord Mention',
                count: n,
                rawText: rawText,
                classes: el.className
              });
            }
          } else if (rawText === '•' || rawText === '*') {
            count += 1;
          }
        });
      }

      if (count === 0) {
        const unreadPills = Array.from(document.querySelectorAll('div[class*="unread__"], div[class*="unread-"], div[class^="unread_"]'))
          .filter(pill => !pill.closest('[class*="muted"], [class*="isMuted"], [class*="modeMuted"]'));
        if (unreadPills && unreadPills.length > 0) {
          count = 1;
          if (!latestChatTitle) {
            const item = unreadPills[0].closest('li, [data-list-item-id], [aria-label]');
            if (item) {
              const linkWithLabel = item.querySelector('a[aria-label], [aria-label]') || item;
              const cleaned = cleanDiscordChannelLabel(linkWithLabel.getAttribute('aria-label') || '');
              if (cleaned) {
                latestChatTitle = cleaned;
              } else {
                const nameEl = item.querySelector('[class*="name"], [class*="channelName"]');
                if (nameEl) latestChatTitle = extractTextWithoutBadges(nameEl);
              }
            }
          }
          debugItems.push({
            chatTitle: latestChatTitle || 'Discord Unread Pill',
            count: 1,
            classes: unreadPills[0].className
          });
        }
      }

      // Extract latest visible message preview and media thumbnail when a Discord channel/DM is open
      if (count > 0) {
        const msgNodes = document.querySelectorAll('li[id^="chat-messages-"] [id^="message-content-"], [class*="messageContent"]');
        if (msgNodes && msgNodes.length > 0) {
          const lastNode = msgNodes[msgNodes.length - 1];
          if (!latestMessageText) {
            const lastMsg = extractTextWithoutBadges(lastNode);
            if (lastMsg) latestMessageText = lastMsg;
          }
          const msgScope = (typeof lastNode.closest === 'function' ? lastNode.closest('li[id^="chat-messages-"], [class*="message__"], [class*="message-"]') : null) || lastNode;
          const thumb = extractThumbnailFromContainer(msgScope);
          if (thumb.url) {
            latestThumbnailUrl = thumb.url;
            latestThumbnailEl = thumb.imgEl;
          }
        }
      }
    } catch(e) {}
    return { count: count, items: debugItems, latestChatTitle: latestChatTitle, latestMessageText: latestMessageText, latestThumbnailUrl: latestThumbnailUrl, latestThumbnailEl: latestThumbnailEl };
  }

  function checkAndNotifyUnreadCount() {
    const host = (location.hostname || '').toLowerCase();
    const isTelegram = host === 'web.telegram.org' || host === 'telegram.org' || host.endsWith('.telegram.org');
    const isDiscord = host === 'discord.com' || host.endsWith('.discord.com');
    const isWhatsApp = host === 'web.whatsapp.com' || host === 'whatsapp.com' || host.endsWith('.whatsapp.com');
    const title = document.title || '';
    const titleMatch = title.match(/[\(\[](\d+)\+?[\)\]]/);
    const hasBullet = /[\(•\*\)]/.test(title) && (title.includes('•') || title.includes('*') || /\(\s*\)/.test(title));
    const titleCount = titleMatch ? parseInt(titleMatch[1], 10) : (hasBullet ? 1 : 0);

    let domCount = 0;
    let debugItems = [];
    let latestChatTitle = '';
    let latestMessageText = '';
    let latestThumbnailUrl = '';
    let latestThumbnailEl = null;
    if (isTelegram) {
      const tgRes = scanTelegramUnreadCount();
      domCount = tgRes.count;
      debugItems = tgRes.items;
      latestChatTitle = tgRes.latestChatTitle || '';
      latestMessageText = tgRes.latestMessageText || '';
      latestThumbnailUrl = tgRes.latestThumbnailUrl || '';
      latestThumbnailEl = tgRes.latestThumbnailEl || null;
    } else if (isDiscord) {
      const dcRes = scanDiscordUnread();
      domCount = dcRes.count;
      debugItems = dcRes.items;
      latestChatTitle = dcRes.latestChatTitle || '';
      latestMessageText = dcRes.latestMessageText || '';
      latestThumbnailUrl = dcRes.latestThumbnailUrl || '';
      latestThumbnailEl = dcRes.latestThumbnailEl || null;
    } else if (isWhatsApp) {
      const waRes = scanWhatsAppUnread();
      domCount = waRes.count;
      latestChatTitle = waRes.latestChatTitle || '';
      latestMessageText = waRes.latestMessageText || '';
      latestThumbnailUrl = waRes.latestThumbnailUrl || '';
      latestThumbnailEl = waRes.latestThumbnailEl || null;
    }

    if (!latestChatTitle && titleMatch) {
      let cleanedTitle = title.replace(/[\(\[]\d+\+?[\)\]]/, '').trim();
      cleanedTitle = cleanedTitle.replace(/\s*[\-\|]\s*Discord$/i, '').trim();
      if (cleanedTitle && cleanedTitle.toLowerCase() !== 'whatsapp' && cleanedTitle.toLowerCase() !== 'telegram' && cleanedTitle.toLowerCase() !== 'discord') {
        latestChatTitle = cleanedTitle;
      }
    }

    // Preferisci il massimo tra il conteggio estratto dal titolo, i badge del DOM e la Badging API
    const effectiveCount = Math.max(titleCount, domCount, appBadgeCount);
    const now = Date.now();

    if (effectiveCount !== lastReportedUnreadCount || (effectiveCount > 0 && (now - lastHeartbeatTime > 3000))) {
      const isNewMessage = (lastReportedUnreadCount >= 0 && effectiveCount > lastReportedUnreadCount);
      lastReportedUnreadCount = effectiveCount;
      lastHeartbeatTime = now;
      resolveThumbnailUrlAsync(latestThumbnailUrl, latestThumbnailEl, function(resolvedThumb) {
        try {
          if (window.chrome && window.chrome.webview && typeof window.chrome.webview.postMessage === 'function') {
            window.chrome.webview.postMessage({
              channel: 'NotificationChannel',
              type: 'UNREAD_COUNT_CHANGED',
              id: 'unread_' + now,
              unreadCount: effectiveCount,
              title: title,
              domCount: domCount,
              appBadgeCount: appBadgeCount,
              titleCount: titleCount,
              latestChatTitle: latestChatTitle,
              latestMessageText: latestMessageText,
              latestThumbnailUrl: resolvedThumb || '',
              isNewMessage: isNewMessage,
              debugItems: debugItems,
              bridgeToken: __bridgeToken
            });
          }
        } catch(e) {}
      });
    }
  }

  // Monitoraggio stato "Online" / "In linea" / "Sta scrivendo..." del contatto attivo (TODO #42)
  let lastReportedOnline = null;
  let lastReportedStatusText = '';

  function scanOnlineStatus() {
    try {
      // 1. WhatsApp Web: #main header o [data-testid="conversation-header"]
      const waHeader = document.querySelector('#main header, [data-testid="conversation-header"]');
      if (waHeader) {
        const subSpans = waHeader.querySelectorAll('span[title], span[dir="auto"], span');
        for (let i = 0; i < subSpans.length; i++) {
          const span = subSpans[i];
          const text = (span.textContent || '').trim().toLowerCase();
          const title = (span.getAttribute('title') || '').trim().toLowerCase();
          
          if (text === 'online' || text === 'in linea' || text === 'en línea' || text === 'en ligne' || text === 'conectado' ||
              title === 'online' || title === 'in linea' || title === 'en línea' || title === 'en ligne' || title === 'conectado') {
            return { isOnline: true, statusText: span.textContent.trim() || 'in linea' };
          }
          if (text.includes('sta scrivendo') || text.includes('typing') || text.includes('escribiendo') || text.includes('scrive...') || text.includes('registra audio') || text.includes('recording audio')) {
            return { isOnline: true, statusText: span.textContent.trim() || 'sta scrivendo...' };
          }
        }
      }

      // 2. Telegram Web K / Z / A
      const tgStatus = document.querySelector('.chat-info .person-status, .chat-info .status, .topbar .status, .chat-subtitle, .chat-info-status');
      if (tgStatus) {
        const text = (tgStatus.textContent || '').trim().toLowerCase();
        if (text === 'online' || text === 'in linea' || text === 'en ligne' || text === 'en línea' || tgStatus.classList.contains('online')) {
          return { isOnline: true, statusText: tgStatus.textContent.trim() || 'online' };
        }
        if (text.includes('typing') || text.includes('sta scrivendo') || text.includes('scrive') || tgStatus.classList.contains('typing')) {
          return { isOnline: true, statusText: tgStatus.textContent.trim() || 'typing...' };
        }
      }
    } catch(e) {}
    return { isOnline: false, statusText: '' };
  }

  function checkAndNotifyOnlineStatus() {
    const res = scanOnlineStatus();
    if (res.isOnline !== lastReportedOnline || (res.isOnline && res.statusText !== lastReportedStatusText)) {
      lastReportedOnline = res.isOnline;
      lastReportedStatusText = res.statusText;
      try {
        if (window.chrome && window.chrome.webview && typeof window.chrome.webview.postMessage === 'function') {
          window.chrome.webview.postMessage({
            channel: 'NotificationChannel',
            type: 'ONLINE_STATUS_CHANGED',
            id: 'online_' + Date.now(),
            isOnline: res.isOnline,
            statusText: res.statusText,
            bridgeToken: __bridgeToken
          });
        }
      } catch(e) {}
    }
  }

  let isThrottled = false;
  function scheduleAllChecks() {
    if (updateDebounceTimer) clearTimeout(updateDebounceTimer);
    // Se la pagina è nascosta (minimizzata o in background), aumenta il debounce a 2500ms
    const delay = (typeof document !== 'undefined' && document.hidden) ? 2500 : 350;
    updateDebounceTimer = setTimeout(function() {
      scanErrorPopups();
      checkAndNotifyUnreadCount();
      // Scansiona lo stato online del contatto solo se la pagina è visibile in primo piano
      if (typeof document !== 'undefined' && !document.hidden) {
        checkAndNotifyOnlineStatus();
      }
    }, delay);
  }

  function initMonitoring() {
    // Intercetta la modifica diretta a document.title (WhatsApp Web e Telegram SPA)
    try {
      const titleDesc = Object.getOwnPropertyDescriptor(Document.prototype, 'title') ||
                        Object.getOwnPropertyDescriptor(HTMLDocument.prototype, 'title');
      if (titleDesc && titleDesc.set) {
        const origTitleSet = titleDesc.set;
        Object.defineProperty(document, 'title', {
          get: function() {
            return titleDesc.get ? titleDesc.get.call(this) : '';
          },
          set: function(val) {
            origTitleSet.call(this, val);
            scheduleAllChecks();
          },
          configurable: true
        });
      }
    } catch(e) {}

    const titleEl = document.querySelector('title');
    if (titleEl) {
      const titleObserver = new MutationObserver(scheduleAllChecks);
      titleObserver.observe(titleEl, { subtree: true, characterData: true, childList: true });
    }

    if (document.head) {
      const headObserver = new MutationObserver(scheduleAllChecks);
      headObserver.observe(document.head, { subtree: true, characterData: true, childList: true });
    }

    // Osservatore DOM mirato: aggancia esclusivamente il container chat (#pane-side per WhatsApp)
    // una volta presente, evitando categoricamente l'osservazione ricorsiva su tutto document.body
    let chatPanelObserverAttached = false;
    function tryAttachChatPanelObserver() {
      if (chatPanelObserverAttached) return;
      const chatPanel = document.querySelector('#pane-side, [data-testid="chat-list"], .chatlist, [data-list-id="channels"], [data-list-id="guildsnav"]');
      if (chatPanel) {
        const chatObserver = new MutationObserver(scheduleAllChecks);
        chatObserver.observe(chatPanel, { childList: true, subtree: true, attributes: true, attributeFilter: ['aria-label', 'class'] });
        chatPanelObserverAttached = true;
      }
    }
    tryAttachChatPanelObserver();

    // Polling periodico adattivo: 3.5s in primo piano, 25s in background per azzerare il carico notturno
    let pollInterval = 3500;
    function runAdaptivePoll() {
      const isHidden = (typeof document !== 'undefined' && document.hidden);
      tryAttachChatPanelObserver();
      scanErrorPopups();
      checkAndNotifyUnreadCount();
      if (!isHidden) {
        checkAndNotifyOnlineStatus();
      }
      pollInterval = isHidden ? 25000 : 3500;
      setTimeout(runAdaptivePoll, pollInterval);
    }
    setTimeout(runAdaptivePoll, pollInterval);

    // Ripresa immediata al cambio di visibilità della finestra (quando l'utente sblocca il PC o apre la finestra)
    if (typeof document !== 'undefined' && typeof document.addEventListener === 'function') {
      document.addEventListener('visibilitychange', function() {
        sendDiagnostic('VISIBILITY_CHANGE', { hidden: document.hidden });
        if (!document.hidden) {
          scheduleAllChecks();
        }
      });
    }

    checkAndNotifyUnreadCount();
    checkAndNotifyOnlineStatus();
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initMonitoring);
  } else {
    initMonitoring();
  }

  window.onNotificationClicked = function(id) {
    const notification = activeCustomNotifications[id];
    if (notification) {
      if (typeof notification.onclick === 'function') {
        notification.onclick();
      }
      const listeners = notification._listeners['click'] || [];
      listeners.forEach(cb => {
        try { cb(); } catch(e) {}
      });
    }
  };

  window.onNotificationClosedFromServer = function(id) {
    const notification = activeCustomNotifications[id];
    if (notification) {
      if (typeof notification.onclose === 'function') {
        notification.onclose();
      }
      const listeners = notification._listeners['close'] || [];
      listeners.forEach(cb => {
        try { cb(); } catch(e) {}
      });
      delete activeCustomNotifications[id];
    }
  };
})();
