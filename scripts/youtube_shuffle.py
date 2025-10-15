#!/usr/bin/env python3
"""Standard Player for the YouTube App over ChromeCast APIs."""

from __future__ import annotations

import argparse
import logging
import queue
import sys
import time
import warnings
from collections.abc import Iterator
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from queue import Queue
from typing import NamedTuple, TypeVar

import grpc
import pychromecast
import pychromecast.controllers.media
from pychromecast.controllers.media import MediaStatus, MediaStatusListener
from pychromecast.controllers.youtube import YouTubeController

import Api_pb2
import Api_pb2_grpc
import common

common.set_logging()


# https://github.com/home-assistant-libs/pychromecast/blob/master/pychromecast/controllers/media.py#L26
def pycast_status_to_mf_state(status: MediaStatus):
    if not status.supports_seek:
        return Api_pb2.ADVERT
    return {
        "UNKNOWN": Api_pb2.UNKNOWN,
        "BUFFERING": Api_pb2.LOADING,
        "PLAYING": Api_pb2.PLAYING,
        "PAUSED": Api_pb2.PAUSED,
        "IDLE": Api_pb2.IDLE,
    }[status.player_state]


class QueueEvent(NamedTuple):
    """Events for the player to process."""

    next_video_id: int = None
    go_next: bool = False
    mark_watched: bool = False
    content_id: str = None


class MyMediaStatusListener(MediaStatusListener):
    """Status media listener."""

    def __init__(
        self,
        name: str | None,
        cast: pychromecast.Chromecast,
        status_queue: Queue,
    ) -> None:
        """Initial setup."""
        self.name = name
        self.cast = cast
        self.status_queue = status_queue
        self.event_queue = Queue()
        self.last_status = None
        self._logger = logging.getLogger("MediaStatusListener")

    def get_event(self, timeout: int) -> QueueEvent | None:
        """Get next event in queue."""
        try:
            return self.event_queue.get(timeout=timeout)
        except queue.Empty:
            return None

    def new_media_status(self, status: MediaStatus) -> None:
        """Process a new media status from youtube."""
        self.last_status = status
        # print(f"new_media_status: {status}")

        # This is for adverts
        if not status.supports_pause:
            return

        if status.player_state == "IDLE" and status.idle_reason == "FINISHED":
            self._logger.info("Received finished event: %s", status.content_id)
            self.event_queue.put(
                QueueEvent(
                    go_next=True,
                    mark_watched=True,
                    content_id=status.content_id,
                ),
            )

        status_message = Api_pb2.PlaybackSessionRequest()
        status_message.Duration = int(status.current_time)
        if status.content_type == "x-youtube/video":
            status_message.Provider = "Youtube"
        status_message.State = pycast_status_to_mf_state(status)
        status_message.Volume = int(status.volume_level * 100)
        status_message.Rate = status.playback_rate
        self.status_queue.put(status_message)

    def load_media_failed(self, queue_item_id: int, error_code: int) -> None:
        """Process a media fail event from youtube."""
        self._warning(
            "load media failed for queue item id: %s with code %s",
            queue_item_id,
            error_code,
        )

    def on_ses_rep(self, iterator: Iterator[Api_pb2.PlaybackSessionReply]) -> None:
        """Process incoming messages from MediaFeeder."""
        try:
            self._logger.info("Started streaming RPC.")
            for rep in iterator:
                try:
                    self.on_ses_rep_msg(rep)
                except Exception:
                    self._logger.exception("failed to handle msg %s", rep)
        except Exception:
            self._logger.exception("failed to read stream")

    def on_ses_rep_msg(self, rep: Api_pb2.PlaybackSessionReply) -> None:
        """Process a single incoming message from MediaFeeder."""
        if rep.ShouldPlayPause:
            if not self.last_status:
                self._logger.error("can not play/pause without last_status.")
            elif self.last_status.player_is_paused:
                self.cast.media_controller.play()
                self._logger.info("playing")
            elif self.last_status.player_is_playing:
                self.cast.media_controller.pause()
                self._logger.info("paused")
            elif (self.last_status.player_is_idle or self.last_status.player_state == pychromecast.controllers.media.MEDIA_PLAYER_STATE_UNKNOWN) and rep.NextVideoId:
                self._logger.info("Player is idle so requesting video replay.")
                self.event_queue.put(QueueEvent(next_video_id=rep.NextVideoId))
            else:
                self._logger.warning("can not play/pause in state: %s", self.last_status.player_state)
            return  # so NextVideoId is not treated as skip to next video.

        if rep.ShouldSeekRelativeSeconds:
            if not self.last_status:
                self._logger.error("can not seek without last_status.")
            elif self.last_status.player_is_playing:
                self.cast.media_controller.seek(self.last_status.current_time + rep.ShouldSeekRelativeSeconds)

        elif rep.ShouldWatch:
            self._logger.info("Received mark as watched and skip command")
            self.event_queue.put(QueueEvent(go_next=True, mark_watched=True))

        elif rep.ShouldSkip:
            self._logger.info("Received skip command")
            self.event_queue.put(QueueEvent(go_next=True, mark_watched=False))

        elif rep.NextVideoId > 0:
            self._logger.info("Received next video ID: %s", rep.NextVideoId)
            self.event_queue.put(QueueEvent(next_video_id=rep.NextVideoId))

    def pause_if_playing(self):
        if self.last_status.player_is_playing:
            self.cast.media_controller.pause()
            self._logger.info("paused")


T = TypeVar("T")


class Player:
    """Main Player."""

    def message_queue_iterator(self, queue: Queue[T]) -> Iterator[T]:
        """Send queued messages to MediaFeeder.

        Yields:
            T: Items from queue

        """
        while True:
            yield queue.get()

    def subscribe_callback(self, connectivity: grpc.ChannelConnectivity) -> None:
        """Recieve connection status updates from GRPC."""
        self.logger.info("Channel: %s", connectivity)

    def connect_to_server(self) -> None:
        """Connect to MediaFeeder."""
        if self.session_reader:
            self.session_reader.cancel()

        self.logger.info("Connecting to server...")
        grpc.channel_ready_future(self.channel).result()

        session_iterator = self.stub.PlaybackSession(
            self.message_queue_iterator(self.status_message_queue),
        )
        self.session_reader = self.executor.submit(self.listener_media.on_ses_rep, session_iterator)
        self.send_player_title()

        # hack to work around grpc sometimes dropping early msgs.
        self.executor.submit(self.send_player_title_again)

    def send_player_title(self):
        self.status_message_queue.put(Api_pb2.PlaybackSessionRequest(Title=self.args.cast))

    def send_player_title_again(self):
        time.sleep(2)
        self.send_player_title()

    def update_cast(self) -> bool:
        """Update status from ChromeCast.

        Returns:
            bool: Update success

        """
        try:
            self.cast.media_controller.update_status()
        except pychromecast.error.NotConnected:
            self.logger.warning("Waiting for chromecast...")
            time.sleep(3)
            return False
        else:
            return True

    def main(self) -> None:
        """Main player setup."""
        # Enable deprecation warnings etc.
        if not sys.warnoptions:
            warnings.simplefilter("default")

        self.logger = logging.getLogger("Youtube_Shuffle")

        parser = argparse.ArgumentParser(
            description="Example on how to use the Youtube Controller.",
        )
        parser.add_argument(
            "--server",
            help="MediaFeeder server address and port",
            required=True,
        )
        parser.add_argument("--server-cert", help="Path to .pem for server TLS cert.")
        parser.add_argument("--cert-host", help="Server hostname to trust.")
        parser.add_argument("--cast", help="Name of cast device")
        parser.add_argument(
            "--known-host",
            help="Add known host (IP), can be used multiple times",
            action="append",
        )
        parser.add_argument("--token", help="Authentication token", required=True)
        self.args = parser.parse_args()

        chromecasts, browser = pychromecast.get_listed_chromecasts(
            friendly_names=[self.args.cast],
            known_hosts=self.args.known_host,
        )

        if not chromecasts:
            self.logger.error('No chromecast with name "%s" discovered', self.args.cast)
            sys.exit(1)

        self.cast = chromecasts[0]
        # Start socket client's worker thread and wait for initial status update
        self.cast.wait()

        ssl_credentials = grpc.ssl_channel_credentials()
        if self.args.server_cert:
            root_certs = Path(self.args.server_cert).read_bytes()
            ssl_credentials = grpc.ssl_channel_credentials(root_certificates=root_certs)

        bearer_credentials = grpc.access_token_call_credentials(self.args.token)
        composite_credentials = grpc.composite_channel_credentials(
            ssl_credentials,
            bearer_credentials,
        )

        channel_options = [
            ("grpc.keepalive_time_ms", 8000),
            ("grpc.keepalive_timeout_ms", 5000),
            ("grpc.http2.max_pings_without_data", 0),
            ("grpc.keepalive_permit_without_calls", 1),
        ]

        if self.args.cert_host:
            channel_options += [("grpc.ssl_target_name_override", self.args.cert_host)]

        self.channel = grpc.secure_channel(
            self.args.server,
            composite_credentials,
            options=channel_options,
        )

        self.channel.subscribe(self.subscribe_callback, try_to_connect=True)

        self.stub = Api_pb2_grpc.APIStub(self.channel)

        self.yt = YouTubeController()
        self.cast.register_handler(self.yt)

        self.executor = ThreadPoolExecutor()
        self.status_message_queue = Queue()

        self.listener_media = MyMediaStatusListener(self.cast.name, self.cast, self.status_message_queue)
        self.cast.media_controller.register_status_listener(self.listener_media)
        self.session_reader = None

        try:
            self.connect_to_server()

            self.loop()

        except KeyboardInterrupt:
            self.logger.info("Shuting down...")

        # Shut down discovery
        browser.stop_discovery()

        self.logger.info("fin.")
        self.status_message_queue.put(Api_pb2.PlaybackSessionRequest(EndSession=True))

        self.executor.shutdown(wait=True, cancel_futures=True)
        sys.exit(0)

    def loop(self) -> None:
        """Main event loop."""
        current_video_id = None
        current_content_id = None

        while True:
            if not self.session_reader or not self.session_reader.running():
                self.connect_to_server()

                # if connection was lost, at least restore what is currently playing.
                if current_video_id:
                    self.status_message_queue.put(
                        Api_pb2.PlaybackSessionRequest(
                            VideoId=current_video_id,
                            State=Api_pb2.UNKNOWN,  # reset any previous state
                        ),
                    )

            if current_video_id and not self.update_cast():
                continue

            event = self.listener_media.get_event(5)
            if event and event.next_video_id:
                current_video_id = event.next_video_id
                id_response = self.stub.Video(Api_pb2.VideoRequest(Id=current_video_id))
                current_content_id = id_response.VideoId

                self.logger.info(
                    "Playing %s: %s [%s]",
                    current_video_id,
                    id_response.Title,
                    current_content_id,
                )
                self.status_message_queue.put(
                    Api_pb2.PlaybackSessionRequest(VideoId=current_video_id),
                )

                # if the chromecast has been used for other apps, cast lib might incorrectly think it is already launched.
                # this makes sure it reconnects and is ready to receive play commands.  hopefully.
                self.yt.update_screen_id()
                self.yt.start_session_if_none()
                time.sleep(1)

                self.yt.clear_playlist()
                self.yt.play_video(current_content_id)

            elif event and event.go_next:
                # this check is to protected against the chromecast sending multiple finished events.
                if not event.content_id or event.content_id == current_content_id:
                    if current_video_id and event.mark_watched:
                        self.logger.info("Marking %s as watched...", current_video_id)
                        self.stub.Watched(Api_pb2.WatchedRequest(Id=current_video_id, Watched=True, ActuallyWatched=True))
                        self.listener_media.pause_if_playing()  # so playback stops if last video in queue

                    self.yt.clear_playlist()
                    current_video_id = None
                    current_content_id = None
                    self.logger.info("Requesting next video...")
                    self.status_message_queue.put(
                        Api_pb2.PlaybackSessionRequest(Action=Api_pb2.POP_NEXT_VIDEO),
                    )
                    time.sleep(1)  # let chromecast finish before trying to play next video.
                else:
                    self.logger.info("Ignoring event: %s", event)


Player().main()
