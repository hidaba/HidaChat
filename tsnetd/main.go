package main

import (
	"bufio"
	"context"
	"crypto/tls"
	"encoding/json"
	"flag"
	"fmt"
	"log"
	"net"
	"net/http"
	"net/http/httputil"
	"net/url"
	"os"
	"strings"
	"sync"
	"time"

	"tailscale.com/tsnet"
)

type CommandMsg struct {
	Cmd        string `json:"cmd"`
	AccountId  string `json:"accountId,omitempty"`
	Target     string `json:"target,omitempty"`
	LocalToken string `json:"localToken,omitempty"`
	AuthKey    string `json:"authKey,omitempty"`
	Port       int    `json:"port,omitempty"`
}

type StatusResp struct {
	NodeState string `json:"nodeState"`
	TailnetIP string `json:"tailnetIP,omitempty"`
	LoginURL  string `json:"loginUrl,omitempty"`
	Error     string `json:"error,omitempty"`
}

type RouteResp struct {
	AccountId string `json:"accountId"`
	LocalPort int    `json:"localPort,omitempty"`
	Success   bool   `json:"success"`
	Error     string `json:"error,omitempty"`
}

type GenericResp struct {
	Status  string `json:"status,omitempty"`
	Success bool   `json:"success"`
	Error   string `json:"error,omitempty"`
}

type RouteEntry struct {
	listener net.Listener
	server   *http.Server
	port     int
	target   string
}

type retryTransport struct {
	base http.RoundTripper
}

func (rt *retryTransport) RoundTrip(req *http.Request) (*http.Response, error) {
	var resp *http.Response
	var err error
	maxAttempts := 3
	if req.Method != "GET" && req.Method != "HEAD" {
		maxAttempts = 1
	}
	for attempt := 1; attempt <= maxAttempts; attempt++ {
		resp, err = rt.base.RoundTrip(req)
		if err == nil {
			return resp, nil
		}
		if attempt < maxAttempts {
			log.Printf("[tsnetd][proxy] attempt %d to %s failed (%v), retrying in 1s...", attempt, req.URL.String(), err)
			select {
			case <-time.After(1 * time.Second):
			case <-req.Context().Done():
				return nil, req.Context().Err()
			}
		}
	}
	return resp, err
}

var (
	routesMu    sync.Mutex
	routes      = make(map[string]*RouteEntry)
	outMu       sync.Mutex
	tsReady     = make(chan struct{})
	tsReadyOnce sync.Once
)

func writeJSON(v any) {
	outMu.Lock()
	defer outMu.Unlock()
	b, err := json.Marshal(v)
	if err != nil {
		fmt.Fprintf(os.Stderr, "json marshal err: %v\n", err)
		return
	}
	os.Stdout.Write(b)
	os.Stdout.WriteString("\n")
}

func main() {
	stateDir := flag.String("dir", "data/tsnet", "Directory for tsnet state")
	hostname := flag.String("hostname", "hidachat", "Hostname in tailnet")
	authKey := flag.String("authkey", "", "Tailscale auth key")
	flag.Parse()

	log.SetOutput(os.Stderr)
	log.Printf("[tsnetd] Starting with state dir: %s, hostname: %s", *stateDir, *hostname)

	_ = os.MkdirAll(*stateDir, 0755)

	tsServer := &tsnet.Server{
		Dir:      *stateDir,
		Hostname: *hostname,
		AuthKey:  *authKey,
		Logf: func(format string, args ...any) {
			log.Printf("[tsnet] "+format, args...)
		},
	}
	defer tsServer.Close()

	go func() {
		ctx, cancel := context.WithTimeout(context.Background(), 45*time.Second)
		defer cancel()
		_, err := tsServer.Up(ctx)
		if err != nil {
			log.Printf("[tsnetd] tsServer.Up error: %v", err)
		} else {
			log.Printf("[tsnetd] tsServer.Up succeeded")
			tsReadyOnce.Do(func() { close(tsReady) })
		}
	}()

	scanner := bufio.NewScanner(os.Stdin)
	for scanner.Scan() {
		line := strings.TrimSpace(scanner.Text())
		if line == "" {
			continue
		}

		var cmd CommandMsg
		if err := json.Unmarshal([]byte(line), &cmd); err != nil {
			writeJSON(GenericResp{Success: false, Error: "invalid json: " + err.Error()})
			continue
		}

		switch cmd.Cmd {
		case "status":
			handleStatus(tsServer)
		case "add_route":
			handleAddRoute(tsServer, cmd)
		case "remove_route":
			handleRemoveRoute(cmd.AccountId)
		case "shutdown":
			writeJSON(GenericResp{Status: "ok", Success: true})
			log.Printf("[tsnetd] Shutdown requested via command")
			routesMu.Lock()
			for _, r := range routes {
				_ = r.server.Close()
			}
			routesMu.Unlock()
			_ = tsServer.Close()
			os.Exit(0)
		default:
			writeJSON(GenericResp{Success: false, Error: "unknown command: " + cmd.Cmd})
		}
	}

	if err := scanner.Err(); err != nil {
		log.Printf("[tsnetd] Stdin scanner error: %v", err)
	}
}

func handleStatus(ts *tsnet.Server) {
	ctx, cancel := context.WithTimeout(context.Background(), 3*time.Second)
	defer cancel()

	lc, err := ts.LocalClient()
	if err != nil {
		writeJSON(StatusResp{NodeState: "Stopped", Error: err.Error()})
		return
	}

	st, err := lc.Status(ctx)
	if err != nil {
		writeJSON(StatusResp{NodeState: "Stopped", Error: err.Error()})
		return
	}

	state := string(st.BackendState)
	if state == "" {
		state = "Starting"
	}
	if state == "Running" {
		tsReadyOnce.Do(func() { close(tsReady) })
	}

	var ipStr string
	if len(st.TailscaleIPs) > 0 {
		ipStr = st.TailscaleIPs[0].String()
	}

	loginURL := st.AuthURL

	writeJSON(StatusResp{
		NodeState: state,
		TailnetIP: ipStr,
		LoginURL:  loginURL,
	})
}

func handleAddRoute(ts *tsnet.Server, cmd CommandMsg) {
	if cmd.AccountId == "" || cmd.Target == "" {
		writeJSON(RouteResp{AccountId: cmd.AccountId, Success: false, Error: "missing accountId or target"})
		return
	}

	rawTarget := strings.TrimSpace(cmd.Target)
	if !strings.HasPrefix(rawTarget, "http://") && !strings.HasPrefix(rawTarget, "https://") {
		rawTarget = "http://" + rawTarget
	}

	targetURL, err := url.Parse(rawTarget)
	if err != nil || targetURL.Host == "" {
		writeJSON(RouteResp{AccountId: cmd.AccountId, Success: false, Error: "invalid target url: " + cmd.Target})
		return
	}

	routesMu.Lock()
	defer routesMu.Unlock()

	if existing, ok := routes[cmd.AccountId]; ok {
		if (cmd.Port <= 0 || existing.port == cmd.Port) && strings.EqualFold(existing.target, rawTarget) {
			log.Printf("[tsnetd] Route already active for account %s on 127.0.0.1:%d -> %s", cmd.AccountId, existing.port, cmd.Target)
			writeJSON(RouteResp{
				AccountId: cmd.AccountId,
				LocalPort: existing.port,
				Success:   true,
			})
			return
		}
		_ = existing.server.Close()
		delete(routes, cmd.AccountId)
	}

	var ln net.Listener
	if cmd.Port > 0 {
		for p := cmd.Port; p < cmd.Port+20; p++ {
			ln, err = net.Listen("tcp", fmt.Sprintf("127.0.0.1:%d", p))
			if err == nil {
				break
			}
		}
		if err != nil {
			log.Printf("[tsnetd] preferred port %d range busy (%v), falling back to dynamic port", cmd.Port, err)
			ln, err = net.Listen("tcp", "127.0.0.1:0")
		}
	} else {
		ln, err = net.Listen("tcp", "127.0.0.1:0")
	}

	if err != nil {
		writeJSON(RouteResp{AccountId: cmd.AccountId, Success: false, Error: "listen error: " + err.Error()})
		return
	}

	port := ln.Addr().(*net.TCPAddr).Port

	proxy := httputil.NewSingleHostReverseProxy(targetURL)

	baseTransport := ts.HTTPClient().Transport
	if tr, ok := baseTransport.(*http.Transport); ok {
		trClone := tr.Clone()
		if trClone.TLSClientConfig == nil {
			trClone.TLSClientConfig = &tls.Config{InsecureSkipVerify: true}
		} else {
			trClone.TLSClientConfig.InsecureSkipVerify = true
		}
		proxy.Transport = &retryTransport{base: trClone}
	} else {
		proxy.Transport = &retryTransport{base: baseTransport}
	}

	proxy.ErrorHandler = func(w http.ResponseWriter, r *http.Request, proxyErr error) {
		log.Printf("[tsnetd][proxy] error proxying to %s: %v", targetURL.String(), proxyErr)
		if strings.Contains(r.Header.Get("Accept"), "text/html") {
			w.Header().Set("Content-Type", "text/html; charset=utf-8")
			w.Header().Set("Retry-After", "2")
			w.WriteHeader(http.StatusBadGateway)
			fmt.Fprintf(w, `<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8">
    <meta http-equiv="refresh" content="2">
    <title>Connessione a OpenClaw...</title>
    <style>
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: #111b21; color: #e9edef; display: flex; align-items: center; justify-content: center; height: 100vh; margin: 0; }
        .box { text-align: center; max-width: 420px; padding: 24px; }
        .spinner { width: 36px; height: 36px; border: 3px solid rgba(255,255,255,0.2); border-top-color: #00a884; border-radius: 50%; animation: spin 1s linear infinite; margin: 0 auto 16px; }
        @keyframes spin { to { transform: rotate(360deg); } }
        h2 { font-size: 18px; margin-bottom: 8px; }
        p { font-size: 13px; color: #8696a0; line-height: 1.4; }
    </style>
</head>
<body>
    <div class="box">
        <div class="spinner"></div>
        <h2>Connessione alla rete Tailscale in corso...</h2>
        <p>Collegamento a OpenClaw in corso, la pagina si aggiornerà automaticamente.</p>
    </div>
</body>
</html>`)
			return
		}
		http.Error(w, "Bad Gateway (Tailscale tsnet): "+proxyErr.Error(), http.StatusBadGateway)
	}

	origDirector := proxy.Director
	proxy.Director = func(req *http.Request) {
		origDirector(req)
		req.Host = targetURL.Host
		req.Header.Set("X-Forwarded-Host", targetURL.Host)
		req.Header.Set("X-Forwarded-Proto", targetURL.Scheme)
		req.Header.Del("X-HidaChat-Local-Token")

		// Riscrivi Origin per superare i controlli CORS / WebSocket origin check del backend
		if req.Header.Get("Origin") != "" || strings.EqualFold(req.Header.Get("Upgrade"), "websocket") {
			req.Header.Set("Origin", targetURL.Scheme+"://"+targetURL.Host)
		}
	}

	handler := http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		valid := false
		isWs := strings.EqualFold(r.Header.Get("Upgrade"), "websocket") || r.Header.Get("Sec-WebSocket-Key") != ""

		if cmd.LocalToken == "" || isWs {
			valid = true
		} else {
			if r.Header.Get("X-HidaChat-Local-Token") == cmd.LocalToken {
				valid = true
			}
			if !valid && r.URL.Query().Get("__htok") == cmd.LocalToken {
				valid = true
			}
			if !valid {
				if c, err := r.Cookie("__htok"); err == nil && c.Value == cmd.LocalToken {
					valid = true
				}
			}
		}

		if !valid {
			http.Error(w, "Forbidden: Invalid HidaChat Local Token", http.StatusForbidden)
			return
		}

		// Rilascia sempre il cookie per le richieste successive
		if cmd.LocalToken != "" {
			http.SetCookie(w, &http.Cookie{
				Name:     "__htok",
				Value:    cmd.LocalToken,
				Path:     "/",
				SameSite: http.SameSiteLaxMode,
			})
		}

		// Attendi che il nodo Tailscale sia pronto prima di inoltrare la richiesta (fino a 15s)
		select {
		case <-tsReady:
		case <-time.After(15 * time.Second):
			log.Printf("[tsnetd][proxy] warning: tsReady timeout, proceeding anyway")
		case <-r.Context().Done():
			return
		}

		proxy.ServeHTTP(w, r)
	})

	srv := &http.Server{
		Handler: handler,
	}

	go func() {
		if err := srv.Serve(ln); err != nil && err != http.ErrServerClosed {
			log.Printf("[tsnetd] Route server for %s closed: %v", cmd.AccountId, err)
		}
	}()

	routes[cmd.AccountId] = &RouteEntry{
		listener: ln,
		server:   srv,
		port:     port,
		target:   rawTarget,
	}

	log.Printf("[tsnetd] Route added for account %s -> 127.0.0.1:%d -> %s", cmd.AccountId, port, cmd.Target)
	writeJSON(RouteResp{
		AccountId: cmd.AccountId,
		LocalPort: port,
		Success:   true,
	})
}

func handleRemoveRoute(accountId string) {
	routesMu.Lock()
	defer routesMu.Unlock()

	if entry, ok := routes[accountId]; ok {
		_ = entry.server.Close()
		delete(routes, accountId)
		log.Printf("[tsnetd] Route removed for account %s", accountId)
		writeJSON(RouteResp{AccountId: accountId, Success: true})
	} else {
		writeJSON(RouteResp{AccountId: accountId, Success: true})
	}
}
