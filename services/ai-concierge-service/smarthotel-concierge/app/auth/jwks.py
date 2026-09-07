import time
import logging
from typing import Optional, Dict, Any
import httpx
import jwt
from jwt.algorithms import RSAAlgorithm
from fastapi import Request, HTTPException, Security, status
from fastapi.security import HTTPBearer, HTTPAuthorizationCredentials
from app.config import settings
from app.models.schemas import JWTPayload

logger = logging.getLogger(__name__)
security_scheme = HTTPBearer(auto_error=False)


class JwksKeyManager:
    def __init__(self, jwks_uri: str, ttl_seconds: int = 600):
        self.jwks_uri = jwks_uri
        self.ttl_seconds = ttl_seconds
        self._keys_cache: Dict[str, Any] = {}
        self._last_fetch: float = 0

    async def get_key(self, kid: Optional[str] = None) -> Any:
        now = time.time()
        # Return from cache if valid and key exists
        if self._keys_cache and (now - self._last_fetch < self.ttl_seconds):
            if kid and kid in self._keys_cache:
                return self._keys_cache[kid]
            elif not kid and self._keys_cache:
                return next(iter(self._keys_cache.values()))

        # Otherwise refresh from JWKS endpoint
        await self._fetch_jwks()

        if kid and kid in self._keys_cache:
            return self._keys_cache[kid]
        elif not kid and self._keys_cache:
            return next(iter(self._keys_cache.values()))

        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail=f"Unable to find signing key with kid '{kid}'",
            headers={"WWW-Authenticate": "Bearer"},
        )

    async def _fetch_jwks(self):
        try:
            async with httpx.AsyncClient(timeout=5.0) as client:
                resp = await client.get(self.jwks_uri)
                if resp.status_code != 200:
                    logger.warning(f"Failed to fetch JWKS from {self.jwks_uri}, status {resp.status_code}")
                    return

                data = resp.json()
                keys = data.get("keys", [])
                new_cache = {}
                for key_dict in keys:
                    kid = key_dict.get("kid")
                    if kid:
                        public_key = RSAAlgorithm.from_jwk(key_dict)
                        new_cache[kid] = public_key

                if new_cache:
                    self._keys_cache = new_cache
                    self._last_fetch = time.time()
                    logger.info(f"Loaded {len(new_cache)} keys from JWKS endpoint {self.jwks_uri}")
        except Exception as e:
            logger.warning(f"Error fetching JWKS from {self.jwks_uri}: {e}")

    def set_key_for_testing(self, kid: str, public_key: Any):
        """Allows test fixtures to register mock public keys directly."""
        self._keys_cache[kid] = public_key
        self._last_fetch = time.time() + 999999


jwks_manager = JwksKeyManager(settings.JWT_JWKS_URI, settings.JWKS_CACHE_TTL_SECONDS)


async def get_current_user(
    credentials: Optional[HTTPAuthorizationCredentials] = Security(security_scheme)
) -> JWTPayload:
    if not credentials or not credentials.credentials:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Missing Bearer token in Authorization header",
            headers={"WWW-Authenticate": "Bearer"},
        )

    token = credentials.credentials

    try:
        unverified_header = jwt.get_unverified_header(token)
        kid = unverified_header.get("kid")
        signing_key = await jwks_manager.get_key(kid)

        payload = jwt.decode(
            token,
            key=signing_key,
            algorithms=["RS256"],
            issuer=settings.JWT_ISSUER,
            audience=settings.JWT_AUDIENCE,
            options={"verify_exp": True},
        )

        sub = payload.get("sub") or payload.get("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")
        if not sub:
            raise HTTPException(
                status_code=status.HTTP_401_UNAUTHORIZED,
                detail="Token missing 'sub' claim",
                headers={"WWW-Authenticate": "Bearer"},
            )

        role = payload.get("role") or payload.get("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", "Customer")
        email = payload.get("email") or payload.get("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")

        return JWTPayload(
            sub=str(sub),
            role=str(role),
            email=str(email) if email else None,
            exp=payload.get("exp"),
        )
    except jwt.ExpiredSignatureError:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Token has expired",
            headers={"WWW-Authenticate": "Bearer"},
        )
    except (jwt.InvalidTokenError, Exception) as e:
        logger.warning(f"JWT validation failure: {e}")
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail=f"Invalid token: {str(e)}",
            headers={"WWW-Authenticate": "Bearer"},
        )
