# Embedding the koto-agent React widget

`koto-agent` on npm — embeddable AI agent chat widget for React. The agent works on top of an OpenAPI spec: it picks operations of the host app's API. **HTTP to your API runs in the browser** (same origin/cookies as the rest of the app). The agent-server only talks to the LLM.

Full docs: the package README on npm, and https://koto-agent.ru (SaaS cabinet, Russian).

## Two paths to a publicKey

- **SaaS:** register at koto-agent.ru → verify email → upload the OpenAPI spec in the cabinet → wait until spec status is Ready → copy `pk_...`.
- **Self-hosted:** run the Koto agent-server, authenticate, then `POST /api/specs` with `{content}` and `Authorization: Bearer <JWT>` → response contains `publicKey`.

Do **not** send a host URL when registering a spec. The widget resolves URLs on the page: `baseUrl` prop, otherwise `window.location.origin`, plus the path from swagger.

Keep LLM context on the server (Redis `ConversationStore`, user/assistant only). The widget must **not** POST chat history each turn — only the new `message` + `connectionId`. Customer API payloads stay in the browser; `CompleteApiCall` sends the current tool result to the LLM for that turn and Redis does not store it.

`publicKey` is safe to expose in the browser.

## Integration

```bash
npm install koto-agent        # peer deps: react@^18, react-dom@^18
```

```jsx
import KotoAgent from 'koto-agent'
import 'koto-agent/dist/style.css'

<KotoAgent
  agentUrl="https://agent.example.com"
  publicKey="pk_..."
  mode="floating"
  theme="auto"
  skin="default"
  onMessage={() => refetchData()}
/>
```

Same host as the SPA — no extra props. Other gateway / JWT / axios:

```jsx
<KotoAgent publicKey="pk_..." baseUrl="https://api.example.com" />

<KotoAgent
  publicKey="pk_..."
  request={(url, init) => fetch(url, {
    ...init,
    headers: { ...init.headers, Authorization: `Bearer ${token}` },
  })}
/>

<KotoAgent
  publicKey="pk_..."
  request={(url, init) => axios({
    url, method: init.method, data: init.body, withCredentials: true,
  })}
/>
```

Optional: `position`, `title`, `placeholder`, `initialOpen`, `width`/`height`, `userId`, `model`, `className`, `authHeader` (adds Authorization in the widget if `request` is not used; not sent to agent-server), `skin` (`default` | `minimal` | `contrast`, omit for `default`), `tokens` (CSS variables on the root; keys `--koto-accent` or `accent`). When `publicKey` is set, the built-in settings panel is hidden.

`request` gets a fully resolved URL (`baseUrl` + swagger path) and fetch-style `init`. Return `Response`, `{ status, data }` (axios), or a string. For an axios instance with interceptors, parse `init.body` if it is a JSON string and pass `baseUrl={api.defaults.baseURL}` so the URL is absolute (axios will not double-prefix).

## Theming

Styles are scoped under `.koto-root`. Default is zinc + system fonts. `theme` is light/dark/auto. `skin` is the chrome (`default`, `minimal`, `contrast`). `tokens` override any `--koto-*` value:

```jsx
<KotoAgent
  publicKey="pk_..."
  skin="minimal"
  theme="dark"
  tokens={{ accent: '#2563eb', 'accent-2': '#1d4ed8' }}
/>
```

## Notes

- Widget UI strings are in Russian.
- SignalR: `${agentUrl}/chatHub` — reverse proxy must pass WebSockets.
- In Vite, proxy `/chatHub` (`ws: true`) and `/api/agent` to agent-server; `agentUrl=''`.
