(function() {
  const __bridgeToken = $$BRIDGE_TOKEN$$;
  if (window.__notificationOverrideInstalled) return;
  window.__notificationOverrideInstalled = true;

  const activeCustomNotifications = {};

  // Override ServiceWorkerRegistration.showNotification poiché WhatsApp lo utilizza per le notifiche push
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
          bridgeToken: __bridgeToken
        });
      } catch(e) {
        console.error('Failed to postMessage:', e);
      }
      
      // Non chiamiamo l'originale per evitare la notifica nativa del browser Edge/WebView2
      return Promise.resolve();
    };
  }

  // Override del costruttore window.Notification nativo come fallback
  function CustomNotification(title, options) {
    console.log('[NotificationOverride] CustomNotification triggered:', title, options);
    var self = this;
    this.title = title;
    this.options = options || {};
    this.id = Math.random().toString(36).substring(2, 9);
    activeCustomNotifications[this.id] = this;

    this._listeners = {};

    try {
      window.chrome.webview.postMessage({
        channel: 'NotificationChannel',
        type: 'NOTIFICATION_RECEIVED',
        id: this.id,
        title: this.title,
        body: this.options.body || '',
        icon: this.options.icon || '',
        bridgeToken: __bridgeToken
      });
    } catch(e) {}

    this.close = function() {
      if (activeCustomNotifications[self.id]) {
        delete activeCustomNotifications[self.id];
        try {
          window.chrome.webview.postMessage({
            channel: 'NotificationChannel',
            type: 'NOTIFICATION_CLOSED',
            id: self.id,
            bridgeToken: __bridgeToken
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

  CustomNotification.permission = 'granted';
  CustomNotification.requestPermission = function(callback) {
    if (typeof callback === 'function') callback('granted');
    return Promise.resolve('granted');
  };
  
  CustomNotification.toString = function() {
    return "function Notification() { [native code] }";
  };

  try {
    Object.defineProperty(CustomNotification, 'permission', {
      get: function() { return 'granted'; },
      set: function() {}
    });
  } catch(e) {}

  window.Notification = CustomNotification;

  if (navigator.permissions && navigator.permissions.query) {
    const origQuery = navigator.permissions.query.bind(navigator.permissions);
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
      return origQuery(parameters);
    };
  }

  // Monitoraggio titolo pagina per rilevamento badge/conteggio non letti
  let lastUnreadCount = 0;
  function initTitleObserver() {
    const titleEl = document.querySelector('title');
    if (!titleEl) return;
    const titleObserver = new MutationObserver(function() {
      const title = document.title || '';
      const match = title.match(/^\((\d+)\)/);
      const count = match ? parseInt(match[1], 10) : 0;
      if (count !== lastUnreadCount) {
        lastUnreadCount = count;
        try {
          window.chrome.webview.postMessage({
            channel: 'NotificationChannel',
            type: 'UNREAD_COUNT_CHANGED',
            unreadCount: count,
            title: title,
            bridgeToken: __bridgeToken
          });
        } catch(e) {}
      }
    });
    titleObserver.observe(titleEl, { subtree: true, characterData: true, childList: true });
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initTitleObserver);
  } else {
    initTitleObserver();
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
