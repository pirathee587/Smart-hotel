import time
import threading
from typing import Dict, List, Tuple
from app.config import settings


class CustomerRateLimiter:
    """
    In-memory sliding-window rate limiter per customer ('sub').
    Limits requests to 20 messages per minute.
    Thread-safe and deterministic for testing.
    """
    def __init__(self, max_requests: int = 20, window_seconds: int = 60):
        self.max_requests = max_requests
        self.window_seconds = window_seconds
        self._requests: Dict[str, List[float]] = {}
        self._lock = threading.Lock()

    def check_rate_limit(self, customer_id: str) -> Tuple[bool, int]:
        now = time.time()
        window_start = now - self.window_seconds

        with self._lock:
            if customer_id not in self._requests:
                self._requests[customer_id] = [now]
                return True, 0

            # Prune timestamps outside current window
            timestamps = [ts for ts in self._requests[customer_id] if ts > window_start]

            if len(timestamps) >= self.max_requests:
                oldest_in_window = timestamps[0]
                retry_after = int(max(1, (oldest_in_window + self.window_seconds) - now))
                self._requests[customer_id] = timestamps
                return False, retry_after

            timestamps.append(now)
            self._requests[customer_id] = timestamps
            return True, 0

    def reset(self, customer_id: str = None):
        with self._lock:
            if customer_id:
                self._requests.pop(customer_id, None)
            else:
                self._requests.clear()


customer_rate_limiter = CustomerRateLimiter(
    max_requests=settings.RATE_LIMIT_PER_MINUTE,
    window_seconds=60
)
