#!/usr/bin/env python

"""Player for YouTube TV App's "Lounge" API."""

import asyncio
import logging
import sys

import pyytlounge

import Api_pb2
import auth
import common


class LoungePlayer(pyytlounge.EventListener, common.PlayerBase):
    """Player for Youtube TV App's 'Lounge' API."""

    _api: pyytlounge.YtLoungeApi
    _shuffler: common.Shuffler
    _last_status: pyytlounge.PlaybackStateEvent | None = None
    _now_playing: str | None = None

    def __init__(self, name: str) -> None:
        """Set up variables, but doesn't connect yet."""
        self._logger = logging.getLogger(f"LoungePlayer: {name}")
        self._logger.debug("LoungePlayer Init")

        self._api = pyytlounge.YtLoungeApi("MediaFeeder", self)
        self._shuffler = common.Shuffler(name, self)

        config = auth.MediaFeederConfig()
        self._api.load_auth_state(config.get_player(name))

    async def __aenter__(self) -> "LoungePlayer":
        """Connect to Lounge.

        Returns:
            LoungePlayer

        """
        self._logger.debug("AEnter")
        await self._api.__aenter__()
        await self._shuffler.__aenter__()
        return self

    async def __aexit__(self, *args: object) -> None:
        """Disconnect from Lounge."""
        self._logger.debug("AExit")
        await self._shuffler.__aexit__(*args)
        await self._api.__aexit__(*args)

    async def play_video(self, video: Api_pb2.VideoReply) -> None:
        """Play a video immidiately."""
        self._logger.debug("Play Video")
        self._now_playing = video.VideoId
        await self._api.play_video(video.VideoId)

    async def play_pause(self) -> None:
        """Toggle the paused state."""
        self._logger.debug("Play Pause")
        if self._last_status is None:
            self._logger.error("can not play/pause without last_status.")
            return

        if self._last_status.state == pyytlounge.State.Paused:
            await self._api.play()
        elif self._last_status.state == pyytlounge.State.Playing:
            await self._api.pause()
        else:
            self._logger.warning("Don't know how to play/pause from state %s", self._last_status.state)

    async def main(self) -> None:
        """Connect to the Lounge API."""
        self._logger.debug("Main")
        self._logger.info("Trying to link: %s", await self._api.refresh_auth())
        self._logger.info("Trying to connect: %s", await self._api.connect())

        asyncio.get_event_loop().create_task(self._subscribe_events())
        asyncio.get_event_loop().create_task(self._request_updates())

        await self._shuffler.start()

    async def _request_updates(self) -> None:
        self._logger.debug("Request Updates")
        while True:
            await asyncio.sleep(10)
            self._logger.info("Requesting update: %s", await self._api.get_now_playing())

    async def _subscribe_events(self) -> None:
        self._logger.debug("Subscribe Events")
        while True:
            await asyncio.sleep(10)
            self._logger.warning("Subscribing to events: %s", await self._api.subscribe())

    async def now_playing_changed(self, event: pyytlounge.NowPlayingEvent) -> None:
        self._logger.debug("Now Playing Changed")
        """Called when active video changes."""
        self._logger.info(("Now Playing:\n    %s\n    ID: %s\n    pos: %s\n    duration: %s"), event.state, event.video_id, event.current_time, event.duration)

        update = common.StatusUpdate()

        if event.video_id is not None and event.video_id != self._now_playing:
            self._now_playing = event.video_id
            server_video = await self._shuffler.search(Api_pb2.SearchRequest(Provider="YouTube", ProviderVideoId=self._now_playing))
            if server_video is not None:
                update.VideoId = server_video

        if event.current_time is not None:
            update.Duration = int(event.current_time)
        update.Provider = "YouTube"

        await self._shuffler.send_status(update)

    async def ad_playing_changed(self, event: pyytlounge.AdPlayingEvent) -> None:
        """Called when ad starts playing."""
        self._logger.debug("Ad Playing Changed")
        self._logger.info(
            (
                "Ad Playing:\n"
                "    AD Video ID: %s\n"
                "    AD Video URI: %s\n"
                "    AD Title: %s\n"
                "    Is Bumper: %s\n"
                "    Is Skippable: %s\n"
                "    Is Click Enabled: %s\n"
                "    Click Through URL: %s\n"
                "    AD System: %s\n"
                "    AD Next Params: %s\n"
                "    Remote Slots Data: %s\n"
                "    AD State: %s\n"
                "    Content Video ID: %s\n"
                "    Duration: %s\n"
                "    Current Time: %s"
            ),
            event.ad_video_id,
            event.ad_video_uri,
            event.ad_title,
            event.is_bumper,
            event.is_skippable,
            event.is_skip_enabled,
            event.click_through_url,
            event.ad_system,
            event.ad_next_params,
            event.remote_slots_data,
            event.ad_state,
            event.content_video_id,
            event.duration,
            event.current_time,
        )

    async def ad_state_changed(self, event: pyytlounge.AdStateEvent) -> None:
        """Called when ad state changes (position, play/pause, skippable)."""
        self._logger.debug("Ad State Changed")
        self._logger.info(("Ad State:\n    AD State: %s\n    Current Time: %s\n    Is Skip Enabled: %s"), event.ad_state, event.current_time, event.is_skip_enabled)

    async def autoplay_changed(self, event: pyytlounge.AutoplayModeChangedEvent) -> None:
        """Called when auto play mode changes."""
        self._logger.debug("Autoplay Changed")
        self._logger.info(("Autoplay Changed:\n    Enabled: %s\n    Supported: %s"), event.enabled, event.supported)
        if event.enabled:
            asyncio.get_event_loop().create_task(self._disable_autoplay())

    async def _disable_autoplay(self) -> None:
        """Disable autoplay in the app."""
        await asyncio.sleep(10)
        await self._api._command(
            "setAutoplayMode",
            {"autoplayMode": "DISABLED"},
        )

    async def autoplay_up_next_changed(self, event: pyytlounge.AutoplayUpNextEvent) -> None:
        """Called when up next video changes."""
        self._logger.debug("Autoplay Up Next Changed")
        self._logger.info(("Autoplay Up Next Changed:\n    Video ID: %s"), event.video_id)

    async def disconnected(self, event: pyytlounge.DisconnectedEvent) -> None:
        """Called when the screen is no longer connected."""
        self._logger.debug("Disconnected")
        self._logger.info(("Disconnected:\n    Reason: %s"), event.reason)

    async def playback_speed_changed(self, event: pyytlounge.PlaybackSpeedEvent) -> None:
        """Called when playback speed changes."""
        self._logger.debug("Playback Speed Changed")
        self._logger.info(("Playback Speed Changed:\n    Playback Speed: %s"), event.playback_speed)

        update = common.StatusUpdate()
        update.Rate = event.playback_speed
        await self._shuffler.send_status(update)

    async def playback_state_changed(self, event: pyytlounge.PlaybackStateEvent) -> None:
        """Called when playback state changes (position, play/pause)."""
        self._logger.debug("Playback State Changed")
        self._logger.info(("Playback State Changed:    Current Time: %s\n    Duration: %s\n    State: %s"), event.current_time, event.duration, event.state)

        self._last_status = event

        update = common.StatusUpdate()
        update.State = str(event.state)

        if event.current_time is not None:
            update.Duration = int(event.current_time)

        await self._shuffler.send_status(update)

        if event.state == pyytlounge.State.Stopped and self._now_playing is not None:
            self._now_playing = None
            await self._shuffler.finished()

    async def subtitles_track_changed(self, event: pyytlounge.SubtitlesTrackEvent) -> None:
        """Called when subtitles track changes."""
        self._logger.debug("Subtitles Track Changed")
        self._logger.info(
            (
                "Subtitles Track Changed:\n"
                "    Video ID: %s\n"
                "    Track Name: %s\n"
                "    Language Code: %s\n"
                "    Source Language Code: %s\n"
                "    Language Name: %s\n"
                "    Kind: %s\n"
                "    Vss ID: %s\n"
                "    Caption ID: %s\n"
                "    Style: %s"
            ),
            event.video_id,
            event.track_name,
            event.language_code,
            event.source_language_code,
            event.language_name,
            event.kind,
            event.vss_id,
            event.caption_id,
            event.style,
        )

    async def volume_changed(self, event: pyytlounge.VolumeChangedEvent) -> None:
        """Called when volume or muted state changes."""
        self._logger.debug("Volume Changed")
        self._logger.info(("Volume Changed:\n    Volume: %s\n    Muted: %s"), event.volume, event.muted)

        update = common.StatusUpdate()
        update.Volume = event.volume
        await self._shuffler.send_status(update)


async def _main() -> None:
    """Main entrypoint, start playback."""
    common.set_logging()
    async with LoungePlayer(sys.argv[1]) as player:
        await player.main()


if __name__ == "__main__":
    asyncio.run(_main(), debug=True)
