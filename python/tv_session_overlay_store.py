"""In-memory session HUD state for Tizen apps (peek-on-read, not consumed)."""

from __future__ import annotations

import threading
from typing import Any, Optional

_lock = threading.Lock()
# tv_id (str) -> session overlay payload
_store: dict[str, dict[str, Any]] = {}


def _key(tv_id: Optional[str | int]) -> str:
    if tv_id is None or str(tv_id).strip() == "":
        return "default"
    return str(tv_id).strip()


def _empty() -> dict[str, Any]:
    return {
        "active": False,
        "package_name": "",
        "customer_name": "",
        "billing_mode": "Fixed",
        "ends_at": None,
    }


def set_session_overlay(
    tv_id: Optional[str | int],
    *,
    active: bool,
    package_name: str = "",
    customer_name: str = "",
    billing_mode: str = "Fixed",
    ends_at: Optional[str] = None,
) -> dict[str, Any]:
    key = _key(tv_id)
    if not active:
        with _lock:
            _store.pop(key, None)
        return _empty()

    mode = (billing_mode or "Fixed").strip() or "Fixed"
    entry = {
        "active": True,
        "package_name": (package_name or "").strip(),
        "customer_name": (customer_name or "").strip(),
        "billing_mode": mode,
        "ends_at": (ends_at or "").strip() or None,
    }
    with _lock:
        _store[key] = entry
    return dict(entry)


def get_session_overlay(tv_id: Optional[str | int] = None) -> dict[str, Any]:
    key = _key(tv_id)
    with _lock:
        entry = _store.get(key)
        if entry is None:
            return _empty()
        return dict(entry)


def clear_all_sessions() -> None:
    with _lock:
        _store.clear()
