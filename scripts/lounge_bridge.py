#!/usr/bin/env python
"""Player for YouTube TV App's "Lounge" API."""
# vim: tw=0 ts=4 sw=4

from __future__ import annotations

import argparse
import asyncio
import logging

import pyytlounge
from typing_extensions import Self

import Api_pb2
import auth
import common

# TODO
# - set volume
# - set rate
# - set subtitles


class LoungePlayer(pyytlounge.EventListener, common.PlayerBase):
    """Player for Youtube TV App's 'Lounge' API."""

    _api: pyytlounge.YtLoungeApi
    _shuffler: common.Shuffler
    _current_player_state: pyytlounge.State | None = None

    _prov_id_to_video_id = {}

    _now_provider_id: str | None = None
    _now_duration: float | None = None

    _prev_provider_id: str | None = None
    _prev_duration: float | None = None

    def __init__(self, name: str, *, verbose: bool) -> None:
        """Set up variables, but doesn't connect yet."""
        self._logger = logging.getLogger(f"LoungePlayer: {name}")
        if verbose:
            self._logger.setLevel(logging.DEBUG)
        self._logger.debug("LoungePlayer Init")

        self._api = pyytlounge.YtLoungeApi("MediaFeeder", self)
        self._shuffler = common.Shuffler(name, self, verbose=verbose)

        config = auth.MediaFeederConfig()
        self._api.load_auth_state(config.get_player(name))

    async def __aenter__(self) -> Self:
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

    # PlayerBase
    async def ensure_ready(self) -> None:
        pass

    # PlayerBase
    async def play_video(self, video: Api_pb2.VideoReply) -> None:
        """Play a video immidiately."""
        self._logger.debug("Play Video")
        self._prov_id_to_video_id[video.VideoId] = video.Id

        await self._api._command("setPlaylist", {"videoId": video.VideoId})

    # PlayerBase
    async def play_pause(self, resume_video_id: int | None, resume_from_position: int | None) -> None:
        """Toggle the paused state."""
        self._logger.debug("Play Pause")

        if self._current_player_state == pyytlounge.State.Paused:
            await self._api.play()
        elif self._current_player_state == pyytlounge.State.Playing:
            await self._api.pause()
        elif self._current_player_state == pyytlounge.State.Starting and resume_video_id:
            self._logger.info("Player is idle so requesting replay of %s from %s seconds.", resume_video_id, resume_from_position)
            await self._shuffler.play_video(resume_video_id, resume_from_position)
        else:
            self._logger.warning("Don't know how to play/pause from state %s", self._current_player_state)

    # PlayerBase
    async def pause_if_playing(self) -> None:
        """Pause, but only if in playing state."""
        if self._current_player_state == pyytlounge.State.Playing:
            await self._api.pause()

    # PlayerBase
    async def seek(self, position_seconds: int) -> None:
        """Seek to a position in the video."""
        await self._api.seek_to(position_seconds)

    # PlayerBase
    async def change_volume(self, direction: int) -> None:
        self._logger.info("TODO: impl volume change")

    # PlayerBase
    async def change_playback_rate(self, rate: float) -> None:
        self._logger.info("TODO: impl rate change")

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
            self._logger.debug("Requesting update: %s", await self._api.get_now_playing())

    async def _subscribe_events(self) -> None:
        self._logger.debug("Subscribe Events")
        while True:
            await asyncio.sleep(10)
            self._logger.warning("Subscribing to events")
            await self._api.subscribe()

    async def now_playing_changed(self, event: pyytlounge.NowPlayingEvent) -> None:
        """Process active video change."""
        self._logger.debug(("Now Playing:   %s   provider_id: %s   pos: %s   duration: %s"), event.state, event.video_id, event.current_time, event.duration)

        self._current_player_state = event.state

        update = common.StatusUpdate()
        update.State = self._lounge_state_to_mf_state(event.state)

        if event.state == pyytlounge.State.Advertisement:
            await self._shuffler.send_status(update)
            return

        if event.video_id:
            if event.video_id != self._now_provider_id:
                self._prev_provider_id = self._now_provider_id
                self._prev_duration = self._now_duration
                self._logger.debug("self._now_provider_id=%s  prev_provider_id=%s  prev_duration=%s", event.video_id, self._prev_provider_id, self._prev_duration)
            self._now_provider_id = event.video_id

        if event.duration:
            self._now_duration = event.duration

        if event.current_time is not None:
            update.Position = int(event.current_time)
        update.Provider = "Youtube"

        await self._shuffler.send_status(update)

        if event.current_time is None and event.state == pyytlounge.State.Starting and event.video_id == self._now_provider_id and self._now_provider_id is not None and self._now_duration is not None:
            self._logger.debug("Assuming video finished: now_provider_id=%s  now_duration=%s", self._now_provider_id, self._now_duration)
            await self._shuffler.finished(self._prov_id_to_video_id[self._now_provider_id])
            self._now_provider_id = None
            self._now_duration = None
            self._prev_provider_id = None
            self._prev_duration = None


    async def ad_playing_changed(self, event: pyytlounge.AdPlayingEvent) -> None:
        """Process an ad playing."""
        self._logger.debug(
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
        """Process an ad state change (position, play/pause, skippable)."""
        self._logger.debug(("Ad State:   AD State: %s   Current Time: %s   Is Skip Enabled: %s"), event.ad_state, event.current_time, event.is_skip_enabled)

    async def autoplay_changed(self, event: pyytlounge.AutoplayModeChangedEvent) -> None:
        """Process auto play mode changing."""
        self._logger.debug(("Autoplay Changed:   Enabled: %s    Supported: %s"), event.enabled, event.supported)
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
        """Process up next video changing."""
        self._logger.debug(("Autoplay Up Next Changed:   Video ID: %s"), event.video_id)

    async def disconnected(self, event: pyytlounge.DisconnectedEvent) -> None:
        """Process when the screen is no longer connected."""
        self._logger.info(("Disconnected:   Reason: %s"), event.reason)

    async def playback_speed_changed(self, event: pyytlounge.PlaybackSpeedEvent) -> None:
        """Process when playback speed changes."""
        self._logger.debug(("Playback Speed Changed:   Playback Speed: %s"), event.playback_speed)

        update = common.StatusUpdate()
        update.Rate = event.playback_speed
        await self._shuffler.send_status(update)

    async def playback_state_changed(self, event: pyytlounge.PlaybackStateEvent) -> None:
        """Process when playback state changes (position, play/pause)."""
        self._logger.debug(("Playback State Changed:   Current Time: %s    Duration: %s    State: %s"), event.current_time, event.duration, event.state)

        self._current_player_state = event.state

        update = common.StatusUpdate()
        update.State = self._lounge_state_to_mf_state(event.state)

        if event.state == pyytlounge.State.Advertisement:
            await self._shuffler.send_status(update)
            return

        if event.current_time is not None:
            update.Position = int(event.current_time)

        await self._shuffler.send_status(update)

        if self.is_close(event.current_time, event.duration):
            if self._prev_provider_id and self._prev_duration is not None and self.is_close(self._prev_duration, event.duration):
                self._logger.debug("Assuming prev video finished: prev_provider_id=%s  prev_duration=%s", self._prev_provider_id, self._prev_duration)
                await self._shuffler.finished(self._prov_id_to_video_id[self._prev_provider_id])
                self._prev_provider_id = None
                self._prev_duration = None
            elif self._now_provider_id and self._now_duration is not None and self.is_close(self._now_duration, event.duration):
                self._logger.debug("Assuming now video finished: now_provider_id=%s  now_duration=%s", self._now_provider_id, self._now_duration)
                await self._shuffler.finished(self._prov_id_to_video_id[self._now_provider_id])
                self._now_provider_id = None
                self._now_duration = None
                self._prev_provider_id = None
                self._prev_duration = None

    def is_close(self, a: float, b: float, epsilon: float = 0.01) -> bool:
        """Check if two numbers are approximately close."""
        if a is None or b is None:
            return False
        return abs(a - b) < epsilon

    def _lounge_state_to_mf_state(self, state: pyytlounge.State) -> int:
        # https://github.com/FabioGNR/pyytlounge/blob/master/src/pyytlounge/models.py
        return {
            pyytlounge.State.Stopped: Api_pb2.IDLE,
            pyytlounge.State.Buffering: Api_pb2.LOADING,
            pyytlounge.State.Playing: Api_pb2.PLAYING,
            pyytlounge.State.Paused: Api_pb2.PAUSED,
            pyytlounge.State.Starting: Api_pb2.IDLE,
            pyytlounge.State.Advertisement: Api_pb2.ADVERT,
        }[state]

    async def subtitles_track_changed(self, event: pyytlounge.SubtitlesTrackEvent) -> None:
        """Process when subtitles track changes."""
        self._logger.debug(
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
        self._logger.debug(("Volume Changed:   Volume: %s   Muted: %s"), event.volume, event.muted)

        update = common.StatusUpdate()
        update.Volume = event.volume
        await self._shuffler.send_status(update)


async def _main() -> None:
    """Execute the main entrypoint."""
    common.set_logging()

    parser = argparse.ArgumentParser(description="Youtube Lounge API Controller.")
    parser.add_argument("--player", help="Name of lounge player in config file")
    parser.add_argument("--verbose", "-v", action="store_true", help="Set log level to DEBUG")
    args = parser.parse_args()

    async with LoungePlayer(args.player, verbose=args.verbose) as player:
        await player.main()


if __name__ == "__main__":
    # https://docs.python.org/3/library/asyncio-dev.html#asyncio-debug-mode
    asyncio.run(_main(), debug=True)
