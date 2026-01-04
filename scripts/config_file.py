"""Accessor provider for config file."""

from __future__ import annotations

from contextlib import AbstractContextManager
from pathlib import Path
from typing import TYPE_CHECKING, Any

import fasteners
import yaml

if TYPE_CHECKING:
    from types import TracebackType


class ConfigFile(AbstractContextManager):
    """Accessor provider for config file."""

    def __init__(self, path: str) -> None:
        """Prepare configuration for access."""
        self.path = path
        self._lock = fasteners.InterProcessReaderWriterLock(f"{path}.lock")

    def __enter__(self) -> Config:
        """Obtain configuration instance."""
        self._lock.acquire_write_lock(timeout=30)
        return self.Config(self)

    def __exit__(self, exc_type: type[BaseException] | None, exc_value: BaseException | None, traceback: TracebackType | None) -> None:
        """Release configuration instance."""
        self._lock.release_write_lock()

    class Config:
        """Accessor for configuration values."""

        def __init__(self, cf: ConfigFile) -> None:
            """Load the configuration."""
            self._cf = cf
            with Path(self._cf.path).open("r", encoding="utf-8") as f:
                self._data = yaml.safe_load(f)

        def __contains__(self, key: str) -> bool:
            """Check for the existance of an item in the configuraiton root."""
            return self._data.__contains__(key)

        def __getitem__(self, key: str) -> Any:
            """Get an item in the configuraiton root."""
            return self._data.__getitem__(key)

        def __setitem__(self, key: str, value: Any) -> Any:
            """Set an item in the configuration root."""
            return self._data.__setitem__(key, value)

        def __len__(self) -> int:
            """Get length of configuration root."""
            return self._data.__len__()

        def write(self) -> None:
            """Write changes to disk."""
            with Path(self._cf.path).open("w", encoding="utf-8") as f:
                yaml.dump(self._data, f)


# vim: tw=0 ts=4 sw=4
