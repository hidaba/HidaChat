Public Class ThemeJsScripts
    Public Shared ReadOnly Property RoundedCorners As String =
        <css><![CDATA[
        #app {
            border-radius: 15px !important;
        }
        ]]></css>.Value

    Public Shared ReadOnly Property RemoveDownloadForWindows As String =
        <css><![CDATA[
        section[data-testid="intro-panel"] > :first-child {
            display: none !important;
        }
        div[data-tab="4"]:has(span[data-icon="wa-square-icon"]) {
            display: none !important;
        }
        ]]></css>.Value

    Public Shared ReadOnly Property LightModeJS As String
        Get
            Return <js><![CDATA[
            var style = document.createElement("style");
            style.innerHTML =
            ]]></js>.Value & " `" & RoundedCorners & " " & RemoveDownloadForWindows & <css><![CDATA[
            body{
              background:#fff !important;
            }

            ._ap4q::after {
              background-color: #fff !important;
            }
            `;
            var ref = document.querySelector("script");
            ref.parentNode.insertBefore(style, ref);
            document.getElementsByTagName("body")[0].classList = [""];
            ]]></css>.Value
        End Get
    End Property

    Public Shared ReadOnly Property DarkModeJS As String
        Get
            Return <js><![CDATA[
            var style = document.createElement("style");
            style.innerHTML =
            ]]></js>.Value & " `" & RoundedCorners & " " & RemoveDownloadForWindows & <css><![CDATA[
            body{
              background:#000 !important;
            }

            ._ap4q::after {
              background-color: #000 !important;
            }
            `;
            var ref = document.querySelector("script");
            ref.parentNode.insertBefore(style, ref);
            document.getElementsByTagName("body")[0].classList = ["dark"];
            ]]></css>.Value
        End Get
    End Property
End Class

Public Class NotificationJsScripts
    Public Shared ReadOnly Property NotificationOverrideJS As String =
        <js><![CDATA[
(function() {
  if (window.__notificationOverrideInstalled) return;
  window.__notificationOverrideInstalled = true;

  window.activeCustomNotifications = {};

  // Override ServiceWorkerRegistration.showNotification since WhatsApp uses it
  if (window.ServiceWorkerRegistration && window.ServiceWorkerRegistration.prototype) {
    const originalShowNotification = window.ServiceWorkerRegistration.prototype.showNotification;
    window.ServiceWorkerRegistration.prototype.showNotification = function(title, options) {
      console.log('[NotificationOverride] ServiceWorker showNotification intercepted:', title, options);
      
      const id = Math.random().toString(36).substring(2, 9);
      
      try {
        window.chrome.webview.postMessage({
          channel: 'NotificationChannel',
          type: 'NOTIFICATION_RECEIVED',
          id: id,
          title: title,
          body: options ? (options.body || '') : '',
          icon: options ? (options.icon || '') : '',
          bridgeToken: window.__bridgeToken || ''
        });
      } catch(e) {
        console.error('Failed to postMessage:', e);
      }
      
      // We don't call the original because we want our WPF popup to handle it, not the browser's native toast
      return Promise.resolve();
    };
  }

  // Also override window.Notification as a fallback
  function CustomNotification(title, options) {
    console.log('[NotificationOverride] CustomNotification triggered:', title, options);
    var self = this;
    this.title = title;
    this.options = options || {};
    this.id = Math.random().toString(36).substring(2, 9);
    window.activeCustomNotifications[this.id] = this;

    this._listeners = {};

    try {
      window.chrome.webview.postMessage({
        channel: 'NotificationChannel',
        type: 'NOTIFICATION_RECEIVED',
        id: this.id,
        title: this.title,
        body: this.options.body || '',
        icon: this.options.icon || '',
        bridgeToken: window.__bridgeToken || ''
      });
    } catch(e) {}

    this.close = function() {
      if (window.activeCustomNotifications[self.id]) {
        delete window.activeCustomNotifications[self.id];
        try {
          window.chrome.webview.postMessage({
            channel: 'NotificationChannel',
            type: 'NOTIFICATION_CLOSED',
            id: self.id,
            bridgeToken: window.__bridgeToken || ''
          });
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

  // Inherit static properties and requestPermission
  CustomNotification.permission = 'granted';
  CustomNotification.requestPermission = function(callback) {
    if (typeof callback === 'function') callback('granted');
    return Promise.resolve('granted');
  };
  
  // Trick WhatsApp into thinking this is native
  CustomNotification.toString = function() {
    return "function Notification() { [native code] }";
  };

  window.Notification = CustomNotification;

  window.onNotificationClicked = function(id) {
    const notification = window.activeCustomNotifications[id];
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
    const notification = window.activeCustomNotifications[id];
    if (notification) {
      if (typeof notification.onclose === 'function') {
        notification.onclose();
      }
      const listeners = notification._listeners['close'] || [];
      listeners.forEach(cb => {
        try { cb(); } catch(e) {}
      });
      delete window.activeCustomNotifications[id];
    }
  };
})();
        ]]></js>.Value
End Class

Public Class TranslationJsScripts
    Private Shared ReadOnly Property TranslationStyles As String =
        <css><![CDATA[
  const style = document.createElement('style');
  style.innerHTML = `
    .custom-translate-hover-btn {
      position: absolute;
      top: 6px;
      width: 28px;
      height: 28px;
      background-color: transparent;
      border-radius: 50%;
      display: none;
      align-items: center;
      justify-content: center;
      cursor: pointer;
      z-index: 999;
      box-shadow: none;
      color: #8696a0;
      font-size: 12px;
      user-select: none;
      transition: background-color 0.2s;
    }
    .custom-translate-hover-btn:hover {
      background-color: rgba(0,0,0,0.1);
    }
    body.disable-hover-translation .custom-translate-hover-btn {
      display: none !important;
    }
    [data-testid^="conv-msg"] {
      position: relative !important;
    }
    [data-testid^="conv-msg"]:hover .custom-translate-hover-btn {
      display: flex !important;
    }
    @keyframes translatePulse {
      0% { opacity: 0.4; }
      50% { opacity: 1.0; }
      100% { opacity: 0.4; }
    }
    .translation-body-text.loading {
      animation: translatePulse 1.5s infinite ease-in-out;
    }
  `;
  document.head.appendChild(style);
        ]]></css>.Value

    Private Shared ReadOnly Property TranslationHoverButton As String =
        <js><![CDATA[
  document.addEventListener('mouseover', function(e) {
    if (window.__enableHoverTranslation === false) return;
    const bubble = e.target.closest('[data-testid="msg-container"]');
    if (bubble && !bubble.querySelector('.custom-translate-hover-btn') && !bubble.closest('.custom-translation-bubble')) {
      const btn = document.createElement('div');
      btn.className = 'custom-translate-hover-btn';
      btn.innerHTML = `<svg viewBox="0 0 24 24" width="20" height="20" fill="currentColor"><path d="M0 0h24v24H0V0z" fill="none"/><path d="M11.99 2C6.47 2 2 6.48 2 12s4.47 10 9.99 10C17.52 22 22 17.52 22 12S17.52 2 11.99 2zm6.93 6h-2.95c-.32-1.25-.78-2.45-1.38-3.56 1.84.63 3.37 1.91 4.33 3.56zM12 4.04c.83 1.2 1.48 2.53 1.91 3.96h-3.82c.43-1.43 1.08-2.76 1.91-3.96zM4.26 14C4.1 13.36 4 12.69 4 12s.1-1.36.26-2h3.38c-.08.66-.14 1.34-.14 2 0 .66.06 1.34.14 2H4.26zm.82 2h2.95c.32 1.25.78 2.45 1.38 3.56-1.84-.63-3.37-1.91-4.33-3.56zm2.95-8H5.08c.96-1.65 2.49-2.93 4.33-3.56C8.81 5.55 8.35 6.75 8.03 8zM12 19.96c-.83-1.2-1.48-2.53-1.91-3.96h3.82c-.43 1.43-1.08 2.76-1.91 3.96zM14.34 14H9.66c-.09-.66-.16-1.34-.16-2 0-.66.07-1.34.16-2h4.68c.09.66.16 1.34.16 2 0 .66-.07 1.34-.16 2zm.25 5.56c.6-1.11 1.06-2.31 1.38-3.56h2.95c-.96 1.65-2.49 2.93-4.33 3.56zM16.36 14c.08-.66.14-1.34.14-2 0-.66-.06-1.34-.14-2h3.38c.16.64.26 1.31.26 2s-.1 1.36-.26 2h-3.38z"/></svg>`;
      btn.title = window.__translationTooltipLabel || (window.__translationTargetLangName || 'App Language');
      
      const isOutgoing = bubble.firstElementChild && bubble.firstElementChild.getAttribute('data-testid') === 'tail-out';
      if (isOutgoing) {
        btn.style.left = '-65px';
        btn.style.right = 'auto';
      } else {
        btn.style.right = '-65px';
        btn.style.left = 'auto';
      }
      
      btn.addEventListener('click', function(evt) {
        evt.stopPropagation();
        evt.preventDefault();
        
        let textToTranslate = getMessageText(bubble);
        if (textToTranslate) {
          textToTranslate = textToTranslate.trim();
          textToTranslate = textToTranslate.replace(/\s*\d{1,2}:\d{2}\s*(?:AM|PM|am|pm)?\s*$/g, '');
          if (textToTranslate) {
            performTranslation(textToTranslate, bubble);
          }
        }
      });
      bubble.appendChild(btn);
    }
  });
        ]]></js>.Value

    Private Shared ReadOnly Property TranslationBubbleUI As String =
        <js><![CDATA[
  function performTranslation(text, container) {
    const existing = container.querySelector('.custom-translation-bubble');
    if (existing) existing.remove();

    const quotedNode = container.querySelector('[data-testid="quoted-message"] .selectable-text, .quoted-message .selectable-text');
    const quotedText = quotedNode ? quotedNode.innerText.trim() : null;

    const transId = 'trans_' + Math.random().toString(36).substring(2, 9);

    const transBubble = document.createElement('div');
    transBubble.className = 'custom-translation-bubble';
    transBubble.setAttribute('data-translation-id', transId);
    transBubble.style.marginTop = '6px';
    transBubble.style.padding = '6px 8px';
    transBubble.style.borderRadius = '6px';
    transBubble.style.fontSize = '12.5px';
    transBubble.style.lineHeight = '1.4';
    transBubble.style.borderLeft = '3px solid #00a884';
    transBubble.style.position = 'relative';

    const isDark = document.body.classList.contains('dark');
    transBubble.style.backgroundColor = isDark ? '#1f2c34' : '#f0f2f5';
    transBubble.style.color = isDark ? '#e9edef' : '#111b21';

    const header = document.createElement('div');
    header.style.fontWeight = 'bold';
    header.style.fontSize = '11px';
    header.style.color = '#00a884';
    header.style.marginBottom = '2px';
    header.style.display = 'flex';
    header.style.justifyContent = 'space-between';
    header.style.alignItems = 'center';

    const title = document.createElement('span');
    title.innerText = 'Translation';
    header.appendChild(title);

    const closeBtn = document.createElement('span');
    closeBtn.innerText = '×';
    closeBtn.style.cursor = 'pointer';
    closeBtn.style.fontSize = '14px';
    closeBtn.style.fontWeight = 'bold';
    closeBtn.style.padding = '0 4px';
    closeBtn.addEventListener('click', (e) => {
      e.stopPropagation();
      transBubble.remove();
    });
    header.appendChild(closeBtn);

    transBubble.appendChild(header);

    const bodyText = document.createElement('div');
    bodyText.className = 'translation-body-text loading';
    bodyText.innerText = 'Translating...';
    transBubble.appendChild(bodyText);

    container.appendChild(transBubble);

    const targetLang = window.__translationTargetLangCode || 'en';
    try {
      window.chrome.webview.postMessage({
        channel: 'TranslationChannel',
        id: transId,
        text: text,
        quotedText: quotedText,
        targetLang: targetLang,
        bridgeToken: window.__bridgeToken || ''
      });
    } catch(e) {
      bodyText.innerText = 'Translation channel error';
    }
  }
        ]]></js>.Value

    Private Shared ReadOnly Property TranslateAllMessagesJS As String =
        <js><![CDATA[
  window.translateAllMessages = function() {
    const bubbles = document.querySelectorAll('[data-testid="msg-container"]');
    bubbles.forEach(bubble => {
      if (bubble.closest('.custom-translation-bubble')) return;
      
      const text = getMessageText(bubble);
      if (text) {
        let cleanText = text.trim();
        cleanText = cleanText.replace(/\s*\d{1,2}:\d{2}\s*(?:AM|PM|am|pm)?\s*$/g, '');
        if (cleanText && !bubble.querySelector('.custom-translation-bubble')) {
          performTranslation(cleanText, bubble);
        }
      }
    });
  };
        ]]></js>.Value

    Private Shared ReadOnly Property TranslationEngineJS As String =
        <js><![CDATA[
  let translatedNodes = new WeakSet();
  let isScanning = false;

  function scanAndTranslateDOM() {
    if (isScanning) return;
    isScanning = true;

    try {
      const batchNodes = [];
      const batchTexts = [];

      function walk(node) {
        if (node.nodeType === 3) {
          const text = node.nodeValue.trim();
          if (text.length > 0 && !/^\d+$/.test(text) && !/^\d{1,2}:\d{2}$/.test(text) && !translatedNodes.has(node)) {
            batchNodes.push(node);
            batchTexts.push(text);
          }
        } else if (node.nodeType === 1) {
          const tag = node.tagName.toLowerCase();
          if (tag !== 'script' && tag !== 'style' && tag !== 'noscript' && tag !== 'iframe') {
            const isWidget = node.classList && (node.classList.contains('custom-translate-hover-btn') || node.classList.contains('custom-translation-bubble'));
            if (!isWidget) {
              const placeholder = node.getAttribute('placeholder');
              const dataPlaceholder = node.getAttribute('data-placeholder');
              if ((placeholder && placeholder.trim().length > 0) || (dataPlaceholder && dataPlaceholder.trim().length > 0)) {
                if (!translatedNodes.has(node)) {
                  batchNodes.push(node);
                  batchTexts.push(((placeholder && placeholder.trim().length > 0) ? placeholder : dataPlaceholder).trim());
                }
              }
              const isEditable = node.isContentEditable || node.getAttribute('contenteditable') === 'true';
              if (!isEditable) {
                for (let child = node.firstChild; child; child = child.nextSibling) {
                  walk(child);
                }
              }
            }
          }
        }
      }

      if (document.body) {
        walk(document.body);
      }

      if (batchTexts.length === 0) {
        isScanning = false;
        return;
      }

      const chunkSize = 50;
      for (let i = 0; i < batchTexts.length; i += chunkSize) {
        const chunkNodes = batchNodes.slice(i, i + chunkSize);
        const chunkTexts = batchTexts.slice(i, i + chunkSize);
        
        const transId = 'batch_' + Math.random().toString(36).substring(2, 9);
        
        window.__batchMap = window.__batchMap || {};
        window.__batchMap[transId] = chunkNodes;

        chunkNodes.forEach(n => translatedNodes.add(n));

        try {
          window.chrome.webview.postMessage({
            channel: 'TranslationChannel',
            type: 'BATCH_TRANSLATE',
            id: transId,
            texts: chunkTexts,
            targetLang: window.__translationTargetLangCode,
            bridgeToken: window.__bridgeToken || ''
          });
        } catch (e) {
          chunkNodes.forEach(n => translatedNodes.delete(n));
        }
      }
    } catch (err) {
      console.error("DOM translation error:", err);
    }
    
    isScanning = false;
  }

  window.translatePage = function() {
    if (window.__fullPageTranslationActive) return;
    window.__fullPageTranslationActive = true;
    
    scanAndTranslateDOM();

    const observer = new MutationObserver(() => {
      scanAndTranslateDOM();
    });
    observer.observe(document.body, {
      childList: true,
      subtree: true,
      characterData: true
    });
  };
        ]]></js>.Value

    Private Shared ReadOnly Property TranslationCallbacksJS As String =
        <js><![CDATA[
  window.onBatchTranslationReceived = function(transId, translatedTexts, isSuccess) {
    const nodes = window.__batchMap ? window.__batchMap[transId] : null;
    if (nodes && isSuccess && translatedTexts.length === nodes.length) {
      nodes.forEach((node, idx) => {
        if (node && translatedTexts[idx]) {
          if (node.nodeType === 3) {
            const original = node.nodeValue;
            const leadingWs = original.match(/^\s*/)[0];
            const trailingWs = original.match(/\s*$/)[0];
            node.nodeValue = leadingWs + translatedTexts[idx] + trailingWs;
            translatedNodes.add(node);
          } else if (node.nodeType === 1) {
            if (node.hasAttribute('placeholder')) {
              node.setAttribute('placeholder', translatedTexts[idx]);
            }
            if (node.hasAttribute('data-placeholder')) {
              node.setAttribute('data-placeholder', translatedTexts[idx]);
            }
            translatedNodes.add(node);
          }
        }
      });
    }
    if (window.__batchMap) {
      delete window.__batchMap[transId];
    }
  };

  window.onTranslationReceived = function(transId, translatedText, isSuccess) {
    const bubble = document.querySelector('[data-translation-id="' + transId + '"]');
    if (bubble) {
      const bodyText = bubble.querySelector('.translation-body-text');
      if (bodyText) {
        bodyText.className = 'translation-body-text';
        if (isSuccess) {
          if (translatedText.startsWith('{')) {
            try {
              const data = JSON.parse(translatedText);
              let quotedElement = bubble.querySelector('.quoted-translation');
              if (!quotedElement) {
                quotedElement = document.createElement('div');
                quotedElement.className = 'quoted-translation';
                quotedElement.style.borderLeft = '2px solid #8696a0';
                quotedElement.style.paddingLeft = '6px';
                quotedElement.style.color = '#8696a0';
                quotedElement.style.fontSize = '11.5px';
                quotedElement.style.marginBottom = '6px';
                quotedElement.style.fontStyle = 'italic';
                bodyText.parentNode.insertBefore(quotedElement, bodyText);
              }
              quotedElement.innerText = data.quoted;
              bodyText.innerText = data.response;
            } catch(e) {
              bodyText.innerText = translatedText;
            }
          } else {
            bodyText.innerText = translatedText;
          }
        } else {
          bodyText.innerText = 'Translation failed';
        }
      }
    }
  };
        ]]></js>.Value

    Public Shared Function GetTranslationJS(
        targetLangCode As String,
        targetLangName As String,
        tooltipLabel As String,
        enableHover As Boolean,
        enableFullPage As Boolean
    ) As String
        Dim escapedTooltip = tooltipLabel.Replace("'", "\'")
        Dim escapedName = targetLangName.Replace("'", "\'")
        
        Dim hoverClassListCmd = If(enableHover, 
            "document.body.classList.remove('disable-hover-translation');", 
            "document.body.classList.add('disable-hover-translation');")
            
        Dim fullPageInitCmd = If(enableFullPage,
            "if (document.readyState === 'complete') { window.translatePage(); } else { window.addEventListener('load', () => window.translatePage()); }",
            "")

        Return <js><![CDATA[
(function() {
  if (window.__translationOverrideInstalled) {
    if (window.setTargetLanguage) {
      window.setTargetLanguage('$$LANG_CODE$$', '$$LANG_NAME$$', '$$TOOLTIP$$', $$ENABLE_HOVER$$);
    }
    return;
  }
  window.__translationOverrideInstalled = true;

  function getMessageText(bubble) {
    const nodes = bubble.querySelectorAll('.selectable-text');
    for (let node of nodes) {
      if (node.closest('[data-testid="quoted-message"]') || node.closest('.quoted-message')) {
        continue;
      }
      return node.innerText;
    }
    if (nodes.length > 0) return nodes[nodes.length - 1].innerText;
    return bubble.innerText;
  }

  window.__translationTargetLangCode = '$$LANG_CODE$$';
  window.__translationTargetLangName = '$$LANG_NAME$$';
  window.__translationTooltipLabel = '$$TOOLTIP$$';
  window.__enableHoverTranslation = $$ENABLE_HOVER$$;
  $$HOVER_CLASS_CMD$$

  window.setTargetLanguage = function(code, name, tooltipLabel, enableHover) {
    const oldCode = window.__translationTargetLangCode;
    window.__translationTargetLangCode = code;
    window.__translationTargetLangName = name;
    window.__translationTooltipLabel = tooltipLabel || name;
    window.__enableHoverTranslation = enableHover !== undefined ? enableHover : true;
    if (window.__enableHoverTranslation) {
      document.body.classList.remove('disable-hover-translation');
    } else {
      document.body.classList.add('disable-hover-translation');
    }
    const btns = document.querySelectorAll('.custom-translate-hover-btn');
    btns.forEach(btn => {
      btn.title = window.__translationTooltipLabel;
    });
    if (oldCode !== code && window.__fullPageTranslationActive) {
      translatedNodes = new WeakSet();
      scanAndTranslateDOM();
    }
  };

  $$STYLES$$

  $$HOVER_BTN$$

  $$BUBBLE_UI$$

  $$ALL_MSG_JS$$

  $$ENGINE_JS$$

  $$CALLBACKS_JS$$

  $$FULL_PAGE_INIT$$
})();
        ]]></js>.Value _
            .Replace("$$LANG_CODE$$", targetLangCode) _
            .Replace("$$LANG_NAME$$", escapedName) _
            .Replace("$$TOOLTIP$$", escapedTooltip) _
            .Replace("$$ENABLE_HOVER$$", enableHover.ToString().ToLower()) _
            .Replace("$$HOVER_CLASS_CMD$$", hoverClassListCmd) _
            .Replace("$$STYLES$$", TranslationStyles) _
            .Replace("$$HOVER_BTN$$", TranslationHoverButton) _
            .Replace("$$BUBBLE_UI$$", TranslationBubbleUI) _
            .Replace("$$ALL_MSG_JS$$", TranslateAllMessagesJS) _
            .Replace("$$ENGINE_JS$$", TranslationEngineJS) _
            .Replace("$$CALLBACKS_JS$$", TranslationCallbacksJS) _
            .Replace("$$FULL_PAGE_INIT$$", fullPageInitCmd)
    End Function
End Class
