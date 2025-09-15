"""MediaFeeder Common client API."""

import abc
import asyncio
import logging
from collections.abc import Iterator
from pathlib import Path

import grpc
import grpc.aio

import Api_pb2
import Api_pb2_grpc
from auth import MediaFeederConfig


class StatusUpdate:
    """Properties for sending playback status updates back to MediaFeeder."""

    """MediaFeeder ID of the video currently being played"""
    VideoId: int | None = None

    """Current position of playback, in seconds"""
    Duration: int | None = None

    """Description of the quality of playback"""
    Quality: str | None = None

    """Which provider is playing the video"""
    Provider: str | None = None

    """Description of the state of playback"""
    State: str | None = None

    """Volume, 0-11 scale"""
    Volume: int | None = None

    """Rate of playback, 1 being 100%"""
    Rate: float | None = None

    """Percent of video loaded into buffer"""
    Loaded: float | None = None


class PlayerBase(abc.ABC):
    """Base class for player implementations."""

    @abc.abstractmethod
    async def play_video(self, video: Api_pb2.VideoReply) -> None:
        """Play a video immidiately."""

    @abc.abstractmethod
    async def play_pause(self) -> None:
        """Toggle the paused state."""


class _AuthGateway(grpc.AuthMetadataPlugin):
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
    _now_playing: int | None = None
    _mark_watched: bool
    _play_next: int | None = None
    _player: PlayerBase
    _session_reader = None
    _channel: grpc.aio.Channel = None

    def __init__(self, name: str, player: PlayerBase) -> None:
        """Prepare state only."""
        self._logger = logging.getLogger(f"Shuffler")
        self._logger.debug("Shuffler Init")

        self.name = name
        self._player = player
        self._settings = MediaFeederConfig()
        self._status_report_queue: asyncio.Queue[Api_pb2.PlaybackSessionRequest] = asyncio.Queue()

        ssl_credentials = grpc.ssl_channel_credentials()

        server_cert = self._settings.get_certificate_path()
        if server_cert is not None:
            with Path(server_cert).open("rb") as f:
                root_certs = f.read()
            ssl_credentials = grpc.ssl_channel_credentials(root_certificates=root_certs)

        bearer_credentials = grpc.metadata_call_credentials(_AuthGateway(self._settings))
        composite_credentials = grpc.composite_channel_credentials(ssl_credentials, bearer_credentials)

        channel_options = [
            ("grpc.keepalive_time_ms", 8000),
            ("grpc.keepalive_timeout_ms", 5000),
            ("grpc.http2.max_pings_without_data", 0),
            ("grpc.keepalive_permit_without_calls", 1),
        ]

        if server_cert is not None:
            channel_options += [("grpc.ssl_target_name_override", server_cert)]

        self._channel = grpc.aio.secure_channel(self._settings.get_server(), composite_credentials, options=channel_options)
        self._stub = Api_pb2_grpc.APIStub(self._channel)

    async def __aenter__(self) -> "Shuffler":
        await self._channel.__aenter__()
        return self

    async def __aexit__(self, *args: object) -> None:
        await self._channel.__aexit__(*args)

    async def _connect_to_server(self) -> None:
        """Connect to the MediaFeeder server."""
        self._logger.debug("Connect to server")
        if self._session_reader is not None:
            self._session_reader.cancel()

        self._logger.info("Connecting to server...")
        #channel_ready = grpc.channel_ready_future(self._channel)
        #channel_ready.add_done_callback(self._on_connected)
        self._logger.debug("Conneting")

        await self._channel.channel_ready()

        self._logger.info("Connected!")
        status_report_iterator = self._stub.PlaybackSession(self._status_report_queue_iterator())

        self._session_reader = asyncio.get_event_loop().create_task(self._on_ses_rep(status_report_iterator))

        await self._status_report_queue.put(Api_pb2.PlaybackSessionRequest(Title=self.name))

    async def _on_ses_rep(self, iterator: Iterator[Api_pb2.PlaybackSessionReply]) -> None:
        """Process incoming messages from MediaFeeder."""
        self._logger.debug("On Ses Rep")
        try:
            self._logger.info("Started streaming RPC.")
            async for rep in iterator:
                self._logger.info("Got message from MediaFeeder")
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

        elif rep.ShouldWatch:
            self._logger.info("Received mark as watched and skip command")
            self._mark_watched = True
            await self.finished()

        elif rep.ShouldSkip:
            self._logger.info("Received skip command")
            self._mark_watched = False
            await self.finished()

        elif rep.NextVideoId > 0:
            self._logger.info("Received next video ID: %s", rep.NextVideoId)
            self._play_next = rep.NextVideoId

    async def _status_report_queue_iterator(self) -> Iterator[Api_pb2.PlaybackSessionRequest]:
        self._logger.debug("Status Report Queue Iterator")
        while True:
            yield await self._status_report_queue.get()

    async def send_status(self, status: StatusUpdate) -> None:
        """Send a playback status update to MediaFeeder."""
        self._logger.debug("Send Status")
        status_message = Api_pb2.PlaybackSessionRequest()

        if status.VideoId is not None:
            status_message.VideoId = status.VideoId

        if status.Duration is not None:
            status_message.Duration = status.Duration

        if status.Quality is not None:
            status_message.Quality = status.Quality

        if status.Provider is not None:
            status_message.Provider = status.Provider

        if status.State is not None:
            status_message.State = status.State

        if status.Volume is not None:
            status_message.Volume = status.Volume

        if status.Rate is not None:
            status_message.Rate = status.Rate

        if status.Loaded is not None:
            status_message.Loaded = status.Loaded

        await self._status_report_queue.put(status_message)

    async def search(self, request: Api_pb2.SearchRequest) -> int | None:
        results = await self._stub.Search(request)
        if results.VideoId is not None and len(results.VideoId) > 0:
            return results.VideoId[0]
        return None

    async def finished(self) -> None:
        """Current video has finished playing."""
        self._logger.debug("Finished")
        if self._mark_watched and self._now_playing is not None:
            watched_request = Api_pb2.WatchedRequest(Id=self._now_playing, Watched=True)
            await self._stub.Watched(watched_request)

        await self._status_report_queue.put(Api_pb2.PlaybackSessionRequest(Action=Api_pb2.POP_NEXT_VIDEO))

        self._now_playing = None

    async def start(self) -> None:
        """Start playback, if available."""
        self._logger.debug("Start")
        while True:
            if self._session_reader is None or self._session_reader.done():
                self._logger.debug("Loop reconnecting...")
                await self._connect_to_server()

                # if connection was lost, at least restore what is currently playing.
                if self._now_playing:
                    await self._status_report_queue.put(
                        Api_pb2.PlaybackSessionRequest(VideoId=self._now_playing),
                    )
                self._logger.debug("Loop reconnected!")

            if self._now_playing is None and self._play_next is not None:
                self._logger.debug("Play next video")
                self._now_playing = self._play_next
                self._play_next = None

                id_response = await self._stub.Video(Api_pb2.VideoRequest(Id=self._now_playing))

                self._logger.info("Playing %s: %s [%s]", self._now_playing, id_response.Title, id_response.VideoId)
                await self._status_report_queue.put(Api_pb2.PlaybackSessionRequest(VideoId=self._now_playing))

                await self._player.play_video(id_response)

            await asyncio.sleep(1)


def set_logging() -> None:
    """Set up logging in a standardised mannor."""
    logging.basicConfig(
        level=logging.DEBUG,
        format="%(asctime)s [%(levelname)s] %(message)s",
        handlers=[
            logging.StreamHandler(),
        ],
    )
    logging.getLogger("asyncio").setLevel(logging.INFO)
