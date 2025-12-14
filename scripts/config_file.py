from contextlib import AbstractContextManager
from pathlib import Path

import fasteners
import yaml


class ConfigFile(AbstractContextManager):
    def __init__(self, path):
        self._path = path
        self._lock = fasteners.InterProcessReaderWriterLock(f"{path}.lock")

    def __enter__(self):
        self._lock.acquire_write_lock(timeout=30)
        return self.Config(self)

    def __exit__(self, exc_type, exc_value, traceback):
        self._lock.release_write_lock()

    class Config:
        def __init__(self, cf):
            self._cf = cf
            with Path(self._cf._path).open("r", encoding="utf-8") as f:
                self._data = yaml.safe_load(f)

        def __contains__(self, key):
            return self._data.__contains__(key)

        def __getitem__(self, key):
            return self._data.__getitem__(key)

        def __setitem__(self, key, value):
            return self._data.__setitem__(key, value)

        def __len__(self):
            return self._data.__len__()

        def write(self):
            with Path(self._cf._path).open("w", encoding="utf-8") as f:
                yaml.dump(self._data, f)


# vim: tw=0 ts=4 sw=4
