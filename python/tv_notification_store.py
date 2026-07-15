"""In-memory TV overlay notifications for Tizen apps (consume-on-read)."""

from __future__ import annotations

import threading
import time
from typing import Any, Optional

# If Tizen never polls, drop stale warnings after this many seconds.
DEFAULT_TTL_SECONDS = 60

_lock = threading.Lock()
# tv_id (str) -> {"show_warning": bool, "message": str, "expires_at": float|None}
_store: dict[str, dict[str, Any]] = {}


def _key(tv_id: Optional[str | int]) -> str:
    if tv_id is None or str(tv_id).strip() == "":
        return "default"
    return str(tv_id).strip()


def set_notification(
    tv_id: Optional[str | int],
    show_warning: bool,
    message: str = "",
    ttl_seconds: int = DEFAULT_TTL_SECONDS,
) -> dict[str, Any]:
    key = _key(tv_id)
    expires_at = time.monotonic() + ttl_seconds if show_warning else None
    entry = {
        "show_warning": bool(show_warning),
        "message": (message or "").strip(),
        "expires_at": expires_at,
    }
    with _lock:
        if show_warning:
            _store[key] = entry
        else:
            _store.pop(key, None)
    return {"show_warning": entry["show_warning"], "message": entry["message"]}


def consume_notification(tv_id: Optional[str | int] = None) -> dict[str, Any]:
    """Return current warning for tv_id and clear it (one-shot for Tizen poll)."""
    key = _key(tv_id)
    now = time.monotonic()
    with _lock:
        entry = _store.get(key)
        if entry is None:
            return {"show_warning": False, "message": ""}

        expires_at = entry.get("expires_at")
        if expires_at is not None and now > expires_at:
            _store.pop(key, None)
            return {"show_warning": False, "message": ""}

        if not entry.get("show_warning"):
            _store.pop(key, None)
            return {"show_warning": False, "message": ""}

        result = {
            "show_warning": True,
            "message": entry.get("message") or "",
        }
        _store.pop(key, None)
        return result


def peek_notification(tv_id: Optional[str | int] = None) -> dict[str, Any]:
    """Read without consuming (for tests / debugging)."""
    key = _key(tv_id)
    now = time.monotonic()
    with _lock:
        entry = _store.get(key)
        if entry is None:
            return {"show_warning": False, "message": ""}
        expires_at = entry.get("expires_at")
        if expires_at is not None and now > expires_at:
            return {"show_warning": False, "message": ""}
        return {
            "show_warning": bool(entry.get("show_warning")),
            "message": entry.get("message") or "",
        }


def clear_all() -> None:
    with _lock:
        _store.clear()
