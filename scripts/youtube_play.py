#!/usr/bin/env python
"""Play a list of YouTube Videos."""

import argparse
import logging
import sys
import time
from threading import Event

import pychromecast
from pychromecast.controllers.media import MediaStatus, MediaStatusListener
from pychromecast.controllers.youtube import YouTubeController

import common

common.set_logging()

logger = logging.getLogger("youtube_play")


class MyMediaStatusListener(MediaStatusListener):
    """Status media listener."""

    def __init__(self) -> None:
        """Complete initial setup."""
        self._logger = logging.getLogger("MyMediaStatusListener")
        self.last_is_idle = Event()
        self.reset()

    def reset(self) -> None:
        """Reset the event hook."""
        self.last_is_idle.clear()

    def wait(self, timeout: int) -> bool:
        """Wait for the Chromecast to be idle."""
        return self.last_is_idle.wait(timeout)

    def new_media_status(self, status: MediaStatus) -> None:
        """Recieve a status update from the Chromecast."""
        # self._logger.debug("new_media_status: %s", status)

        if status.player_state == "IDLE" and status.idle_reason == "FINISHED":
            self.last_is_idle.set()
        else:
            self.last_is_idle.clear()

        # This is for adverts
        if not status.supports_pause:
            return

    def load_media_failed(self, queue_item_id: int, error_code: int) -> None:
        """Log results of an error on the Chromecast side."""
        self._logger.info(
            "load media failed for queue item id: %s with code: %s",
            queue_item_id,
            error_code,
        )


# Enable deprecation warnings etc.
if not sys.warnoptions:
    import warnings

    warnings.simplefilter("default")

parser = argparse.ArgumentParser(description="Example on how to use the Youtube Controller.")
parser.add_argument("--cast", help="Name of cast device")
parser.add_argument("--known-host", help="Add known host (IP), can be used multiple times", action="append")
parser.add_argument("--videos", help="YouTube video IDs to play", nargs="+", default=[], required=True)
args = parser.parse_args()

chromecasts, browser = pychromecast.get_listed_chromecasts(friendly_names=[args.cast], known_hosts=args.known_host)

if not chromecasts:
    logger.error("No chromecast with name %s discovered", args.cast)
    sys.exit(1)

cast = chromecasts[0]
# Start socket client's worker thread and wait for initial status update
cast.wait()


logger.info("Videos to play:")
for video in args.videos:
    logger.info("  - %s", video)

yt = YouTubeController()
cast.register_handler(yt)

listener_media = MyMediaStatusListener()
cast.media_controller.register_status_listener(listener_media)


while len(args.videos) > 0:
    video = args.videos.pop(0)

    logger.info("Playing: %s", video)

    yt.play_video(video)
    listener_media.reset()

    while not listener_media.wait(5):
        cast.media_controller.update_status()

    time.sleep(1)


# Shut down discovery
browser.stop_discovery()

logger.info("fin.")
