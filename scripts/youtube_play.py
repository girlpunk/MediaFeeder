#!/usr/bin/env python

import argparse
import sys
import time
from threading import Event

import pychromecast
from pychromecast.controllers.media import MediaStatus, MediaStatusListener
from pychromecast.controllers.youtube import YouTubeController


class MyMediaStatusListener(MediaStatusListener):
    """Status media listener"""

    def __init__(self, name: str | None, cast: pychromecast.Chromecast) -> None:
        self.name = name
        self.cast = cast
        self.last_is_idle = Event()
        self.reset()

    def reset(self):
        self.mark_watched = True
        self.last_is_idle.clear()
        self.last_status = None

    def wait(self, timeout):
        return self.last_is_idle.wait(timeout)

    def new_media_status(self, status: MediaStatus) -> None:
        self.last_status = status
        # print(f"new_media_status: {status}")

        if status.player_state == "IDLE" and status.idle_reason == "FINISHED":
            self.last_is_idle.set()
        else:
            self.last_is_idle.clear()

        # This is for adverts
        if not status.supports_pause:
            return

    def load_media_failed(self, queue_item_id: int, error_code: int) -> None:
        print(
            "load media failed for queue item id: ",
            queue_item_id,
            " with code: ",
            error_code,
        )

    def on_ses_rep(self, iterator):
        try:
            for rep in iterator:
                try:
                    self.on_ses_rep_msg(rep)
                except Exception as e:
                    print(f"failed to handle msg {rep}: {e}")
        except Exception as e:
            print(f"failed to read stream: {e}")

    def on_ses_rep_msg(self, rep):
        if rep.ShouldPlayPause:
            if not self.last_status:
                print("can not play/pause without last_status.")
            elif self.last_status.player_is_paused:
                cast.media_controller.play()
                print("playing")
            elif self.last_status.player_is_playing:
                cast.media_controller.pause()
                print("paused")
            else:
                print(f"can not play/pause in state: {self.last_status.player_state}")

        elif rep.ShouldWatch:
            self.mark_watched = True
            self.last_is_idle.set()

        elif rep.ShouldSkip:
            self.mark_watched = False
            self.last_is_idle.set()


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
    print(f'No chromecast with name "{args.cast}" discovered')
    sys.exit(1)

cast = chromecasts[0]
# Start socket client's worker thread and wait for initial status update
cast.wait()


print("Videos to play:")
for video in args.videos:
    print(f"  - {video}")

yt = YouTubeController()
cast.register_handler(yt)

listenerMedia = MyMediaStatusListener(cast.name, cast)
cast.media_controller.register_status_listener(listenerMedia)


while len(args.videos) > 0:
    video = args.videos.pop(0)

    print(f"Playing: {video}")

    yt.play_video(video)
    listenerMedia.reset()

    while not listenerMedia.wait(5):
        cast.media_controller.update_status()

    time.sleep(1)


# Shut down discovery
browser.stop_discovery()

print("fin.")
