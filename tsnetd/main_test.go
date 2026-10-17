package main

import (
	"net/http"
	"net/http/httptest"
	"net/url"
	"strings"
	"testing"
)

func TestLocalAuth(t *testing.T) {
	const secret = "super-secret-token-1234"

	// 1. Nessun token richiesto (localToken == "")
	req, _ := http.NewRequest("GET", "http://127.0.0.1:18800/", nil)
	if !checkLocalAuth(req, "") {
		t.Errorf("checkLocalAuth should allow any request when localToken is empty")
	}

	// 2. Token richiesto ma richiesta priva di credenziali
	if checkLocalAuth(req, secret) {
		t.Errorf("checkLocalAuth should reject request without credentials")
	}

	// 3. Token tramite header X-HidaChat-Local-Token
	reqHeader, _ := http.NewRequest("GET", "http://127.0.0.1:18800/", nil)
	reqHeader.Header.Set("X-HidaChat-Local-Token", secret)
	if !checkLocalAuth(reqHeader, secret) {
		t.Errorf("checkLocalAuth should accept valid X-HidaChat-Local-Token header")
	}

	// 4. Token tramite query param __htok
	reqQuery, _ := http.NewRequest("GET", "http://127.0.0.1:18800/?__htok="+secret, nil)
	if !checkLocalAuth(reqQuery, secret) {
		t.Errorf("checkLocalAuth should accept valid __htok query param")
	}

	// 5. Token tramite cookie __htok
	reqCookie, _ := http.NewRequest("GET", "http://127.0.0.1:18800/", nil)
	reqCookie.AddCookie(&http.Cookie{Name: "__htok", Value: secret})
	if !checkLocalAuth(reqCookie, secret) {
		t.Errorf("checkLocalAuth should accept valid __htok cookie")
	}

	// 6. Token errato
	reqBad, _ := http.NewRequest("GET", "http://127.0.0.1:18800/", nil)
	reqBad.Header.Set("X-HidaChat-Local-Token", "wrong-token")
	if checkLocalAuth(reqBad, secret) {
		t.Errorf("checkLocalAuth should reject invalid token")
	}
}

func TestAllowedOrigin(t *testing.T) {
	target, _ := url.Parse("http://hermes-agent.tailnet:9119")
	const proxyPort = 18800

	// 1. Origin vuoto (navigazione standard o chiamate senza Origin)
	if !isAllowedOrigin("", proxyPort, target) {
		t.Errorf("isAllowedOrigin should allow empty origin")
	}

	// 2. Origin 127.0.0.1 con porta corretta
	if !isAllowedOrigin("http://127.0.0.1:18800", proxyPort, target) {
		t.Errorf("isAllowedOrigin should allow 127.0.0.1 with matching port")
	}

	// 3. Origin localhost con porta corretta
	if !isAllowedOrigin("http://localhost:18800", proxyPort, target) {
		t.Errorf("isAllowedOrigin should allow localhost with matching port")
	}

	// 4. Origin [::1] con porta corretta
	if !isAllowedOrigin("http://[::1]:18800", proxyPort, target) {
		t.Errorf("isAllowedOrigin should allow [::1] with matching port")
	}

	// 5. Origin porta differente (es. altro servizio locale non autorizzato)
	if isAllowedOrigin("http://127.0.0.1:3000", proxyPort, target) {
		t.Errorf("isAllowedOrigin should reject different local port")
	}

	// 6. Origin target Tailscale
	if !isAllowedOrigin("http://hermes-agent.tailnet:9119", proxyPort, target) {
		t.Errorf("isAllowedOrigin should allow origin matching target URL")
	}

	// 7. Origin esterno malevolo (CSWSH / Cross-Origin attack)
	if isAllowedOrigin("https://evil.com", proxyPort, target) {
		t.Errorf("isAllowedOrigin should reject external origin")
	}

	// 8. Origin 'null' (sandboxed iframe exploit)
	if isAllowedOrigin("null", proxyPort, target) {
		t.Errorf("isAllowedOrigin should reject 'null' origin")
	}
}

func TestProxyHandlerAuthenticationAndWebSocketBypass(t *testing.T) {
	const secret = "test-token-xyz"
	const proxyPort = 18800
	target, _ := url.Parse("http://target.tailnet:8080")

	// Dummy backend proxy handler
	backendHit := false
	backend := http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		backendHit = true
		w.WriteHeader(http.StatusOK)
		_, _ = w.Write([]byte("OK"))
	})

	// Canale tsReady già chiuso per non attendere i 15s nel test
	readyChan := make(chan struct{})
	close(readyChan)

	handler := createProxyHandler(backend, secret, proxyPort, target, readyChan)

	// SCENARIO 1: Richiesta HTTP normale senza credenziali -> 403 Forbidden
	{
		backendHit = false
		req, _ := http.NewRequest("GET", "http://127.0.0.1:18800/api/status", nil)
		rec := httptest.NewRecorder()
		handler.ServeHTTP(rec, req)

		if rec.Code != http.StatusForbidden {
			t.Errorf("Expected 403 for unauthenticated HTTP request, got %d", rec.Code)
		}
		if backendHit {
			t.Errorf("Backend should not be reached for unauthenticated request")
		}
		if rec.Header().Get("Set-Cookie") != "" {
			t.Errorf("Unauthenticated request must NOT receive Set-Cookie")
		}
	}

	// SCENARIO 2: Richiesta con header WebSocket contraffatto (Sec-WebSocket-Key) senza token (TODO #77 vulnerability test)
	{
		backendHit = false
		req, _ := http.NewRequest("GET", "http://127.0.0.1:18800/ws", nil)
		req.Header.Set("Sec-WebSocket-Key", "dGhlIHNhbXBsZSBub25jZQ==")
		req.Header.Set("Upgrade", "websocket")
		req.Header.Set("Connection", "Upgrade")
		rec := httptest.NewRecorder()
		handler.ServeHTTP(rec, req)

		if rec.Code != http.StatusForbidden {
			t.Errorf("VULNERABILITY FAIL (TODO #77): Expected 403 for fake WebSocket without token, got %d", rec.Code)
		}
		if backendHit {
			t.Errorf("Backend should not be reached for fake WebSocket without token")
		}
		if rec.Header().Get("Set-Cookie") != "" {
			t.Errorf("Fake WebSocket without token must NOT receive Set-Cookie")
		}
	}

	// SCENARIO 3: Richiesta con token valido ma Origin malevolo (CSWSH) -> 403 Forbidden
	{
		backendHit = false
		req, _ := http.NewRequest("GET", "http://127.0.0.1:18800/api", nil)
		req.Header.Set("X-HidaChat-Local-Token", secret)
		req.Header.Set("Origin", "https://malicious-website.com")
		rec := httptest.NewRecorder()
		handler.ServeHTTP(rec, req)

		if rec.Code != http.StatusForbidden {
			t.Errorf("Expected 403 for malicious origin, got %d", rec.Code)
		}
		if backendHit {
			t.Errorf("Backend should not be reached when origin is malicious")
		}
	}

	// SCENARIO 4: Handshake WebSocket autenticato con cookie e Origin corretto -> Successo (200 OK backend)
	{
		backendHit = false
		req, _ := http.NewRequest("GET", "http://127.0.0.1:18800/ws", nil)
		req.Header.Set("Upgrade", "websocket")
		req.Header.Set("Sec-WebSocket-Key", "dGhlIHNhbXBsZSBub25jZQ==")
		req.Header.Set("Origin", "http://127.0.0.1:18800")
		req.AddCookie(&http.Cookie{Name: "__htok", Value: secret})
		rec := httptest.NewRecorder()
		handler.ServeHTTP(rec, req)

		if rec.Code != http.StatusOK {
			t.Errorf("Authenticated WebSocket handshake should succeed, got %d", rec.Code)
		}
		if !backendHit {
			t.Errorf("Backend should be reached for authenticated WebSocket handshake")
		}
		cookie := rec.Header().Get("Set-Cookie")
		if !strings.Contains(cookie, "__htok="+secret) || !strings.Contains(cookie, "HttpOnly") {
			t.Errorf("Expected HttpOnly __htok cookie in response, got %s", cookie)
		}
	}

	// SCENARIO 5: Navigazione top-level iniziale con query param __htok -> Successo + Set-Cookie HttpOnly
	{
		backendHit = false
		req, _ := http.NewRequest("GET", "http://127.0.0.1:18800/?__htok="+secret, nil)
		rec := httptest.NewRecorder()
		handler.ServeHTTP(rec, req)

		if rec.Code != http.StatusOK {
			t.Errorf("Initial page load with __htok query param should succeed, got %d", rec.Code)
		}
		if !backendHit {
			t.Errorf("Backend should be reached for valid __htok query param")
		}
		cookie := rec.Header().Get("Set-Cookie")
		if !strings.Contains(cookie, "__htok="+secret) || !strings.Contains(cookie, "HttpOnly") {
			t.Errorf("Expected HttpOnly __htok cookie in response, got %s", cookie)
		}
	}
}
