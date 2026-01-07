"""MediaFeeder Common client API."""
# vim: tw=0 ts=4 sw=4

from __future__ import annotations

import abc
import asyncio
import logging
import time
from collections.abc import AsyncGenerator
from cryptography import x509
from cryptography.x509.oid import NameOID
from datetime import datetime
from pathlib import Path
from typing import NamedTuple

import grpc
import grpc.aio

import Api_pb2
import Api_pb2_grpc
from auth import MediaFeederConfig

MINIMUM_SAVE_FREQUENCY = 60


class StatusUpdate:
    """Properties for sending playback status updates back to MediaFeeder."""

    """MediaFeeder ID of the video currently being played"""
    VideoId: int | None = None

    """Current position of playback, in seconds"""
    Position: int | None = None

    """Description of the quality of playback"""
    Quality: str | None = None

    """Which provider is playing the video"""
    Provider: str | None = None

    """Player state, using Api.proto PlayerState enum values"""
    State: int | None = None

    """Volume, 0-11 scale"""
    Volume: int | None = None
    SupportsVolumeChange: bool | None = None

    """Rate of playback, 1 being 100%"""
    Rate: float | None = None
    SupportsRateChange: bool | None = None

    """Percent of video loaded into buffer"""
    Loaded: float | None = None


class QueueEvent(NamedTuple):
    """Internal queue events."""

    next_video_id: int | None = None
    restore_position_seconds: int | None = None
    watched_to_end: bool = False
    video_id: int | None = None
    abandoned: bool = False


class PlayerBase(abc.ABC):
    """Base class for player implementations."""

    @abc.abstractmethod
    async def ensure_ready(self) -> None:
        """Check if player is ready and set up if needed."""

    @abc.abstractmethod
    async def play_video(self, video: Api_pb2.VideoReply) -> None:
        """Play a video immidiately."""

    @abc.abstractmethod
    async def play_pause(self) -> None:
        """Toggle the paused state."""

    @abc.abstractmethod
    async def pause_if_playing(self) -> None:
        """If playing, then pause."""

    @abc.abstractmethod
    async def seek(self, position_seconds: int) -> None:
        """Seek player to this position."""

    @abc.abstractmethod
    async def change_volume(self, direction: int) -> None:
        """Change volume up or down if direction is positive or negative."""

    @abc.abstractmethod
    async def change_playback_rate(self, direction: int) -> None:
        """Increase/decrease playback rate if direction is positive or negative."""


class AuthGateway(grpc.AuthMetadataPlugin):
    """Metadata pluging to add MediaFeeder authentication tokens."""

    def __init__(self, settings: MediaFeederConfig) -> None:
        """Store settings."""
        self._settings = settings

    def __call__(
        self,
        _: grpc.AuthMetadataContext,
        callback: grpc.AuthMetadataPluginCallback,
    ) -> None:
        """Add a token to the request."""
        metadata = (("authorization", f"Bearer {self._settings.get_server_token()}"),)
        callback(metadata, None)


class Shuffler:
    """MediaFeeder API Connection."""

    name: str
    _stub: Api_pb2_grpc.APIStub
    _settings: MediaFeederConfig

    _now_state: int | None = None
    _now_position_seconds: int | None = None

    _event_queue: asyncio.Queue[QueueEvent] = asyncio.Queue()

    _player: PlayerBase
    _session_reader = None

    def __init__(self, name: str, player: PlayerBase, *, verbose: bool) -> None:
        """Prepare state only."""
        self._logger = logging.getLogger("Shuffler")
        if verbose:
            self._logger.setLevel(logging.DEBUG)
        self._logger.debug("Shuffler Init")

        self.name = name
        self._player = player
        self._settings = MediaFeederConfig()
        self._status_report_queue: asyncio.Queue[Api_pb2.PlaybackSessionRequest] = asyncio.Queue()

        ssl_credentials = grpc.ssl_channel_credentials()

        server_cert = self._settings.get_certificate_path()
        cert_cn = None
        if server_cert is not None:
            self._logger.info("Using server cert: %s", server_cert)
            with Path(server_cert).open("rb") as f:
                root_certs = f.read()
            ssl_credentials = grpc.ssl_channel_credentials(root_certificates=root_certs)

            cert_info = x509.load_pem_x509_certificate(root_certs)
            cert_cn = str(cert_info.issuer.get_attributes_for_oid(NameOID.COMMON_NAME)[0].value)

        bearer_credentials = grpc.metadata_call_credentials(AuthGateway(self._settings))
        composite_credentials = grpc.composite_channel_credentials(ssl_credentials, bearer_credentials)

        channel_options: list[tuple[str, str | int]] = [
            ("grpc.keepalive_time_ms", 8000),
            ("grpc.keepalive_timeout_ms", 5000),
            ("grpc.http2.max_pings_without_data", 0),
            ("grpc.keepalive_permit_without_calls", 1),
        ]

        if cert_cn is not None:
            self._logger.info("Using cert CN: %s", cert_cn)
            channel_options += [("grpc.ssl_target_name_override", cert_cn)]

        self._channel: grpc.aio.Channel = grpc.aio.secure_channel(self._settings.get_server(), composite_credentials, options=channel_options)
        self._stub = Api_pb2_grpc.APIStub(self._channel)

    async def __aenter__(self) -> Shuffler:
        """Start processing playback."""
        await self._channel.__aenter__()
        return self

    async def __aexit__(self, *args: object) -> None:
        """Finish processing playback."""
        await self._channel.__aexit__(*args)

    async def _connect_to_server(self) -> None:
        """Connect to the MediaFeeder server."""
        self._logger.debug("Connect to server")
        if self._session_reader is not None:
            self._session_reader.cancel()

        self._logger.info("Connecting to server...")
        await self._channel.channel_ready()
        status_report_iterator = self._stub.PlaybackSession(self._status_report_queue_iterator())
        self._session_reader = asyncio.get_event_loop().create_task(self._on_ses_rep(status_report_iterator))

        await self._status_report_queue.put(Api_pb2.PlaybackSessionRequest(Title=self.name))
        self._logger.info("Server connected.")

    async def _on_ses_rep(self, iterator: AsyncGenerator[Api_pb2.PlaybackSessionReply]) -> None:
        """Process incoming messages from MediaFeeder."""
        self._logger.debug("On Ses Rep")
        try:
            self._logger.info("Started streaming RPC.")
            async for rep in iterator:
                self._logger.debug("Got message from MediaFeeder")
                try:
                    asyncio.get_event_loop().create_task(self._on_ses_rep_msg(rep))
                except Exception:
                    self._logger.exception("failed to handle msg %s", rep)
        except Exception:
            self._logger.exception("failed to read stream")

    async def _on_ses_rep_msg(self, rep: Api_pb2.PlaybackSessionReply) -> None:
        """Process message from MediaFeeder."""
        self._logger.debug("On Ses Rep Msg")
        if rep.ShouldPlayPause:
            await self._player.play_pause()

        elif rep.ShouldPauseIfPlaying:
            await self._player.pause_if_playing()

        elif rep.ShouldSeekRelativeSeconds:
            await self._player.seek(self._now_position_seconds + rep.ShouldSeekRelativeSeconds)

        elif rep.NextVideoId > 0:
            self._logger.info("Received next video ID: %s from %s seconds", rep.NextVideoId, rep.PlaybackPosition)
            self._event_queue.put_nowait(QueueEvent(next_video_id=rep.NextVideoId, restore_position_seconds=rep.PlaybackPosition))

        elif rep.ShouldChangeVolume:
            self._logger.info("Received change volume command: %s", rep.ShouldChangeVolume)
            await self._player.change_volume(rep.ShouldChangeVolume)

        elif rep.ShouldChangeRate:
            self._logger.info("Received change rate command: %s", rep.ShouldChangeRate)
            await self._player.change_playback_rate(rep.ShouldChangeRate)

    async def _status_report_queue_iterator(self) -> AsyncGenerator[Api_pb2.PlaybackSessionRequest]:
        self._logger.debug("Status Report Queue Iterator")
        while True:
            yield await self._status_report_queue.get()

    async def send_status(self, status: StatusUpdate) -> None:
        """Send a playback status update to MediaFeeder."""
        status_message = Api_pb2.PlaybackSessionRequest()

        if status.VideoId is not None:
            status_message.VideoId = status.VideoId

        if status.State is not None:
            status_message.State = status.State
            self._now_state = status.State

        if status.Position is not None:
            status_message.Position = status.Position
            self._now_position_seconds = status.Position
        elif status.State not in [Api_pb2.PLAYING, Api_pb2.PAUSED]:
            self._now_position_seconds = None

        if status.Quality is not None:
            status_message.Quality = status.Quality

        if status.Provider is not None:
            status_message.Provider = status.Provider

        if status.Volume is not None:
            status_message.Volume = status.Volume
        if status.SupportsVolumeChange is not None:
            status_message.SupportsVolumeChange = status.SupportsVolumeChange

        if status.Rate is not None:
            status_message.Rate = status.Rate
        if status.SupportsRateChange is not None:
            status_message.SupportsRateChange = status.SupportsRateChange

        if status.Loaded is not None:
            status_message.Loaded = status.Loaded

        await self._status_report_queue.put(status_message)

    async def finished(self, video_id: int) -> None:
        """Process that current video has finished playing."""
        self._logger.debug("Finished playing: video_id=%s", video_id)
        self._event_queue.put_nowait(QueueEvent(watched_to_end=True, video_id=video_id))

    async def playback_abandoned(self) -> None:
        """Process that current video has been abandoned."""
        self._logger.debug("Playback abandoned.")
        self._event_queue.put_nowait(QueueEvent(abandoned=True))

    async def get_event(self, timeout: int) -> QueueEvent | None:
        """Get next event in queue."""
        try:
            item = await asyncio.wait_for(self._event_queue.get(), timeout=timeout)
            self._event_queue.task_done()  # dont care about work tracking.
        except TimeoutError:
            return None
        else:
            return item

    async def start(self) -> None:
        """Start playback, if available."""
        self._logger.debug("Start")

        current_video_id = None
        last_save_position_time = time.monotonic()
        position_to_restore_seconds = None

        while True:
            if self._session_reader is None or self._session_reader.done():
                self._logger.debug("Loop reconnecting...")
                await self._connect_to_server()

                # if connection was lost, at least restore what is currently playing.
                if current_video_id:
                    await self._status_report_queue.put(Api_pb2.PlaybackSessionRequest(VideoId=current_video_id))
                self._logger.debug("Loop reconnected.")

            if current_video_id:
                await self._player.ensure_ready()

            event = await self.get_event(5)

            if event and event.next_video_id:
                current_video_id = event.next_video_id
                id_response = await self._stub.Video(Api_pb2.VideoRequest(Id=current_video_id))

                self._logger.info("Playing %s: %s [%s]", current_video_id, id_response.Title, id_response.VideoId)
                await self._status_report_queue.put(Api_pb2.PlaybackSessionRequest(VideoId=current_video_id))

                self._last_save_position_time = time.monotonic()  # do not save position immediately.
                await self._player.play_video(id_response)
                position_to_restore_seconds = event.restore_position_seconds

            elif event and event.watched_to_end:
                if event.video_id == current_video_id:
                    self._logger.info("Notifiting server video %s was watched...", current_video_id)
                    await self._status_report_queue.put(Api_pb2.PlaybackSessionRequest(Action=Api_pb2.ON_WATCHED_TO_END, VideoId=current_video_id))

                    current_video_id = None
                else:
                    self._logger.warning("Mismatched watched_to_end event: expected=%s  got=%s", current_video_id, event.video_id)

            elif event and event.abandoned:
                self._logger.info("Playback abandoned.")
                current_video_id = None

            elif current_video_id and self._now_state in [Api_pb2.PLAYING, Api_pb2.PAUSED] and self._now_position_seconds and self._now_position_seconds > 0:
                if position_to_restore_seconds and position_to_restore_seconds > 0:
                    self._logger.info("Seeking to restore playback position: %s ...", position_to_restore_seconds)
                    await self._player.seek(position_to_restore_seconds)
                    position_to_restore_seconds = None
                    last_save_position_time = time.monotonic()  # do not re-save position immediately.

                elif time.monotonic() - last_save_position_time > MINIMUM_SAVE_FREQUENCY:
                    self._stub.SavePlaybackPosition(Api_pb2.SavePlaybackPositionRequest(Id=current_video_id, PositionSeconds=self._now_position_seconds))
                    last_save_position_time = time.monotonic()
                    self._logger.debug("Saved playback position: %s", self._now_position_seconds)


class LogbackLikeFormatter(logging.Formatter):
    def formatTime(self, record, datefmt=None):
        dt = datetime.fromtimestamp(record.created)
        return dt.strftime("%m%d %H:%M:%S.") + f"{int(record.msecs):03d}"

    def format(self, record):
        record.levelname = record.levelname[:1]
        record.threadName = record.threadName[-10:].rjust(10)
        record.name = record.name[-15:].rjust(15)
        return super().format(record)


def set_logging() -> None:
    """Set up logging in a standardised mannor."""
    formatter = LogbackLikeFormatter("%(levelname)s%(asctime)s [%(threadName)s] %(name)s %(message)s")
    handler = logging.StreamHandler()
    handler.setFormatter(formatter)

    logger = logging.getLogger()
    logger.setLevel(logging.INFO)
    logger.addHandler(handler)

    logging.getLogger("asyncio").setLevel(logging.INFO)
