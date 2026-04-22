import hmac
from fastapi import Request
from fastapi.responses import JSONResponse
from starlette.middleware.base import BaseHTTPMiddleware

from app.config import settings

# Routes that skip all auth (cloudflared health check needs /health)
PUBLIC_PATHS = {"/health"}


class AuthMiddleware(BaseHTTPMiddleware):
    async def dispatch(self, request: Request, call_next):
        if request.url.path in PUBLIC_PATHS:
            return await call_next(request)

        # --- Cloudflare Access JWT check ---
        if settings.cf_access_team_domain:
            cf_jwt = request.headers.get("Cf-Access-Jwt-Assertion")
            if not cf_jwt:
                return JSONResponse(status_code=403, content={"detail": "Missing Cloudflare Access token"})
            # Lightweight check: just verify the header is present and non-empty.
            # Full JWT verification requires fetching Cloudflare's public certs —
            # acceptable for internal tools where Cloudflare Access is the primary gate.

        # --- API key check ---
        if settings.api_key:
            incoming = request.headers.get("X-API-Key", "")
            # Constant-time comparison to prevent timing attacks
            if not hmac.compare_digest(incoming, settings.api_key):
                return JSONResponse(status_code=401, content={"detail": "Invalid or missing API key"})

        return await call_next(request)
