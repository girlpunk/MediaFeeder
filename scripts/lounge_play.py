#!/usr/bin/env python

"""Player for YouTube TV App's "Lounge" API."""

from __future__ import annotations

import asyncio
import logging
import sys
import urllib

import pyytlounge

import auth
import common


class LoungePlayer(pyytlounge.EventListener):
    """Player for Youtube TV App's 'Lounge' API."""

    _api: pyytlounge.YtLoungeApi
    _last_status: pyytlounge.PlaybackStateEvent | None

    def __init__(self, name: str) -> None:
        """Set up variables, but doesn't connect yet."""
        self._logger = logging.getLogger(f"LoungePlayer: {name}")
        self._logger.debug("LoungePlayer Init")

        self._api = pyytlounge.YtLoungeApi("MediaFeeder", self)

        config = auth.MediaFeederConfig()
        self._api.load_auth_state(config.get_player(name))

    async def __aenter__(self) -> LoungePlayer:
        """Connect to Lounge.

        Returns:
            LoungePlayer

        """
        self._logger.debug("AEnter")
        await self._api.__aenter__()
        return self

    async def __aexit__(self, *args: object) -> None:
        """Disconnect from Lounge."""
        self._logger.debug("AExit")
        await self._api.__aexit__(*args)

    async def play_video(self, video: str) -> None:
        """Play a video immidiately."""
        self._logger.debug("Play Video")
        await self._api.play_video(video)

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

    async def _request_updates(self) -> None:
        self._logger.debug("Request Updates")
        while True:
            await asyncio.sleep(60)
            self._logger.info("Requesting update: %s", await self._api.get_now_playing())

    async def _subscribe_events(self) -> None:
        self._logger.debug("Subscribe Events")
        while True:
            self._logger.info("Trying to link: %s", await self._api.refresh_auth())
            self._logger.info("Trying to connect: %s", await self._api.connect())

            self._logger.warning("Subscribing to events")
            await self._api.subscribe()

    async def now_playing_changed(self, event: pyytlounge.NowPlayingEvent) -> None:
        """Process when active video changes."""
        self._logger.debug("Now Playing Changed")
        self._logger.info(("Now Playing:\n    %s\n    ID: %s\n    pos: %s\n    duration: %s"), event.state, event.video_id, event.current_time, event.duration)

    async def ad_playing_changed(self, event: pyytlounge.AdPlayingEvent) -> None:
        """Process when ad starts playing."""
        self._logger.debug("Ad Playing Changed")
        self._logger.info(
            (
                "Ad Playing:\n"
                "    AD Video ID: %s\n"
                "    AD Video URI: %s\n"
                "    AD Title: %s\n"
                "    Is Bumper: %s\n"
                "    Is Skippable: %s\n"
                "    Is Skip Enabled: %s\n"
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
        """Process when ad state changes (position, play/pause, skippable)."""
        self._logger.debug("Ad State Changed")
        self._logger.info(("Ad State:\n    AD State: %s\n    Current Time: %s\n    Is Skip Enabled: %s"), event.ad_state, event.current_time, event.is_skip_enabled)

    async def autoplay_changed(self, event: pyytlounge.AutoplayModeChangedEvent) -> None:
        """Process when auto play mode changes."""
        self._logger.debug("Autoplay Changed")
        self._logger.info(("Autoplay Changed:\n    Enabled: %s\n    Supported: %s"), event.enabled, event.supported)

    async def autoplay_up_next_changed(self, event: pyytlounge.AutoplayUpNextEvent) -> None:
        """Process when up next video changes."""
        self._logger.debug("Autoplay Up Next Changed")
        self._logger.info(("Autoplay Up Next Changed:\n    Video ID: %s"), event.video_id)

    async def disconnected(self, event: pyytlounge.DisconnectedEvent) -> None:
        """Process when the screen is no longer connected."""
        self._logger.debug("Disconnected")
        self._logger.info(("Disconnected:\n    Reason: %s"), event.reason)

    async def playback_speed_changed(self, event: pyytlounge.PlaybackSpeedEvent) -> None:
        """Process when playback speed changes."""
        self._logger.debug("Playback Speed Changed")
        self._logger.info(("Playback Speed Changed:\n    Playback Speed: %s"), event.playback_speed)

    async def playback_state_changed(self, event: pyytlounge.PlaybackStateEvent) -> None:
        """Process when playback state changes (position, play/pause)."""
        self._logger.debug("Playback State Changed")
        self._logger.info(("Playback State Changed:    Current Time: %s\n    Duration: %s\n    State: %s"), event.current_time, event.duration, event.state)

    async def subtitles_track_changed(self, event: pyytlounge.SubtitlesTrackEvent) -> None:
        """Process when subtitles track changes."""
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
        """Process when volume or muted state changes."""
        self._logger.debug("Volume Changed")
        self._logger.info(("Volume Changed:\n    Volume: %s\n    Muted: %s"), event.volume, event.muted)


async def _main() -> None:
    """Begin main entrypoint, start playback."""
    common.set_logging()
    async with LoungePlayer(sys.argv[1]) as player:
        await player.main()

        ids: list[str] = []

        for video_id in sys.argv[2].split(","):
            try:
                ids.append(urllib.parse.parse_qs(urllib.parse.urlparse(video_id).query)["v"])
            except KeyError:
                ids.append(video_id)

        await player.play_video(ids.pop(0))

        await asyncio.sleep(1560)

        if len(ids) > 0:
            for video_id in ids:
                input("Press enter to start next video")
                await player.play_video(video_id)


if __name__ == "__main__":
    asyncio.run(_main(), debug=True)
