#!/usr/bin/env python
"""Remote playback for Apple TV."""

import asyncio
import logging
from typing import Any

import pyatv
import pyatv.storage.file_storage

import common

common.set_logging()
logger = logging.getLogger("ATVShuffle")


async def connect() -> None:
    """Connect to an Apple TV."""
    try:
        loop = asyncio.get_event_loop()
        loop.set_exception_handler(error)

        storage = pyatv.storage.file_storage.FileStorage.default_storage(loop)
        await storage.load()

        atv = await pyatv.scan(loop, hosts=["10.101.15.6"])  # 192.168.164.103"])
        device = await pyatv.connect(atv[0], loop=loop, storage=storage)

        listener = MyPushListener(device)
        device.push_updater.listener = listener
        device.push_updater.start()

        # wait forever
        await asyncio.Event().wait()
    except Exception:
        logger.exception("Error while connecting")
        raise
    finally:
        device.close()


class MyPushListener(pyatv.interface.PushListener):
    """Recieve events from Apple TV."""

    def __init__(self, device: pyatv.interface.AppleTV) -> None:
        """Initialise reciever."""
        try:
            self._device = device
        except Exception:
            logger.exception("Error while recieving data from Apple TV")
            raise

    def playstatus_update(self, _updater: pyatv.interface.PushUpdater, playstatus: pyatv.interface.Playing) -> None:
        """Update playback status."""
        asyncio.run_coroutine_threadsafe(self._update_playing(playstatus), asyncio.get_event_loop())

    def playstatus_error(self, _updater: pyatv.interface.PushUpdater, exception: Exception) -> None:
        """Report an error in exception."""
        logger.exception("an error happened! %o", exception)

    async def _update_playing(self, playing: pyatv.interface.Playing) -> None:
        """Update playback status."""
        try:
            logger.info("Playing update: %s", playing)
            logger.info(playing)

            logger.info("media_type: %s", playing.media_type.name)
            logger.info("device_state: %s", playing.device_state.name)
            logger.info("title: %s", playing.title)
            logger.info("artist: %s", playing.artist)
            logger.info("album: %s", playing.album)
            logger.info("genre: %s", playing.genre)
            logger.info("total_time: %s", playing.total_time)
            logger.info("position: %s", playing.position)
            logger.info("shuffle: %s", playing.shuffle)
            logger.info("repeat: %s", playing.repeat)
            logger.info("hash: %s", playing.hash)
            logger.info("series_name: %s", playing.series_name)
            logger.info("season_number: %s", playing.season_number)
            logger.info("episode_number: %s", playing.episode_number)
            logger.info("content_identifier: %s", playing.content_identifier)
            logger.info("itunes_store_identifier: %s", playing.itunes_store_identifier)

        except Exception:
            logger.exception("Error while processing update")
            raise


def error(_loop: asyncio.events.AbstractEventLoop, context: dict[str, Any]) -> None:
    """Report an error."""
    exception = context["exception"]
    logger.exception("got exception %o", exception)


if __name__ == "__main__":
    device = asyncio.run(connect(), debug=True)
