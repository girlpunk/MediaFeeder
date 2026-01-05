#!/usr/bin/env python
"""Player for generic video files on a chromecast."""
# vim: tw=0 ts=4 sw=4

from __future__ import annotations

import argparse
import asyncio
import logging
import sys

from typing_extensions import Self

import Api_pb2
import common
import pychromecast
from pychromecast.controllers.media import STREAM_TYPE_BUFFERED
from pychromecast.controllers.media import BaseMediaPlayer
from pychromecast.controllers.media import MediaStatus
from pychromecast.controllers.media import MediaStatusListener
from pychromecast.controllers.media import DefaultMediaReceiverController
from pychromecast.response_handler import WaitResponse


# https://github.com/home-assistant-libs/pychromecast/blob/master/pychromecast/controllers/media.py
def pycast_status_to_mf_state(status: MediaStatus) -> int:
    """Convert Chromecast status strings to MediaFeeder status Enum."""
    if not status.supports_seek or not status.supports_pause:
        return Api_pb2.ADVERT
    return {
        "UNKNOWN": Api_pb2.UNKNOWN,
        "BUFFERING": Api_pb2.LOADING,
        "PLAYING": Api_pb2.PLAYING,
        "PAUSED": Api_pb2.PAUSED,
        "IDLE": Api_pb2.IDLE,
    }[status.player_state]


class VidFilePlayer(common.PlayerBase, MediaStatusListener):
    """Player for generic video files on a chromecast."""

    _shuffler: common.Shuffler
    _cast_name: str
    _cast: pychromecast.Chromecast
    _cast_con: BaseMediaPlayer
    _url_to_video_id: dict[str, int] = {}

    def __init__(self, cast_name: str, *, verbose: bool) -> None:
        """Set up variables, but doesn't connect yet."""
        self._logger = logging.getLogger(f"VidFilePlayer: {cast_name}")
        if verbose:
            self._logger.setLevel(logging.DEBUG)
        self._logger.debug("VideoFilePlayer Init")

        self._shuffler = common.Shuffler(f"{cast_name} (file)", self, verbose=verbose)
        self._cast_name = cast_name

        # only has for play_media(), rest is via _cast.media_controller
        # https://github.com/home-assistant-libs/pychromecast/blob/master/pychromecast/controllers/media.py
        self._cast_con = DefaultMediaReceiverController()

    async def __aenter__(self) -> Self:
        """Connect to Chromecast.

        Returns:
            VidFilePlayer
        """
        self._logger.debug("AEnter")
        self._loop = asyncio.get_running_loop()

        await self._shuffler.__aenter__()
        return self

    async def __aexit__(self, *args: object) -> None:
        """Disconnect."""
        self._logger.debug("AExit")
        await self._shuffler.__aexit__(*args)
        # browser.stop_discovery()  # TODO?

    async def update_cast(self) -> bool:
        """Update status from ChromeCast.

        Returns:
            bool: Update success

        """
        try:
            self._cast.media_controller.update_status()
        except pychromecast.error.NotConnected:
            self._logger.warning("Waiting for chromecast...")
            await asyncio.sleep(3)
            return False
        else:
            return True

    # PlayerBase
    async def ensure_ready(self) -> None:
        if self._cast.app_id == pychromecast.config.APP_MEDIA_RECEIVER:
            await self.update_cast()

    # PlayerBase
    async def play_video(self, video: Api_pb2.VideoReply) -> None:
        """Play a video immidiately."""
        self._logger.debug("Play Video")

        if not video.MediaUrl:
            self._logger.error("Can not play video without a MediaUrl: %s", video)
            # TODO signal back that playback failed.
            return

        media_url = video.MediaUrl
        media_type = "video/mp4"  # TODO identify content type, probably back in MF
        self._url_to_video_id[media_url] = video.Id

        timeout = 30  # TODO constant or do this better?
        response_handler = WaitResponse(timeout, f"play {media_url}")
        self._cast_con.play_media(media_url, media_type, stream_type=STREAM_TYPE_BUFFERED, callback_function=response_handler.callback)
        response_handler.wait_response()

    # PlayerBase
    async def play_pause(self) -> None:
        """Toggle the paused state."""
        if self._cast.media_controller.status.player_is_paused:
            self._logger.info("PlayPause: playing...")
            self._cast.media_controller.play()
        elif self._cast.media_controller.status.player_is_playing:
            self._logger.info("PlayPause: pausing...")
            self._cast.media_controller.pause()
        else:
            self._logger.warning("Don't know how to play/pause from state %s", self._cast.media_controller.status.player_state)

    # PlayerBase
    async def pause_if_playing(self) -> None:
        """Pause, but only if in playing state."""
        if self._cast.media_controller.status.player_is_playing:
            self._cast.media_controller.pause()

    # PlayerBase
    async def seek(self, position_seconds: int) -> None:
        """Seek to a position in the video."""
        self._cast.media_controller.seek(position_seconds)

    async def main(self) -> None:
        """Find chromecast and run main event loop."""
        self._logger.debug("Main")

        await asyncio.to_thread(self.sync_find_cast)
        await self._shuffler.start()

    def sync_find_cast(self):
        chromecasts, browser = pychromecast.get_listed_chromecasts(
            friendly_names=[self._cast_name],
            known_hosts=[],
        )
        if not chromecasts:
            self._logger.error('No chromecast with name "%s" discovered', self._cast_name)
            sys.exit(1)
        self._cast = chromecasts[0]
        self._cast.wait()  # Start socket client's worker thread and wait for initial status update
        self._cast.register_handler(self._cast_con)
        self._cast.media_controller.register_status_listener(self)

    def sync_send_status(self, update: common.StatusUpdate):
        asyncio.run_coroutine_threadsafe(self._shuffler.send_status(update), self._loop)

    # MediaStatusListener
    def new_media_status(self, status: MediaStatus) -> None:
        update = common.StatusUpdate()
        update.State = pycast_status_to_mf_state(status)

        # if advert, only update state and nothing else.
        if update.State == Api_pb2.ADVERT:
            self.sync_send_status(update)
            return

        if status.current_time is not None:
            update.Position = int(status.current_time)
        update.Volume = int(status.volume_level * 100)
        update.Rate = status.playback_rate
        # update.Subtitles = str(status.current_subtitle_tracks)  # TODO not in StatusUpdate
        # update.Provider = "Youtube"  # TODO what should this be?

        self.sync_send_status(update)

        if status.player_state == "IDLE" and status.idle_reason == "FINISHED":
            self._logger.info("Received playback finished event: %s", status.content_id)
            if status.content_id is not None:
                asyncio.run_coroutine_threadsafe(self._shuffler.finished(self._url_to_video_id[status.content_id]), self._loop)
            else:
                self._logger.warning("End of playbacks status missing content_id: %s", status)

    # MediaStatusListener
    def load_media_failed(self, queue_item_id: int, error_code: int) -> None:
        """Process a media fail event from the chromecast."""
        self._logger.warning("load media failed for queue item id: %s with code %s", queue_item_id, error_code)


async def _main() -> None:
    """Execute the main entrypoint."""
    common.set_logging()

    parser = argparse.ArgumentParser(description="Youtube Chromecast Video File Controller.")
    parser.add_argument("--cast", required=True, help="Name of cast device")
    parser.add_argument("--verbose", "-v", action="store_true", help="Set log level to DEBUG")
    args = parser.parse_args()

    if args.verbose:
        logging.getLogger("pychromecast").setLevel(logging.DEBUG)

    async with VidFilePlayer(cast_name=args.cast, verbose=args.verbose) as player:
        await player.main()


if __name__ == "__main__":
    # https://docs.python.org/3/library/asyncio-dev.html#asyncio-debug-mode
    asyncio.run(_main(), debug=True)
